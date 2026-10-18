using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

[ComImport, Guid("8e018a9d-2415-4677-bf08-794ea61f94bb"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ISpellCheckerFactory
{
    void GetSupportedLanguages(out IntPtr languages);
    void IsSupported([MarshalAs(UnmanagedType.LPWStr)] string languageTag, out int supported);
    void CreateSpellChecker([MarshalAs(UnmanagedType.LPWStr)] string languageTag, out ISpellChecker checker);
}

[ComImport, Guid("b6fd0b71-e2bc-4653-8d05-f197e412770b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ISpellChecker
{
    void GetLanguageTag(out IntPtr languageTag);
    void Check([MarshalAs(UnmanagedType.LPWStr)] string text, out IEnumSpellingError errors);
    void Suggest([MarshalAs(UnmanagedType.LPWStr)] string word, out IEnumStrings suggestions);
}

[ComImport, Guid("803e3bd4-2828-4410-8290-418d1d73c762"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IEnumSpellingError
{
    [PreserveSig] int Next(out ISpellingError error);
}

[ComImport, Guid("b7c82d61-fbe8-4b47-9b27-6c0d2e0de0a3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ISpellingError
{
    void GetStartIndex(out uint start);
    void GetLength(out uint length);
    void GetCorrectiveAction(out int action);
    void GetReplacement(out IntPtr replacement);
}

[ComImport, Guid("00000101-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IEnumStrings
{
    [PreserveSig] int Next(uint count, out IntPtr value, out uint fetched);
}

internal sealed class WindowsSpeller
{
    ISpellChecker checker;
    HunspellFallback hunspell;
    public string Language { get; private set; }

    public WindowsSpeller() : this(false) { }

    internal WindowsSpeller(bool forceLocalRules)
    {
        Language = "reglas locales";
        if (forceLocalRules) return;
        try
        {
            var type = Type.GetTypeFromCLSID(new Guid("7ab36653-1796-484b-bdfa-e74f1db7c1dc"));
            var factory = (ISpellCheckerFactory)Activator.CreateInstance(type);
            foreach (string language in new[] { "es-CO", "es-MX", "es-ES" })
            {
                try
                {
                    int supported;
                    factory.IsSupported(language, out supported);
                    if (supported != 0) { factory.CreateSpellChecker(language, out checker); if (checker != null) { Language = language; return; } }
                }
                catch { }
            }
        }
        catch { }
        hunspell = HunspellFallback.TryCreate();
        if (hunspell != null) Language = "es-CO (diccionario local)";
    }

    public List<Issue> Check(string text)
    {
        var found = new List<Issue>();
        if (checker == null)
        {
            foreach (Match match in Regex.Matches(text ?? "", @"\b[\p{L}]{3,}\b"))
            {
                string replacement = "";
                switch (match.Value.ToLowerInvariant())
                {
                    case "prueva": replacement = "prueba"; break;
                    case "ortografia": replacement = "ortografía"; break;
                    case "comenso": replacement = "comenzó"; break;
                    case "enpese": replacement = "empecé"; break;
                    case "aser": replacement = "hacer"; break;
                }
                string[] suggestions = null;
                if (replacement.Length > 0)
                {
                    if (char.IsUpper(match.Value[0])) replacement = char.ToUpperInvariant(replacement[0]) + replacement.Substring(1);
                    suggestions = new[] { replacement };
                }
                else if (hunspell != null && !char.IsUpper(match.Value[0]) &&
                    !Regex.IsMatch(match.Value, @"(?i)^(echo|cmd|dir|cd|cls|copy|del|type|more|findstr|powershell|exe|com|http|https)$"))
                {
                    try { if (!hunspell.IsCorrect(match.Value)) suggestions = hunspell.Suggestions(match.Value); }
                    catch { hunspell = null; Language = "reglas locales"; }
                }
                if (suggestions == null || suggestions.Length == 0) continue;
                found.Add(new Issue { Start = match.Index, Length = match.Length, Word = match.Value, Suggestions = suggestions });
                if (found.Count == 5) break;
            }
            return found;
        }
        IEnumSpellingError errors;
        try { checker.Check(text, out errors); }
        catch { checker = null; hunspell = HunspellFallback.TryCreate(); Language = hunspell == null ? "reglas locales" : "es-CO (diccionario local)"; return Check(text); }
        if (errors == null) return found;
        for (int count = 0; count < 5; count++)
        {
            ISpellingError error;
            int hr = errors.Next(out error);
            if (hr != 0 || error == null) break;
            uint start, length;
            error.GetStartIndex(out start);
            error.GetLength(out length);
            if (start + length > text.Length || length == 0) continue;
            string word = text.Substring((int)start, (int)length);
            var suggestions = new List<string>();
            IEnumStrings strings;
            checker.Suggest(word, out strings);
            if (strings != null)
            {
                for (int j = 0; j < 3; j++)
                {
                    IntPtr value;
                    uint fetched;
                    if (strings.Next(1, out value, out fetched) != 0 || fetched == 0) break;
                    suggestions.Add(Marshal.PtrToStringUni(value));
                    Marshal.FreeCoTaskMem(value);
                }
                Marshal.ReleaseComObject(strings);
            }
            found.Add(new Issue { Start = (int)start, Length = (int)length, Word = word, Suggestions = suggestions.ToArray() });
            Marshal.ReleaseComObject(error);
        }
        Marshal.ReleaseComObject(errors);
        return found;
    }
}
