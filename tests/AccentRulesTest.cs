using System;
using System.Linq;
internal class Issue { public int Start, Length; public string Word; public string[] Suggestions; }
internal class Test
{
    static int Main()
    {
        string[] input = { "¿Como estas?", "Como estas", "Tu eres amable.", "El esta aqui.", "Quiero mas café.", "Como tú quieras.", "hola me envias el comprobante por favor" };
        string[] expected = { "Como:Cómo,estas:estás", "Como:Cómo,estas:estás", "Tu:Tú", "El:Él,esta:está", "mas:más", "", "envias:envías" };
        for (int i = 0; i < input.Length; i++)
        {
            string actual = string.Join(",", AccentRules.Check(input[i]).Select(x => x.Word + ":" + x.Suggestions[0]).ToArray());
            Console.WriteLine(input[i] + " => " + actual);
            if (actual != expected[i]) return 1;
        }
        return 0;
    }
}
