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
}
