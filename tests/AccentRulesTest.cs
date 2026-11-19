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
        return CheckNewCases() ? 0 : 1;
    }

    static bool CheckNewCases()
    {
        string[,] cases = {
            { "el numero de teléfono", "numero:número" },
            { "La pagina principal", "pagina:página" },
            { "los medicos llegaron", "medicos:médicos" },
            { "las medicas trabajan", "medicas:médicas" },
            { "un termino raro", "termino:término" },
            { "una practica útil", "practica:práctica" },
            { "su critica fue justa", "critica:crítica" },
            { "mi calculo fue correcto", "calculo:cálculo" },
            { "los titulos nuevos", "titulos:títulos" },
            { "unos articulos largos", "articulos:artículos" },
            { "el ultimo capitulo", "ultimo:último,capitulo:capítulo" },
            { "aquel catalogo", "catalogo:catálogo" },
            { "otra formula", "formula:fórmula" },
            { "el politico habló", "politico:político" },
            { "la clinica abrió", "clinica:clínica" },
            { "la papa está cocida", "" },
            { "el papa viajó", "" },
            { "mi papa", "papa:papá" },
            { "Tu mama llegó", "mama:mamá" },
            { "Mama, ven", "Mama:Mamá" },
            { "mama,", "mama:mamá" },
            { "papa, ven", "papa:papá" },
            { "la mama vino", "" },
            { "yo llamo mañana", "" },
            { "te llamo luego", "" },
            { "lo llamo ahora", "" },
            { "me llamo Juan", "" },
            { "ayer llamo el doctor", "llamo:llamó" },
            { "anoche llego la doctora", "llego:llegó" },
            { "anteayer compro pan", "compro:compró" },
            { "él hablo ayer", "hablo:habló" },
            { "ella termino temprano", "termino:terminó" },
            { "usted pregunto algo", "pregunto:preguntó" },
            { "el paciente pago", "pago:pagó" },
            { "la médica entro", "entro:entró" },
            { "el lunes pasado firmo", "firmo:firmó" },
            { "ayer yo llamo", "" },
            { "yo tenia tiempo", "tenia:tenía" },
            { "ella sabia la verdad", "sabia:sabía" },
            { "no podia venir", "podia:podía" },
            { "me gustaria ir", "gustaria:gustaría" },
            { "se podria hacer", "podria:podría" },
            { "yo hacia la tarea", "hacia:hacía" },
            { "no hacia nada", "hacia:hacía" },
            { "voy hacia la casa", "" },
            { "camino hacia el parque", "" },
            { "aun no llega", "aun:aún" },
            { "aun más lejos", "aun:aún" },
            { "no ha llegado aun", "aun:aún" },
            { "aun así lo hizo", "" },
            { "eso si.", "si:sí" },
            { "ahora si, vamos", "si:sí" },
            { "claro que si", "si:sí" },
            { "creo que si", "si:sí" },
            { "dijo que si", "si:sí" },
            { "pues si", "si:sí" },
            { "si llueve, me quedo", "" },
            { "no se que hacer", "se:sé" },
            { "yo se nada", "se:sé" },
            { "se lo dije", "" },
            { "para mi casa", "" },
            { "es para mi.", "mi:mí" },
            { "hablan de mi, creo", "mi:mí" },
            { "para mi mismo", "mi:mí" },
            { "¿y tu?", "tu:tú" },
            { "tu, ven", "tu:tú" },
            { "tu mismo lo sabes", "tu:tú" },
            { "Tu casa es linda", "" },
            { "tu mamá vino", "" },
            { "es para el.", "el:él" },
            { "habla con el, luego", "el:él" },
            { "el dijo eso", "el:él" },
            { "el llego ayer", "el:él" },
            { "el doctor vino", "" },
            { "el vino tinto", "" },
            { "la seria situación", "" },
            { "una persona seria", "" },
            { "no seria posible", "seria:sería" },
            { "la tenia es un parásito", "" },
            { "la termino mañana", "" },
            { "ella la practica a diario", "" },
            { "no la publica", "" },
            { "la practica médica", "practica:práctica" },
            { "el limite de velocidad", "limite:límite" }
        };
        for (int i = 0; i < cases.GetLength(0); i++)
        {
            var issues = AccentRules.Check(cases[i, 0]);
            string actual = string.Join(",", issues.Select(x => x.Word + ":" + x.Suggestions[0]).ToArray());
            Console.WriteLine(cases[i, 0] + " => " + actual);
            if (actual != cases[i, 1]) return false;
            if (issues.Select(x => x.Start).Distinct().Count() != issues.Count) return false;
            if (issues.Any(x => cases[i, 0].Substring(x.Start, x.Length) != x.Word)) return false;
        }
        return true;
    }
}
