using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

// Palabras que el corrector de Windows o Hunspell no conocen: listas incluidas en
// el ejecutable (términos médicos, uso cotidiano) y el diccionario personal del
// usuario. También propone correcciones cercanas cuando el motor sugiere
// palabras sin relación con la que se escribió.
internal sealed class CustomDictionary
{
    const string ResourcePrefix = "SpanishWriter.Dict.";
    readonly HashSet<string> words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    readonly List<string> ordered = new List<string>();
    readonly List<string> folded = new List<string>();
    readonly string userFile;

    public CustomDictionary() : this(DefaultUserFile()) { }

    internal CustomDictionary(string userFile)
    {
        this.userFile = userFile;
        Reload();
    }

    public string UserFile { get { return userFile; } }
    public int Count { get { return words.Count; } }
    public int UserCount { get; private set; }

    public static string DefaultUserFile()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SpanishWriter", "diccionario-personal.txt");
    }

    // Devuelve false si no pudo leer el diccionario personal; en ese caso conserva
    // las palabras cargadas antes.
    public bool Reload()
    {
        string personal = null;
        if (userFile != null && File.Exists(userFile))
        {
            try { personal = File.ReadAllText(userFile, Encoding.UTF8); }
            catch { if (words.Count > 0) return false; }
        }
        words.Clear(); ordered.Clear(); folded.Clear();
        var assembly = Assembly.GetExecutingAssembly();
        foreach (string name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal)) continue;
            using (var stream = assembly.GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                AddLines(reader.ReadToEnd());
        }
        int before = words.Count;
        if (personal != null) AddLines(personal);
        UserCount = words.Count - before;
        return true;
    }

    void AddLines(string text)
    {
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim(' ', '\t', '\r', '﻿');
            if (line.Length == 0 || line[0] == '#') continue;
            foreach (string token in line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                AddWord(token);
        }
    }

    void AddWord(string word)
    {
        word = word.Normalize(NormalizationForm.FormC);
        if (!IsValidWord(word) || !words.Add(word)) return;
        ordered.Add(word);
        folded.Add(Fold(word));
    }

    internal static bool IsValidWord(string word)
    {
        if (string.IsNullOrEmpty(word) || word.Length > 60 || !char.IsLetter(word[0])) return false;
        foreach (char c in word)
            if (!char.IsLetter(c) && c != '-' && c != '\'' && c != '’') return false;
        return true;
    }

    public bool IsKnown(string word)
    {
        if (string.IsNullOrEmpty(word)) return false;
        word = word.Normalize(NormalizationForm.FormC);
        if (words.Contains(word)) return true;
        // Plurales regulares: «cefaleas», «adenopatías», «hipoglucemiantes», «antibióticos».
        if (word.Length > 4 && EndsWith(word, "es"))
        {
            string stem = word.Substring(0, word.Length - 2);
            // «herpes», «tórax»: palabras en -s/-x no forman plural con -es aquí.
            char last = char.ToLowerInvariant(stem[stem.Length - 1]);
            if (!IsVowel(last) && last != 's' && last != 'x' && words.Contains(stem)) return true;
        }
        if (word.Length > 3 && EndsWith(word, "s"))
        {
            string stem = word.Substring(0, word.Length - 1);
            if (IsVowel(stem[stem.Length - 1]) && words.Contains(stem)) return true;
        }
        return false;
    }

    // Agrega una palabra al diccionario personal y la guarda en disco.
    public bool Add(string word)
    {
        if (word == null) return false;
        word = word.Trim().Normalize(NormalizationForm.FormC);
        if (!IsValidWord(word)) return false;
        if (words.Contains(word)) return true;
        if (userFile == null) { AddWord(word); return true; }
        string folder = Path.GetDirectoryName(userFile);
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        if (!File.Exists(userFile))
            File.WriteAllText(userFile, "# Diccionario personal de SpanishWriter. Una palabra por línea." + Environment.NewLine, new UTF8Encoding(false));
        File.AppendAllText(userFile, word + Environment.NewLine, new UTF8Encoding(false));
        AddWord(word);
        UserCount++;
        return true;
    }

    // Devuelve palabras del diccionario parecidas a la escrita, con su distancia.
    public List<KeyValuePair<string, int>> Suggest(string word, int max)
    {
        var result = new List<KeyValuePair<string, int>>();
        if (string.IsNullOrEmpty(word) || word.Length < 3) return result;
        string key = Fold(word);
        int limit = key.Length >= 8 ? 2 : 1;
        for (int i = 0; i < ordered.Count; i++)
        {
            string candidate = folded[i];
            if (Math.Abs(candidate.Length - key.Length) > limit) continue;
            int distance = candidate == key ? 0 : Distance(key, candidate, limit);
            if (distance <= limit) result.Add(new KeyValuePair<string, int>(MatchCase(word, ordered[i]), distance));
        }
        return result.OrderBy(x => x.Value).Take(max).ToList();
    }

    // Combina las sugerencias del motor con las del diccionario: una palabra del
    // diccionario se coloca antes de cualquier sugerencia del motor más alejada.
    internal static string[] Merge(string word, IEnumerable<string> engine, List<KeyValuePair<string, int>> custom, int max)
    {
        var engineList = (engine ?? new string[0]).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (custom == null || custom.Count == 0) return engineList.Distinct(StringComparer.Ordinal).Take(max).ToArray();
        string key = Fold(word);
        var merged = new List<string>();
        int next = 0;
        foreach (var candidate in custom)
        {
            while (next < engineList.Count && Distance(key, Fold(engineList[next]), int.MaxValue) <= candidate.Value)
                merged.Add(engineList[next++]);
            merged.Add(candidate.Key);
        }
        while (next < engineList.Count) merged.Add(engineList[next++]);
        return merged.Distinct(StringComparer.OrdinalIgnoreCase).Take(max).ToArray();
    }

    internal static string MatchCase(string original, string candidate)
    {
        if (original.Length > 1 && original == original.ToUpperInvariant() && original != original.ToLowerInvariant())
            return candidate.ToUpperInvariant();
        if (char.IsUpper(original[0]) && !char.IsUpper(candidate[0]))
            return char.ToUpperInvariant(candidate[0]) + candidate.Substring(1);
        return candidate;
    }

    internal static string Fold(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (char c in text.ToLowerInvariant().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) builder.Append(c);
        return builder.ToString();
    }

    // Distancia de Damerau-Levenshtein (alineación óptima) con corte temprano.
    internal static int Distance(string a, string b, int limit)
    {
        if (a == b) return 0;
        int n = a.Length, m = b.Length;
        if (limit != int.MaxValue && Math.Abs(n - m) > limit) return limit + 1;
        var previous2 = new int[m + 1];
        var previous = new int[m + 1];
        var current = new int[m + 1];
        for (int j = 0; j <= m; j++) previous[j] = j;
        for (int i = 1; i <= n; i++)
        {
            current[0] = i;
            int best = current[0];
            for (int j = 1; j <= m; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                int value = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    value = Math.Min(value, previous2[j - 2] + 1);
                current[j] = value;
                if (value < best) best = value;
            }
            if (limit != int.MaxValue && best > limit) return limit + 1;
            var swap = previous2; previous2 = previous; previous = current; current = swap;
        }
        return previous[m];
    }

    static bool EndsWith(string word, string suffix)
    {
        return word.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
    }

    static bool IsVowel(char c)
    {
        return "aeiouáéíóúAEIOUÁÉÍÓÚ".IndexOf(c) >= 0;
    }
}
