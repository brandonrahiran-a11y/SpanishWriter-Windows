using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

internal static class AccentRules
{
    static readonly Dictionary<string, string> QuestionWords = new Dictionary<string, string>
    {
        { "como", "cómo" }, { "que", "qué" }, { "cuando", "cuándo" },
        { "donde", "dónde" }, { "quien", "quién" }, { "cual", "cuál" },
        { "cuanto", "cuánto" }, { "cuanta", "cuánta" },
        { "cuantos", "cuántos" }, { "cuantas", "cuántas" }
    };

    static readonly HashSet<string> SecondPersonVerbs = new HashSet<string>
    {
        "eres", "estas", "estás", "tienes", "quieres", "puedes", "sabes", "vas", "has", "fuiste"
    };
    static readonly HashSet<string> ThirdPersonVerbs = new HashSet<string>
    {
        "es", "esta", "está", "tiene", "quiere", "puede", "sabe", "fue", "hizo", "va", "ha"
    };
    static readonly HashSet<string> ThirdPersonSubjects = new HashSet<string>
    {
        "el", "él", "ella", "usted", "eso", "esto"
    };

    public static List<Issue> Check(string text)
    {
        var words = Regex.Matches(text, @"\p{L}+").Cast<Match>().ToList();
        var issues = new List<Issue>();
        for (int i = 0; i < words.Count; i++)
        {
            var word = words[i];
            var current = word.Value.ToLowerInvariant();
            var previous = i > 0 ? words[i - 1].Value.ToLowerInvariant() : "";
            var next = i + 1 < words.Count ? words[i + 1].Value.ToLowerInvariant() : "";
            string suggestion = null;

            int left = text.LastIndexOfAny(new[] { '.', '!', '?', '\n' }, Math.Max(0, word.Index - 1)) + 1;
            int right = text.IndexOfAny(new[] { '.', '!', '?', '\n' }, word.Index + word.Length);
            if (right < 0) right = text.Length;
            string clause = text.Substring(left, right - left);
            bool question = clause.IndexOf('¿') >= 0 || (right < text.Length && text[right] == '?');

            bool shortGreeting = words.Count == 2 &&
                (words[0].Value.Equals("como", StringComparison.OrdinalIgnoreCase) ||
                 words[0].Value.Equals("cómo", StringComparison.OrdinalIgnoreCase)) &&
                words[1].Value.Equals("estas", StringComparison.OrdinalIgnoreCase);
            if ((question || shortGreeting) && QuestionWords.ContainsKey(current)) suggestion = QuestionWords[current];
            else if (current == "estas" && (previous == "como" || previous == "cómo") &&
                     (question || shortGreeting)) suggestion = "estás";
            else if (current == "tu" && SecondPersonVerbs.Contains(next)) suggestion = "tú";
            else if (current == "el" && ThirdPersonVerbs.Contains(next)) suggestion = "él";
            else if (current == "esta" && ThirdPersonSubjects.Contains(previous)) suggestion = "está";
            else if (current == "mas") suggestion = "más";

            if (suggestion != null)
            {
                if (Char.IsUpper(word.Value[0])) suggestion = Char.ToUpperInvariant(suggestion[0]) + suggestion.Substring(1);
                issues.Add(new Issue { Start = word.Index, Length = word.Length, Word = word.Value, Suggestions = new[] { suggestion } });
            }
        }
        return issues;
    }
}
