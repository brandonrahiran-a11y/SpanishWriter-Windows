using System;
using System.Collections.Generic;
using System.Linq;
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

internal sealed class WindowsSpeller : IDisposable
{
    // Antes solo se marcaban los cinco primeros errores de todo el texto; los
    // siguientes (por ejemplo «comun» al final de un párrafo) quedaban ocultos.
    internal const int MaxIssues = 25;
    readonly CustomDictionary dictionary;
    ISpellChecker checker;
    HunspellFallback hunspell;
    public string Language { get; private set; }

    public WindowsSpeller() : this(false, null) { }

    public WindowsSpeller(CustomDictionary dictionary) : this(false, dictionary) { }

    internal WindowsSpeller(bool forceLocalRules) : this(forceLocalRules, null) { }

    internal WindowsSpeller(bool forceLocalRules, CustomDictionary dictionary)
    {
        // Sin diccionario explícito se usan solo las listas incluidas en el ejecutable.
        this.dictionary = dictionary ?? new CustomDictionary(null);
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

    public void Dispose()
    {
        if (hunspell != null) { hunspell.Dispose(); hunspell = null; }
        if (checker != null) { Marshal.ReleaseComObject(checker); checker = null; }
    }

    public List<Issue> Check(string text)
    {
        var found = CheckEngine(text);
        AddUppercase(text, found);
        // Keep this known keyboard typo ahead of dictionary guesses in every
        // engine, including Windows and the limited local fallback.
        foreach (Match match in Regex.Matches(text ?? "", @"\bdipas\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            string replacement = match.Value == match.Value.ToUpperInvariant() ? "DÍAS" :
                char.IsUpper(match.Value[0]) ? "Días" : "días";
            var issue = found.FirstOrDefault(x => x.Start == match.Index && x.Length == match.Length);
            if (issue == null)
            {
                issue = new Issue { Start = match.Index, Length = match.Length, Word = match.Value, Suggestions = new string[0] };
                found.Add(issue);
            }
            issue.Suggestions = new[] { replacement }.Concat(issue.Suggestions)
                .Distinct(StringComparer.Ordinal).Take(3).ToArray();
        }
        return found.OrderBy(x => x.Start).Take(MaxIssues).ToList();
    }

    // Windows ignora las palabras escritas todo en mayúsculas. Se revisan en
    // minúsculas: con tilde faltante siempre se marcan («COMUN» → «COMÚN»); sin
    // variante con tilde solo desde cinco letras, para no marcar siglas (EPS, TSH).
    void AddUppercase(string text, List<Issue> found)
    {
        if (checker == null && hunspell == null) return;
        foreach (Match match in Regex.Matches(text ?? "", @"\b\p{Lu}{2,}\b"))
        {
            if (found.Count >= MaxIssues) break;
            int start = match.Index, end = match.Index + match.Length;
            if (found.Any(x => x.Start < end && start < x.Start + x.Length)) continue;
            string word = match.Value, lower = word.ToLowerInvariant();
            if (dictionary.IsKnown(word) || Accepts(lower)) continue;
            var engine = word.Length >= 5 ? EngineSuggestions(lower).Select(x => x.ToUpperInvariant()) : new string[0];
            var variants = AccentVariants(word);
            if (variants.Count == 0 && word.Length < 5) continue;
            var suggestions = Rank(word, engine);
            if (suggestions.Length == 0) continue;
            found.Add(new Issue { Start = start, Length = match.Length, Word = word, Suggestions = suggestions });
        }
    }

    List<string> EngineSuggestions(string word)
    {
        var suggestions = new List<string>();
        try
        {
            if (checker != null)
            {
                IEnumStrings strings;
                checker.Suggest(word, out strings);
                if (strings == null) return suggestions;
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
            else if (hunspell != null) suggestions.AddRange(hunspell.Suggestions(word));
        }
        catch { }
        return suggestions;
    }

    // Comprueba una palabra suelta con el motor activo.
    bool Accepts(string word)
    {
        if (dictionary.IsKnown(word)) return true;
        if (checker != null)
        {
            try
            {
                IEnumSpellingError errors;
                checker.Check(word, out errors);
                if (errors == null) return true;
                ISpellingError error;
                int hr = errors.Next(out error);
                bool valid = hr != 0 || error == null;
                if (error != null) Marshal.ReleaseComObject(error);
                Marshal.ReleaseComObject(errors);
                return valid;
            }
            catch { return false; }
        }
        if (hunspell != null)
        {
            try { return hunspell.IsCorrect(word); } catch { return false; }
        }
        return false;
    }

    // Variantes de la palabra con una tilde, ñ o ü que el motor reconoce:
    // «jovenes» → «jóvenes», «musica» → «música», «tia» → «tía», «pinguino» → «pingüino».
    // Van antes que las sugerencias del motor, que a veces proponen otra palabra
    // («musical», «acciona», «corazones»).
    internal List<string> AccentVariants(string word)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(word) || word.Length < 2 || word.Length > 30) return result;
        string lower = word.ToLowerInvariant();
        bool hasAccent = lower.IndexOfAny("áéíóú".ToCharArray()) >= 0;
        const string plain = "aeioun", marked = "áéíóúñ";
        for (int i = 0; i < lower.Length; i++)
        {
            int index = plain.IndexOf(lower[i]);
            var candidates = new List<char>();
            if (index >= 0 && (index == 5 || !hasAccent)) candidates.Add(marked[index]);
            if (lower[i] == 'u' && i > 0 && lower[i - 1] == 'g' && i + 1 < lower.Length && "ei".IndexOf(lower[i + 1]) >= 0) candidates.Add('ü');
            foreach (char c in candidates)
            {
                string candidate = lower.Substring(0, i) + c + lower.Substring(i + 1);
                if (Accepts(candidate)) result.Add(CustomDictionary.MatchCase(word, candidate));
            }
        }
        return result;
    }

    string[] Rank(string word, IEnumerable<string> engine)
    {
        var merged = CustomDictionary.Merge(word, engine, dictionary.Suggest(word, 3), 3);
        return AccentVariants(word).Concat(merged).Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToArray();
    }

    List<Issue> CheckEngine(string text)
    {
        var found = new List<Issue>();
        if (checker == null)
        {
            foreach (Match match in Regex.Matches(text ?? "", @"\b[\p{L}]{3,}\b"))
            {
                if (dictionary.IsKnown(match.Value)) continue;
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
                    try
                    {
                        if (!hunspell.IsCorrect(match.Value))
                            suggestions = Rank(match.Value, hunspell.Suggestions(match.Value));
                    }
                    catch { hunspell.Dispose(); hunspell = null; Language = "reglas locales"; }
                }
                if (suggestions == null || suggestions.Length == 0) continue;
                found.Add(new Issue { Start = match.Index, Length = match.Length, Word = match.Value, Suggestions = suggestions });
                if (found.Count == MaxIssues) break;
            }
            return found;
        }
        IEnumSpellingError errors;
        try { checker.Check(text, out errors); }
        catch { checker = null; hunspell = HunspellFallback.TryCreate(); Language = hunspell == null ? "reglas locales" : "es-CO (diccionario local)"; return CheckEngine(text); }
        if (errors == null) return found;
        // Las palabras del diccionario complementario no cuentan para el límite.
        for (int seen = 0; seen < 200 && found.Count < MaxIssues; seen++)
        {
            ISpellingError error;
            int hr = errors.Next(out error);
            if (hr != 0 || error == null) break;
            uint start, length;
            error.GetStartIndex(out start);
            error.GetLength(out length);
            if (start + length > text.Length || length == 0) { Marshal.ReleaseComObject(error); continue; }
            string word = text.Substring((int)start, (int)length);
            if (dictionary.IsKnown(word)) { Marshal.ReleaseComObject(error); continue; }
            var suggestions = EngineSuggestions(word);
            found.Add(new Issue { Start = (int)start, Length = (int)length, Word = word,
                Suggestions = Rank(word, suggestions) });
            Marshal.ReleaseComObject(error);
        }
        Marshal.ReleaseComObject(errors);
        return found;
    }
}
