using System;
using System.Linq;

internal sealed class Issue
{
    public int Start, Length;
    public string Word;
    public string[] Suggestions;
}

internal static class SpellApiTest
{
    static int Main()
    {
        var checker = new WindowsSpeller(true);
        CheckKeyboardTypo(checker);
        CheckAcceptedWords(checker);
        var installedChecker = new WindowsSpeller();
        CheckKeyboardTypo(installedChecker);
        CheckAcceptedWords(installedChecker);
        Console.WriteLine("dipas => días: reglas locales y motor " + installedChecker.Language);
        var issues = checker.Check("echo Esto es una prueva de ortografia.");
        if (issues.Count != 2 || issues[0].Word != "prueva" || issues[0].Suggestions[0] != "prueba" ||
            issues[1].Word != "ortografia" || issues[1].Suggestions[0] != "ortografía")
            throw new Exception("Las reglas locales no reconocieron las dos faltas esperadas.");
        if (checker.Check("echo prueba terminada").Any())
            throw new Exception("Se marcó un comando sin faltas conocidas.");
        using (var dictionary = HunspellFallback.TryCreate())
        {
            if (dictionary != null && (!dictionary.IsCorrect("prueba") || dictionary.IsCorrect("prueva") ||
                !dictionary.Suggestions("prueva").Contains("prueba")))
                throw new Exception("El diccionario instalado no devuelve la corrección esperada.");
            if (dictionary != null)
                Console.WriteLine("Diccionario amplio: cassa => " + string.Join(", ", dictionary.Suggestions("cassa")));
        }
        Console.WriteLine("Reglas locales correctas.");
        return 0;
    }

    static void CheckKeyboardTypo(WindowsSpeller checker)
    {
        string[] words = { "dipas", "Dipas", "DIPAS" };
        string[] expected = { "días", "Días", "DÍAS" };
        for (int i = 0; i < words.Length; i++)
        {
            var result = checker.Check("Buenos " + words[i] + ".").Single(x => x.Word == words[i]);
            if (result.Start != 7 || result.Length != 5 || result.Suggestions[0] != expected[i] ||
                result.Suggestions.Length > 3 || result.Suggestions.Distinct().Count() != result.Suggestions.Length)
                throw new Exception("Falta la sugerencia prioritaria para " + words[i]);
        }
        if (checker.Check("Buenos días.").Any(x => x.Word == "días") ||
            checker.Check("dipasextra").Any(x => x.Suggestions.Contains("días")))
            throw new Exception("La regla de teclado marcó una palabra distinta.");
    }

    static void CheckAcceptedWords(WindowsSpeller checker)
    {
        foreach (string word in new[] { "fentermina", "Fentermina", "FENTERMINA" })
        {
            var issues = checker.Check(word + " dipas");
            if (issues.Any(x => x.Word.Equals(word, StringComparison.OrdinalIgnoreCase)) ||
                !issues.Any(x => x.Word == "dipas" && x.Suggestions[0] == "días"))
                throw new Exception("Debe admitir " + word + " sin dejar de revisar las otras palabras.");
        }
        Console.WriteLine("fentermina: admitida en minúsculas, inicial mayúscula y mayúsculas.");
    }
}
