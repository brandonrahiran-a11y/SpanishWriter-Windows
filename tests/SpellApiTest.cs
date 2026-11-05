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
        var uppercase = checker.Check("PRUEVA y Ortografia");
        if (uppercase.Count != 2 || uppercase[0].Suggestions[0] != "PRUEBA" ||
            uppercase[1].Suggestions[0] != "Ortografía")
            throw new Exception("Las sugerencias no conservaron las mayúsculas.");
        var active = new WindowsSpeller();
        var activeUppercase = active.Check("PRUEVA");
        if (activeUppercase.Count != 1 || activeUppercase[0].Suggestions[0] != "PRUEBA" ||
            active.Check("PRUEBA").Any())
            throw new Exception("El corrector activo no detectó correctamente las mayúsculas.");
        var combined = active.Check("PRUEVA y prueva");
        if (combined.Count != 2 || combined[0].Start != 0 || combined[0].Suggestions[0] != "PRUEBA" ||
            combined[1].Start != 9 || combined[1].Suggestions[0] != "prueba")
            throw new Exception("Los errores en mayúsculas no conservaron su posición.");
        using (var dictionary = HunspellFallback.TryCreate())
        {
            if (dictionary != null && (!dictionary.IsCorrect("prueba") || dictionary.IsCorrect("prueva") ||
                !dictionary.Suggestions("prueva").Contains("prueba")))
                throw new Exception("El diccionario instalado no devuelve la corrección esperada.");
            if (dictionary != null)
            {
                var withDictionary = new WindowsSpeller(dictionary);
                var uppercaseDictionary = withDictionary.Check("PRUEVA y Prueva");
                if (uppercaseDictionary.Count != 2 || uppercaseDictionary[0].Suggestions[0] != "PRUEBA" ||
                    uppercaseDictionary[1].Suggestions[0] != "Prueba")
                    throw new Exception("El diccionario no detectó las faltas en mayúsculas.");
                Console.WriteLine("Diccionario amplio: cassa => " + string.Join(", ", dictionary.Suggestions("cassa")));
            }
        }
        Console.WriteLine("Reglas locales correctas.");
        return 0;
    }
}
