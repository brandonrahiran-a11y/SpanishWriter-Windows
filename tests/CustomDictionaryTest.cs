using System;
using System.IO;
using System.Linq;

internal sealed class Issue
{
    public int Start, Length;
    public string Word;
    public string[] Suggestions;
}

internal static class CustomDictionaryTest
{
    static void Expect(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static int Main()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SpanishWriterTest-" + Guid.NewGuid().ToString("N"));
        string file = Path.Combine(folder, "diccionario-personal.txt");
        try
        {
            var dictionary = new CustomDictionary(file);
            Expect(dictionary.Count > 400, "No se cargaron las listas incluidas: " + dictionary.Count);

            foreach (string word in new[] { "carvedilol", "Bisoprolol", "LOSARTÁN", "empagliflozina", "bacano", "whatsapp", "eupneico" })
                Expect(dictionary.IsKnown(word), "Falta en el diccionario: " + word);
            foreach (string word in new[] { "adenopatías", "artralgias", "hipoglucemiantes", "comorbilidades" })
                Expect(dictionary.IsKnown(word), "No se reconoció el plural: " + word);
            foreach (string word in new[] { "carvedilool", "piopollo", "losartan", "ácidoo", "herpeses" })
                Expect(!dictionary.IsKnown(word), "Se aceptó una palabra mal escrita: " + word);
            Console.WriteLine("Listas incluidas: " + dictionary.Count + " palabras y plurales regulares.");

            // Sugerencias cercanas y sin tilde.
            Expect(dictionary.Suggest("losartan", 3).First().Key == "losartán", "losartan debe sugerir losartán.");
            Expect(dictionary.Suggest("Carvedilool", 3).First().Key == "Carvedilol", "Debe respetar la mayúscula inicial.");
            Expect(dictionary.Suggest("BISOPROLLOL", 3).First().Key == "BISOPROLOL", "Debe respetar las mayúsculas.");
            Expect(dictionary.Suggest("metfromina", 3).First().Key == "metformina", "Debe reconocer letras transpuestas.");
            Expect(!dictionary.Suggest("cassa", 3).Any(), "No debe sugerir términos lejanos para palabras comunes.");

            // Las sugerencias del diccionario desplazan las del motor que están más lejos.
            var merged = CustomDictionary.Merge("bisoprolool", new[] { "bosorola", "piopollo" }, dictionary.Suggest("bisoprolool", 3), 3);
            Expect(merged[0] == "bisoprolol", "La sugerencia médica debe ir primero: " + string.Join(", ", merged));
            merged = CustomDictionary.Merge("prueva", new[] { "prueba", "pruebas" }, dictionary.Suggest("prueva", 3), 3);
            Expect(merged[0] == "prueba", "No debe alterar el orden del motor cuando no hay coincidencias.");
            Console.WriteLine("Sugerencias: losartan => losartán, bisoprolool => bisoprolol antes que piopollo.");

            // Diccionario personal.
            Expect(!dictionary.IsKnown("Kooltest"), "La palabra de prueba ya existía.");
            Expect(dictionary.Add("Kooltest"), "No se pudo agregar una palabra.");
            Expect(!dictionary.Add("dos palabras") && !dictionary.Add("abc123") && !dictionary.Add(""), "Debe rechazar entradas no válidas.");
            Expect(dictionary.IsKnown("kooltest"), "La palabra agregada no se reconoce.");
            var reloaded = new CustomDictionary(file);
            Expect(reloaded.IsKnown("Kooltest") && reloaded.UserCount == 1, "La palabra agregada no se guardó en disco.");
            Console.WriteLine("Diccionario personal: guardado y recargado.");

            // El motor completo admite términos del diccionario y sigue marcando errores.
            foreach (var speller in new[] { new WindowsSpeller(true, reloaded), new WindowsSpeller(reloaded) })
            {
                var issues = speller.Check("Toma carvedilol, kooltest y bisoprolol dipas.");
                Expect(!issues.Any(x => x.Word == "carvedilol" || x.Word == "kooltest" || x.Word == "bisoprolol"),
                    "El motor " + speller.Language + " marcó palabras del diccionario.");
                Expect(issues.Any(x => x.Word == "dipas"), "El motor " + speller.Language + " dejó de marcar errores.");
            }
            var windows = new WindowsSpeller(reloaded);
            bool windowsEngine = windows.Language.StartsWith("es-") && !windows.Language.Contains("(");
            if (windowsEngine)
            {
                var issue = windows.Check("Paciente con carvedilool.").Single();
                Expect(issue.Suggestions[0] == "carvedilol", "Motor " + windows.Language + ": " + string.Join(", ", issue.Suggestions));
                Console.WriteLine("Motor " + windows.Language + ": carvedilool => " + string.Join(", ", issue.Suggestions));
            }
            if (windowsEngine)
            {
                // Antes solo se mostraban cinco errores; «comun» al final quedaba oculto.
                var many = windows.Check("Tambien despues aqui asi facil rapido, Es algo comun.");
                Expect(many.Count == 7 && many.Last().Word == "comun" && many.Last().Suggestions[0] == "común",
                    "Debe marcar más de cinco errores: " + string.Join(", ", many.Select(x => x.Word).ToArray()));
                string[] words = { "musica", "jovenes", "corazon", "accion", "tia", "Pinguino", "sabado" };
                string[] expected = { "música", "jóvenes", "corazón", "acción", "tía", "Pingüino", "sábado" };
                for (int i = 0; i < words.Length; i++)
                {
                    var issue = windows.Check("x " + words[i] + " x").Single(x => x.Word == words[i]);
                    Expect(issue.Suggestions[0] == expected[i], words[i] + " => " + string.Join(", ", issue.Suggestions));
                }
                // Palabras en mayúsculas: Windows las ignora; las siglas no se marcan.
                var caps = windows.Check("ES ALGO COMUN AQUI, EL PACIENTE TOMA LOSARTAN. EPS TSH SOAT HTA COVID");
                string capsResult = string.Join(", ", caps.Select(x => x.Word + "=" + x.Suggestions[0]).ToArray());
                Expect(capsResult == "COMUN=COMÚN, AQUI=AQUÍ, LOSARTAN=LOSARTÁN", "Mayúsculas: " + capsResult);
                Console.WriteLine("Mayúsculas: " + capsResult + "; siglas sin marcar.");
                Console.WriteLine("Tildes primero: musica => música, jovenes => jóvenes, Pinguino => Pingüino; más de cinco errores.");
            }
            Console.WriteLine("Diccionario complementario correcto.");
            return 0;
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch { }
        }
    }
}
