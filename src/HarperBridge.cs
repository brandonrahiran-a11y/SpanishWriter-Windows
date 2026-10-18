using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

internal sealed class HarperBridge
{
    sealed class HarperFile { public HarperLint[] lints { get; set; } }
    sealed class HarperLint
    {
        public HarperSpan span { get; set; }
        public string[] suggestions { get; set; }
    }
    sealed class HarperSpan { public int char_start { get; set; } public int char_end { get; set; } }

    readonly string executable;
    public bool Available { get { return executable != null; } }

    public HarperBridge(string applicationFolder)
    {
        string local = Path.Combine(applicationFolder, "engines", "harper-cli.exe");
        if (File.Exists(local)) { executable = local; return; }
        foreach (string folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            if (string.IsNullOrWhiteSpace(folder)) continue;
            try
            {
                string candidate = Path.Combine(folder.Trim('"'), "harper-cli.exe");
                if (File.Exists(candidate)) { executable = candidate; return; }
            }
            catch { }
        }
    }

    static int Utf16Offset(string text, int characterOffset)
    {
        int index = 0;
        for (int characters = 0; characters < characterOffset && index < text.Length; characters++)
            index += Char.IsHighSurrogate(text[index]) && index + 1 < text.Length && Char.IsLowSurrogate(text[index + 1]) ? 2 : 1;
        return index;
    }

    internal static List<Issue> ParseJson(string text, string json)
    {
        var found = new List<Issue>();
        var files = new JavaScriptSerializer().Deserialize<HarperFile[]>(json);
        foreach (var file in files ?? new HarperFile[0])
            foreach (var lint in file.lints ?? new HarperLint[0])
            {
                if (lint.span == null || lint.span.char_start < 0 || lint.span.char_end <= lint.span.char_start) continue;
                int start = Utf16Offset(text, lint.span.char_start), end = Utf16Offset(text, lint.span.char_end);
                if (start >= text.Length || end > text.Length || end <= start) continue;
                var suggestions = (lint.suggestions ?? new string[0]).Where(x => !string.IsNullOrWhiteSpace(x)).Take(3).ToArray();
                if (suggestions.Length == 0) continue;
                found.Add(new Issue { Start = start, Length = end - start, Word = text.Substring(start, end - start), Suggestions = suggestions });
            }
        return found.OrderBy(x => x.Start).Take(5).ToList();
    }

    public List<Issue> Check(string text)
    {
        if (!Available || String.IsNullOrWhiteSpace(text) || text.Length > 2000) return new List<Issue>();
        var start = new ProcessStartInfo(executable, "lint --format json --quiet") {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8
        };
        using (var process = Process.Start(start))
        {
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            process.StandardInput.Write(text);
            process.StandardInput.Close();
            if (!process.WaitForExit(4000)) { process.Kill(); return new List<Issue>(); }
            if (process.ExitCode != 0 && String.IsNullOrWhiteSpace(output.Result)) return new List<Issue>();
            return ParseJson(text, output.Result);
        }
    }
}
