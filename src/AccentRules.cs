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

    static readonly HashSet<string> Determiners = Set("el la los las un una unos unas mi mis tu tus su sus este esta ese esa aquel aquella del al nuestro nuestra cada otro otra primer ultimo último");
    static readonly HashSet<string> Possessives = Set("mi tu su mis tus sus nuestra nuestro");
    static readonly HashSet<string> PastSubjects = Set("él ella usted");
    static readonly HashSet<string> PastSubjectNouns = Set("paciente doctor doctora médico médica mamá mama");
    static readonly HashSet<string> PastTimes = Set("ayer anoche anteayer");
    static readonly HashSet<string> Weekdays = Set("lunes martes miércoles jueves viernes sábado domingo");
    static readonly HashSet<string> ImperfectPrefixes = Set("yo él ella usted no ya se me le lo te nos");
    static readonly HashSet<string> HaciaPrefixes = Set("yo él ella usted no");
    static readonly HashSet<string> SeFollowers = Set("que si nada cómo como donde dónde cuando cuándo quien quién");
    static readonly HashSet<string> Prepositions = Set("a para de por sin sobre ante contra hacia hasta en entre según");
    static readonly HashSet<string> MiFollowers = Set("me mismo misma también tampoco no");
    static readonly HashSet<string> TuFollowers = Set("mismo misma también tampoco no sí solo sola");
    static readonly HashSet<string> ElPrepositions = Set("para de con sin por a");
    static readonly HashSet<string> AdditionalThirdPersonVerbs = Set("dijo hizo tiene quiere sabe llegó llego trabaja vive dice piensa cree");

    static readonly HashSet<string> FeminineDeterminers = Set("la las una unas esta esa aquella nuestra otra");
    static readonly HashSet<string> MasculineDeterminers = Set("el los un unos este ese aquel del al nuestro otro primer ultimo último");
    static readonly HashSet<string> CliticSubjects = Set("yo tú tu él ella usted ustedes ellos ellas nosotros no ya nunca siempre también tampoco se");

    // «la termino mañana» y «ella la practica» son verbos con pronombre: exigir
    // concordancia de género y descartar «la/las» precedidos de sujeto o negación.
    static bool NounContext(string noun, string determiner, string before)
    {
        bool feminine = noun.EndsWith("a") || noun.EndsWith("as");
        if (FeminineDeterminers.Contains(determiner) && !feminine) return false;
        if (MasculineDeterminers.Contains(determiner) && feminine) return false;
        if ((determiner == "la" || determiner == "las") && CliticSubjects.Contains(before)) return false;
        return true;
    }

    static readonly Dictionary<string, string> Nouns = MakeNouns();
    static readonly Dictionary<string, string> PastVerbs = Pairs(
        "llamo:llamó hablo:habló llego:llegó paso:pasó compro:compró pregunto:preguntó trabajo:trabajó " +
        "termino:terminó cambio:cambió quedo:quedó dejo:dejó tomo:tomó mando:mandó empezo:empezó " +
        "comenzo:comenzó llevo:llevó entro:entró presento:presentó firmo:firmó envio:envió " +
        "pago:pagó busco:buscó saco:sacó explico:explicó toco:tocó cerro:cerró " +
        "encontro:encontró mostro:mostró conto:contó costo:costó penso:pensó " +
        "recordo:recordó probo:probó acosto:acostó");
    static readonly Dictionary<string, string> ImperfectVerbs = Pairs(
        "tenia:tenía sabia:sabía seria:sería habia:había creia:creía podia:podía queria:quería " +
        "decia:decía hacia:hacía vivia:vivía comia:comía salia:salía venia:venía " +
        "sentia:sentía pedia:pedía debia:debía estaria:estaría tendria:tendría " +
        "podria:podría haria:haría diria:diría sabria:sabría querria:querría gustaria:gustaría");

    static HashSet<string> Set(string values)
    {
        return new HashSet<string>(values.Split(' '));
    }

    static Dictionary<string, string> Pairs(string values)
    {
        var result = new Dictionary<string, string>();
        foreach (string pair in values.Split(' '))
        {
            string[] parts = pair.Split(':');
            result.Add(parts[0], parts[1]);
        }
        return result;
    }

    static Dictionary<string, string> MakeNouns()
    {
        var result = Pairs(
            "numero:número pagina:página medico:médico medica:médica termino:término publico:público " +
            "practica:práctica critica:crítica lampara:lámpara liquido:líquido limite:límite " +
            "deposito:depósito transito:tránsito animo:ánimo calculo:cálculo titulo:título " +
            "vinculo:vínculo articulo:artículo capitulo:capítulo estimulo:estímulo formula:fórmula " +
            "musculo:músculo credito:crédito exito:éxito habito:hábito solido:sólido " +
            "rapido:rápido ultimo:último rotulo:rótulo circulo:círculo catalogo:catálogo " +
            "dialogo:diálogo intimo:íntimo publica:pública practico:práctico critico:crítico " +
            "economico:económico politico:político politica:política unico:único unica:única " +
            "tecnico:técnico clinico:clínico clinica:clínica fisico:físico quimico:químico " +
            "logico:lógico magico:mágico");
        foreach (KeyValuePair<string, string> pair in result.ToArray())
            result.Add(pair.Key + "s", pair.Value + "s");
        return result;
    }

    static bool SameClause(string text, Match first, Match second)
    {
        if (first == null || second == null) return false;
        int start = first.Index + first.Length;
        return text.IndexOfAny(new[] { '.', '!', '?', '\n', ';', ':' }, start, second.Index - start) < 0;
    }

    static bool EndOrComma(string text, Match word, Match next)
    {
        int end = word.Index + word.Length;
        int limit = next == null ? text.Length : next.Index;
        string gap = text.Substring(end, limit - end).TrimStart();
        return (gap.Length == 0 && next == null) ||
            (gap.Length > 0 && (gap[0] == ',' || gap[0] == '.' || gap[0] == '!' || gap[0] == '?' || gap[0] == ';' || gap[0] == '\n'));
    }

    static bool EndOfPhrase(string text, Match word, Match next)
    {
        int end = word.Index + word.Length;
        int limit = next == null ? text.Length : next.Index;
        string gap = text.Substring(end, limit - end).TrimStart();
        return (gap.Length == 0 && next == null) ||
            (gap.Length > 0 && (gap[0] == '.' || gap[0] == '!' || gap[0] == '?' || gap[0] == '\n'));
    }

    static bool AtPhraseStart(string text, Match word)
    {
        int i = word.Index - 1;
        while (i >= 0 && Char.IsWhiteSpace(text[i])) i--;
        return i < 0 || text[i] == '.' || text[i] == '!' || text[i] == '?' || text[i] == '\n' || text[i] == '¿' || text[i] == '¡';
    }

    static bool FollowedByComma(string text, Match word)
    {
        int i = word.Index + word.Length;
        while (i < text.Length && Char.IsWhiteSpace(text[i])) i++;
        return i < text.Length && text[i] == ',';
    }

    static bool HasPastContext(string text, List<Match> words, int i)
    {
        if (i == 0 || !SameClause(text, words[i - 1], words[i])) return false;
        string previous = words[i - 1].Value.ToLowerInvariant();
        if (previous == "yo") return false;
        if (PastSubjects.Contains(previous) || PastTimes.Contains(previous)) return true;
        if (i >= 2 && PastSubjectNouns.Contains(previous) &&
            (words[i - 2].Value.Equals("el", StringComparison.OrdinalIgnoreCase) ||
             words[i - 2].Value.Equals("la", StringComparison.OrdinalIgnoreCase) ||
             words[i - 2].Value.Equals("mi", StringComparison.OrdinalIgnoreCase)) &&
            SameClause(text, words[i - 2], words[i - 1])) return true;
        if (i >= 3 && previous == "pasado" && Weekdays.Contains(words[i - 2].Value.ToLowerInvariant()) &&
            words[i - 3].Value.Equals("el", StringComparison.OrdinalIgnoreCase) &&
            SameClause(text, words[i - 3], words[i - 2]) && SameClause(text, words[i - 2], words[i - 1])) return true;
        return false;
    }

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
            Match previousMatch = i > 0 ? words[i - 1] : null;
            Match nextMatch = i + 1 < words.Count ? words[i + 1] : null;
            bool hasPrevious = SameClause(text, previousMatch, word);
            bool hasNext = SameClause(text, word, nextMatch);
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
            else if (current == "tu" && ((hasNext && (SecondPersonVerbs.Contains(next) || TuFollowers.Contains(next))) ||
                     EndOrComma(text, word, nextMatch))) suggestion = "tú";
            else if (current == "el" && ((hasNext && (ThirdPersonVerbs.Contains(next) || AdditionalThirdPersonVerbs.Contains(next))) ||
                     (hasPrevious && ElPrepositions.Contains(previous) && EndOrComma(text, word, nextMatch)))) suggestion = "él";
            else if (current == "esta" && ThirdPersonSubjects.Contains(previous)) suggestion = "está";
            else if (current == "envias") suggestion = "envías";
            else if (current == "mas") suggestion = "más";
            else if (Nouns.ContainsKey(current) && hasPrevious && Determiners.Contains(previous) &&
                     NounContext(current, previous, i >= 2 && SameClause(text, words[i - 2], words[i - 1]) ? words[i - 2].Value.ToLowerInvariant() : ""))
                suggestion = Nouns[current];
            else if ((current == "mama" || current == "papa") &&
                     ((hasPrevious && Possessives.Contains(previous)) ||
                      (AtPhraseStart(text, word) && FollowedByComma(text, word))))
                suggestion = current == "mama" ? "mamá" : "papá";
            else if (PastVerbs.ContainsKey(current) && HasPastContext(text, words, i)) suggestion = PastVerbs[current];
            else if (ImperfectVerbs.ContainsKey(current) && hasPrevious &&
                     (current == "hacia" ? HaciaPrefixes.Contains(previous) : ImperfectPrefixes.Contains(previous)))
                suggestion = ImperfectVerbs[current];
            else if (current == "aun" && ((hasNext && (next == "no" || next == "mas" || next == "más" || next == "menos")) ||
                     EndOfPhrase(text, word, nextMatch))) suggestion = "aún";
            else if (current == "si" && hasPrevious && EndOrComma(text, word, nextMatch) &&
                     (previous == "eso" || previous == "ahora" || previous == "pues" || previous == "que")) suggestion = "sí";
            else if (current == "se" && hasPrevious && (previous == "yo" || previous == "no") &&
                     (EndOrComma(text, word, nextMatch) || (hasNext && SeFollowers.Contains(next)))) suggestion = "sé";
            else if (current == "mi" && hasPrevious && Prepositions.Contains(previous) &&
                     (EndOrComma(text, word, nextMatch) || (hasNext && MiFollowers.Contains(next)))) suggestion = "mí";

            if (suggestion != null)
            {
                if (Char.IsUpper(word.Value[0])) suggestion = Char.ToUpperInvariant(suggestion[0]) + suggestion.Substring(1);
                issues.Add(new Issue { Start = word.Index, Length = word.Length, Word = word.Value, Suggestions = new[] { suggestion } });
            }
        }
        return issues;
    }
}
