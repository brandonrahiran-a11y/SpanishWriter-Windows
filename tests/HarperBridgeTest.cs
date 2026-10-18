using System;
using System.Linq;
using System.Windows.Automation.Text;

internal class Issue
{
    public int Start, Length;
    public string Word;
    public string[] Suggestions;
    public TextPatternRange Range;
}

internal static class HarperBridgeTest
{
    static int Main()
    {
        const string json = "[{\"file\":\"<stdin>\",\"lints\":[{\"span\":{\"char_start\":10,\"char_end\":12},\"suggestions\":[\"a\"],\"matched_text\":\"an\"}]}]";
        var issues = HarperBridge.ParseJson("😀 This is an test.", json);
        if (issues.Count != 1 || issues[0].Word != "an" || issues[0].Start != 11 || issues[0].Suggestions.Single() != "a")
            throw new Exception("La conversión de posiciones de Harper a Windows falló.");
        Console.WriteLine("Formato Harper correcto.");
        return 0;
    }
}
