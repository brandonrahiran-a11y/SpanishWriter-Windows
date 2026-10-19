using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using System.Windows.Forms;

internal class Issue
{
    public int Start, Length;
    public string Word;
    public string[] Suggestions;
    public TextPatternRange Range;
}

internal sealed class MarkForm : Form
{
    public Issue Issue;
    public Action<Issue> Clicked;
    UnderlineForm line;
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams
    {
        get { var p = base.CreateParams; p.ExStyle |= 0x08000080; return p; }
    }
    public MarkForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Black;
        Opacity = 0.01;
        Cursor = Cursors.Hand;
        Height = 22;
        Click += (s, e) => { if (Clicked != null) Clicked(Issue); };
        MouseEnter += (s, e) => { if (Clicked != null) Clicked(Issue); };
    }
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        line = new UnderlineForm { Bounds = Bounds, Cursor = Cursors.Hand };
        line.Click += (s, args) => { if (Clicked != null) Clicked(Issue); };
        line.MouseEnter += (s, args) => { if (Clicked != null) Clicked(Issue); };
        line.Show();
    }
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (line != null) { line.Close(); line.Dispose(); line = null; }
        base.OnFormClosed(e);
    }
}

internal sealed class UnderlineForm : Form
{
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams
    {
        get { var p = base.CreateParams; p.ExStyle |= 0x08000080; return p; }
    }
    public UnderlineForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using (var pen = new Pen(Color.FromArgb(0, 170, 224), 2f))
            for (int x = 0; x < Width; x += 6)
                e.Graphics.DrawLines(pen, new Point[] { new Point(x, 3), new Point(Math.Min(Width, x + 3), 6), new Point(Math.Min(Width, x + 6), 3) });
    }
}

internal sealed class Writer : Form
{
    const string AppVersion = "2.7";
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, INPUT[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT
    {
        public ushort Key;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr Extra;
    }
    [StructLayout(LayoutKind.Explicit, Size = 40)] struct INPUT
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public KEYBDINPUT Keyboard;
    }

    readonly Timer timer = new Timer();
    readonly NotifyIcon tray = new NotifyIcon();
    readonly Label status = new Label();
    readonly Label heading = new Label();
    readonly Button closeButton = new Button();
    readonly FlowLayoutPanel rows = new FlowLayoutPanel();
    readonly List<MarkForm> marks = new List<MarkForm>();
    readonly WindowsSpeller speller = new WindowsSpeller();
    readonly HarperBridge harper = new HarperBridge(AppDomain.CurrentDomain.BaseDirectory);
    readonly WpsAddinServer wpsServer;
    readonly System.Threading.EventWaitHandle activation;
    bool englishMode;
    bool statusOpen;
    DateTime statusUntilUtc;
    AutomationElement target;
    ValuePattern editable;
    TextPattern textPattern;
    TextPatternRange activeTextRange;
    AutomationElement textOwner;
    int rangeOffset;
    IntPtr targetWindow;
    bool targetConsole;
    string checkedText = "", lastSeen = "", checkedFingerprint = "";
    string lastError = "";
    bool consoleLogged;
    bool exactPositionLogged;
    bool markPositionLogged;
    bool consoleSampleLogged;
    bool officeTreeLogged;
    bool accentSampleLogged;
    DateTime lastConsoleProbeAt = DateTime.MinValue;
    DateTime lastForegroundProbeAt = DateTime.MinValue;
    DateTime changedAt = DateTime.MinValue;
    bool active = true;

    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams
    {
        get { var p = base.CreateParams; p.ExStyle |= 0x08000080; p.ClassStyle |= 0x00020000; return p; }
    }

    public Writer(System.Threading.EventWaitHandle activationEvent)
    {
        activation = activationEvent;
        try { wpsServer = new WpsAddinServer(AppDomain.CurrentDomain.BaseDirectory); }
        catch (Exception ex) { try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "SpanishWriter.log"), DateTime.Now.ToString("s") + " Servidor WPS no disponible: " + ex.Message + Environment.NewLine); } catch { } }
        Text = "Corrector español " + AppVersion;
        Size = new Size(540, 132);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        Font = new Font("Segoe UI", 9);
        BackColor = Color.FromArgb(247, 250, 252);
        Opacity = 0.94;
        heading.Text = "Sugerencias";
        heading.Font = new Font("Segoe UI", 11, FontStyle.Bold);
        heading.ForeColor = Color.FromArgb(28, 49, 64);
        heading.Bounds = new Rectangle(18, 10, 465, 25);
        closeButton.Text = "×";
        closeButton.Font = new Font("Segoe UI", 12);
        closeButton.FlatStyle = FlatStyle.Flat;
        closeButton.FlatAppearance.BorderSize = 0;
        closeButton.BackColor = Color.FromArgb(247, 250, 252);
        closeButton.ForeColor = Color.FromArgb(90, 111, 123);
        closeButton.Bounds = new Rectangle(498, 7, 28, 28);
        closeButton.Click += (s, e) => { statusOpen = false; Hide(); };
        status.Bounds = new Rectangle(18, 38, 502, 22);
        status.ForeColor = Color.FromArgb(96, 112, 123);
        status.Text = "Elige una corrección";
        rows.Bounds = new Rectangle(16, 67, 508, 54);
        rows.FlowDirection = FlowDirection.LeftToRight;
        rows.WrapContents = false;
        rows.AutoScroll = true;
        Controls.AddRange(new Control[] { heading, closeButton, status, rows });
        Resize += (s, e) => { using (var path = Rounded(Bounds.Size, 14)) Region = new Region(path); };
        using (var path = Rounded(Size, 14)) Region = new Region(path);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Corrector español " + AppVersion).Enabled = false;
        menu.Items.Add("Mostrar panel", null, (s, e) => ShowStatus());
        var spanishItem = (ToolStripMenuItem)menu.Items.Add("Idioma: español", null, (s, e) => SelectEnglish(false));
        var englishItem = (ToolStripMenuItem)menu.Items.Add("Idioma: inglés (Harper)", null, (s, e) => SelectEnglish(true));
        spanishItem.Click += (s, e) => { spanishItem.Checked = true; englishItem.Checked = false; };
        englishItem.Click += (s, e) => { if (englishMode) { spanishItem.Checked = false; englishItem.Checked = true; } };
        spanishItem.Checked = true;
        menu.Items.Add("Pausar / reanudar", null, (s, e) => { active = !active; tray.Text = active ? "Corrector español " + AppVersion + ": activo" : "Corrector español " + AppVersion + ": pausado"; if (!active) { HideMarks(); Hide(); } });
        menu.Items.Add("Salir", null, (s, e) => Close());
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        tray.Icon = Icon;
        tray.ContextMenuStrip = menu;
        tray.Text = "Corrector español " + AppVersion + ": activo";
        tray.Visible = true;
        tray.DoubleClick += (s, e) => ShowStatus();
        timer.Interval = 500;
        timer.Tick += (s, e) => {
            if (activation.WaitOne(0)) { ShowStatus(); return; }
            Poll(s, e);
        };
        timer.Start();
        Shown += (s, e) => ShowStatus();
    }

    void ShowStatus()
    {
        HideMarks();
        rows.Controls.Clear();
        heading.Text = "Corrector español " + AppVersion;
        status.Text = active ? "Activo. Revisa los campos de texto compatibles." : "Pausado. Reanúdalo desde el icono junto al reloj.";
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Bounds = new Rectangle(area.Right - Width - 18, area.Bottom - Height - 18, Width, Height);
        statusOpen = true;
        statusUntilUtc = DateTime.UtcNow.AddSeconds(6);
        Show();
        BringToFront();
    }

    static GraphicsPath Rounded(Size size, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(0, 0, d, d, 180, 90);
        path.AddArc(size.Width - d - 1, 0, d, d, 270, 90);
        path.AddArc(size.Width - d - 1, size.Height - d - 1, d, d, 0, 90);
        path.AddArc(0, size.Height - d - 1, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using (var pen = new Pen(Color.FromArgb(223, 235, 242)))
        using (var path = Rounded(ClientSize, 14))
            e.Graphics.DrawPath(pen, path);
    }

    List<Issue> Check(string text)
    {
        if (englishMode) return harper.Check(text);
        var native = speller.Check(text);
        var accents = AccentRules.Check(text);
        foreach (var accent in accents)
        {
            native.RemoveAll(x => x.Start < accent.Start + accent.Length && accent.Start < x.Start + x.Length);
            native.Add(accent);
        }
        return native.OrderBy(x => x.Start).Take(5).ToList();
    }

    void SelectEnglish(bool enabled)
    {
        if (enabled && !harper.Available)
        {
            MessageBox.Show("Para activar Harper, coloca harper-cli.exe en la carpeta engines junto al corrector o instálalo en PATH. Harper solo revisa inglés.",
                "Motor Harper no instalado");
            return;
        }
        englishMode = enabled;
        tray.Text = enabled ? "Corrector inglés Harper " + AppVersion : "Corrector español " + AppVersion;
        checkedFingerprint = lastSeen = "";
        HideMarks(); Hide();
    }

    void Poll(object sender, EventArgs e)
    {
        if (statusOpen)
        {
            if (DateTime.UtcNow < statusUntilUtc) return;
            statusOpen = false;
            Hide();
        }
        if (!active) return;
        try
        {
            uint pid;
            GetWindowThreadProcessId(GetForegroundWindow(), out pid);
            if (pid == 0) return;
            string process = Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant();
            if ((DateTime.UtcNow - lastForegroundProbeAt).TotalSeconds >= 10)
            {
                lastForegroundProbeAt = DateTime.UtcNow;
                try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "SpanishWriter.log"), DateTime.Now.ToString("s") + " Ventana activa: " + process + Environment.NewLine); } catch { }
            }
            if (process == "spanishwriter" && Visible) return;
            bool console = process == "windowsterminal" || process == "openconsole" || process == "cmd" || process == "conhost" || process == "wt";
            bool office = process == "wps" || process == "et" || process == "wpp" || process == "winword" || process == "excel" || process == "powerpnt" || process == "onenote" || process == "outlook";
            // Consultar cualquier aplicación que exponga texto editable a UI Automation.
            // La posición y el contenido de cada palabra se vuelven a verificar antes de marcarla.
            var focus = AutomationElement.FocusedElement;
            if (focus == null || focus.Current.IsPassword) { HideMarks(); Hide(); return; }
            object pattern;
            ValuePattern vp = null;
            TextPattern tp = null;
            AutomationElement owner = null;
            string text = null;
            if (!console && focus.TryGetCurrentPattern(ValuePattern.Pattern, out pattern)) { vp = (ValuePattern)pattern; text = vp.Current.Value; }
            var textElement = focus;
            for (int depth = 0; depth < 5 && textElement != null; depth++)
            {
                if (textElement.TryGetCurrentPattern(TextPattern.Pattern, out pattern)) { tp = (TextPattern)pattern; owner = textElement; break; }
                textElement = TreeWalker.ControlViewWalker.GetParent(textElement);
            }
            if (console && !consoleLogged)
            {
                consoleLogged = true;
                try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "SpanishWriter.log"), DateTime.Now.ToString("s") + " Consola: proceso=" + process + ", control=" + focus.Current.ControlType.ProgrammaticName + ", texto=" + (tp != null) + Environment.NewLine); } catch { }
            }
            int offset = 0;
            TextPatternRange lineRange = null;
            if (console && tp != null)
            {
                try
                {
                    var selection = tp.GetSelection();
                    if (selection != null && selection.Length == 1 && selection[0].GetText(-1).Length == 0)
                    {
                        lineRange = selection[0].Clone();
                        lineRange.ExpandToEnclosingUnit(TextUnit.Line);
                        string whole = lineRange.GetText(2000).TrimEnd('\r', '\n', '\0');
                        int prompt = whole.LastIndexOf('>');
                        if (lineRange.GetBoundingRectangles().Length == 1 && prompt >= 0 && prompt < whole.Length - 1)
                        {
                            offset = prompt + 1;
                            text = whole.Substring(offset);
                        }
                    }
                }
                catch { lineRange = null; }
                if (text == null)
                {
                    try
                    {
                        var visible = tp.GetVisibleRanges();
                        if (visible != null && visible.Length > 0)
                        {
                            var candidate = visible[visible.Length - 1].Clone();
                            candidate.MoveEndpointByRange(TextPatternRangeEndpoint.Start, candidate, TextPatternRangeEndpoint.End);
                            candidate.ExpandToEnclosingUnit(TextUnit.Line);
                            string whole = candidate.GetText(2000).TrimEnd('\r', '\n', '\0');
                            if (candidate.GetBoundingRectangles().Length == 1)
                            {
                                int prompt = whole.LastIndexOf('>');
                                if (prompt >= 0 && prompt < whole.Length - 1)
                                {
                                    lineRange = candidate;
                                    offset = prompt + 1;
                                    text = whole.Substring(offset);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            if (office && text == null && tp != null)
            {
                try
                {
                    var selection = tp.GetSelection();
                    if (selection != null && selection.Length == 1 && selection[0].GetText(-1).Length == 0)
                    {
                        lineRange = selection[0].Clone();
                        lineRange.ExpandToEnclosingUnit(TextUnit.Paragraph);
                        string paragraph = lineRange.GetText(4001).TrimEnd('\r', '\n', '\0');
                        if (paragraph.Length <= 4000) text = paragraph;
                    }
                }
                catch { lineRange = null; }
            }
            if (process == "wps" && !officeTreeLogged)
            {
                officeTreeLogged = true;
                LogOfficeTree(focus);
            }
            if (office && (DateTime.UtcNow - lastConsoleProbeAt).TotalSeconds >= 5)
            {
                lastConsoleProbeAt = DateTime.UtcNow;
                try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "SpanishWriter.log"), DateTime.Now.ToString("s") +
                    " Office lectura: proceso=" + process + ", control=" + focus.Current.ControlType.ProgrammaticName +
                    ", valor=" + (vp != null) + ", texto=" + (tp != null) + ", longitud=" + (text == null ? -1 : text.Length) + Environment.NewLine); } catch { }
            }
            if (!console && !office && text == null && tp != null) text = tp.DocumentRange.GetText(4000);
            if (console && (DateTime.UtcNow - lastConsoleProbeAt).TotalSeconds >= 5)
            {
                lastConsoleProbeAt = DateTime.UtcNow;
                try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "SpanishWriter.log"), DateTime.Now.ToString("s") +
                    " Consola lectura: longitud=" + (text == null ? -1 : text.Length) +
                    ", echo=" + (text != null && text.IndexOf("echo", StringComparison.OrdinalIgnoreCase) >= 0) +
                    ", prueva=" + (text != null && text.IndexOf("prueva", StringComparison.OrdinalIgnoreCase) >= 0) +
                    ", control=" + focus.Current.ControlType.ProgrammaticName + Environment.NewLine); } catch { }
            }
            if (String.IsNullOrWhiteSpace(text) || text.Length > 4000) { HideMarks(); Hide(); return; }
            string fingerprint = pid + ":" + focus.Current.AutomationId + ":" + text;
            if (fingerprint != lastSeen) { lastSeen = fingerprint; changedAt = DateTime.UtcNow; HideMarks(); Hide(); return; }
            if ((DateTime.UtcNow - changedAt).TotalMilliseconds < 450) return;
            if (checkedFingerprint == fingerprint) return;
            target = focus;
            editable = vp;
            textPattern = tp;
            activeTextRange = (console || office) ? lineRange : null;
            textOwner = owner;
            rangeOffset = offset;
            targetWindow = GetForegroundWindow();
            targetConsole = console;
            checkedText = text;
            checkedFingerprint = fingerprint;
            var issues = Check(text);
            if (console && !consoleSampleLogged)
            {
                consoleSampleLogged = true;
                try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "SpanishWriter.log"), DateTime.Now.ToString("s") +
                    " Muestra consola: longitud=" + text.Length + ", contiene_prueva=" + text.Contains("prueva") +
                    ", errores=" + issues.Count + ", rect=" + focus.Current.BoundingRectangle + Environment.NewLine); } catch { }
            }
            if (!console && text.IndexOf("Como estas", StringComparison.OrdinalIgnoreCase) >= 0 && !accentSampleLogged)
            {
                accentSampleLogged = true;
                try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "SpanishWriter.log"), DateTime.Now.ToString("s") +
                    " Muestra tildes: longitud=" + text.Length + ", errores=" +
                    string.Join(",", issues.Select(x => x.Word).ToArray()) +
                    ", apertura=" + text.Contains("¿") + ", cierre=" + text.Contains("?") +
                    ", control=" + focus.Current.ControlType.ProgrammaticName + Environment.NewLine); } catch { }
            }
            ShowMarks(issues, focus, console);
        }
        catch (Exception ex)
        {
            if (ex.ToString() != lastError)
            {
                lastError = ex.ToString();
                try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "SpanishWriter.log"), DateTime.Now.ToString("s") + " Error de revisión: " + ex + Environment.NewLine); } catch { }
            }
            HideMarks();
            Hide();
        }
    }

    void HideMarks()
    {
        foreach (var mark in marks) { mark.Close(); mark.Dispose(); }
        marks.Clear();
    }

    static void LogOfficeTree(AutomationElement focus)
    {
        try
        {
            var lines = new List<string>();
            var pending = new Queue<Tuple<AutomationElement, int>>();
            pending.Enqueue(Tuple.Create(focus, 0));
            var parent = TreeWalker.ControlViewWalker.GetParent(focus);
            if (parent != null) pending.Enqueue(Tuple.Create(parent, 0));
            while (pending.Count > 0 && lines.Count < 120)
            {
                var item = pending.Dequeue();
                try
                {
                    object value, text;
                    bool hasValue = item.Item1.TryGetCurrentPattern(ValuePattern.Pattern, out value);
                    bool hasText = item.Item1.TryGetCurrentPattern(TextPattern.Pattern, out text);
                    int length = hasValue ? ((ValuePattern)value).Current.Value.Length : -1;
                    lines.Add(item.Item2 + ":" + item.Item1.Current.ControlType.ProgrammaticName +
                        ":value=" + length + ":text=" + hasText);
                    if (item.Item2 >= 5) continue;
                    var child = TreeWalker.RawViewWalker.GetFirstChild(item.Item1);
                    for (int n = 0; child != null && n < 25; n++)
                    {
                        pending.Enqueue(Tuple.Create(child, item.Item2 + 1));
                        child = TreeWalker.RawViewWalker.GetNextSibling(child);
                    }
                }
                catch { }
            }
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "SpanishWriter.log"),
                DateTime.Now.ToString("s") + " WPS árbol UIA (sin contenido): " + string.Join(" | ", lines.ToArray()) + Environment.NewLine);
        }
        catch { }
    }

    void ShowMarks(List<Issue> issues, AutomationElement focus, bool console)
    {
        HideMarks();
        if (issues.Count == 0) return;
        var field = focus.Current.BoundingRectangle;
        foreach (var issue in issues)
        {
            Rectangle bounds;
            try
            {
                var range = IssueRange(issue);
                if (range == null) throw new Exception("No hay rango exacto.");
                var rectangles = range.GetBoundingRectangles();
                if (rectangles.Length == 0 || rectangles[0].Width <= 0) throw new Exception("No hay coordenadas para el rango.");
                issue.Range = range;
                int hitWidth = Math.Max(60, (int)rectangles[0].Width + 16);
                bounds = new Rectangle((int)rectangles[0].Left - (hitWidth - (int)rectangles[0].Width) / 2,
                    (int)rectangles[0].Bottom - 1, hitWidth, 22);
                if (!markPositionLogged)
                {
                    markPositionLogged = true;
                    try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "SpanishWriter.log"), DateTime.Now.ToString("s") +
                        " Marca: control=" + focus.Current.ControlType.ProgrammaticName +
                        ", campo=" + field + ", palabra=" + rectangles[0] +
                        ", linea=" + bounds + Environment.NewLine); } catch { }
                }
            }
            catch
            {
                if (!exactPositionLogged)
                {
                    exactPositionLogged = true;
                    try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "SpanishWriter.log"), DateTime.Now.ToString("s") + " Campo sin posición exacta: " + focus.Current.ControlType.ProgrammaticName + ", TextPattern=" + (textPattern != null) + Environment.NewLine); } catch { }
                }
                continue;
            }
            var screen = Screen.FromPoint(bounds.Location).WorkingArea;
            bounds.X = Math.Max(screen.Left, Math.Min(screen.Right - bounds.Width, bounds.X));
            bounds.Y = Math.Max(screen.Top, Math.Min(screen.Bottom - bounds.Height, bounds.Y));
            var mark = new MarkForm { Issue = issue, Bounds = bounds };
            mark.Clicked = selected => ShowSuggestions(selected, bounds);
            marks.Add(mark);
            mark.Show();
        }
    }

    void ShowSuggestions(Issue issue, Rectangle mark)
    {
        statusOpen = false;
        rows.Controls.Clear();
        heading.Text = "Revisar «" + issue.Word + "»";
        status.Text = editable == null ? "Elige una sugerencia" : "Pulsa para corregir en el texto";
        foreach (string candidate in issue.Suggestions)
        {
            string replacement = candidate;
            var button = new Button { Text = replacement, AutoSize = true, Height = 34, Margin = new Padding(0, 0, 8, 0), Padding = new Padding(10, 0, 10, 0), BackColor = Color.FromArgb(230, 247, 253), ForeColor = Color.FromArgb(0, 101, 137), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            button.FlatAppearance.BorderColor = Color.FromArgb(175, 225, 242);
            button.Click += (s, e) => Apply(issue, replacement);
            rows.Controls.Add(button);
        }
        if (issue.Suggestions.Length == 0)
            rows.Controls.Add(new Label { Text = "No hay sugerencias para esta palabra.", AutoSize = true, ForeColor = Color.FromArgb(96, 112, 123) });
        var screen = Screen.FromPoint(mark.Location).WorkingArea;
        Left = Math.Max(screen.Left, Math.Min(screen.Right - Width, mark.Left));
        Top = mark.Bottom + Height + 6 > screen.Bottom ? Math.Max(screen.Top, mark.Top - Height - 6) : mark.Bottom + 6;
        if (!Visible) Show();
    }

    void Apply(Issue issue, string replacement)
    {
        try
        {
            if (targetConsole)
            {
                try
                {
                    var word = issue.Range ?? IssueRange(issue);
                    if (word == null) throw new Exception("No hay rango exacto de la palabra.");
                    if (!checkedText.EndsWith(issue.Word, StringComparison.Ordinal))
                        throw new Exception("La palabra no está al final del comando.");
                    var caret = textPattern.GetSelection();
                    if (caret == null || caret.Length != 1 || caret[0].GetText(-1).Length != 0 ||
                        caret[0].CompareEndpoints(TextPatternRangeEndpoint.Start, word, TextPatternRangeEndpoint.End) != 0)
                        throw new Exception("No se confirmó el cursor después de la palabra.");
                    if (!SetForegroundWindow(targetWindow)) throw new Exception("No se pudo activar la consola.");
                    for (int n = 0; n < issue.Length; n++) PressKey(0x08); // Backspace.
                    TypeUnicode(replacement);
                    checkedText = checkedFingerprint = "";
                    HideMarks(); Hide();
                }
                catch (Exception ex)
                {
                    Clipboard.SetText(replacement);
                    status.Text = "La consola no admitió el reemplazo. Sugerencia copiada.";
                    try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "SpanishWriter.log"), DateTime.Now.ToString("s") + " Consola: reemplazo fallido: " + ex.Message + Environment.NewLine); } catch { }
                }
                return;
            }
            bool applied = false;
            if (textPattern != null)
            {
                try
                {
                    var range = issue.Range ?? IssueRange(issue);
                    if (range != null)
                    {
                        SetForegroundWindow(targetWindow);
                        target.SetFocus();
                        range.Select();
                        TypeUnicode(replacement);
                        applied = true;
                    }
                }
                catch (Exception ex) { try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "SpanishWriter.log"), DateTime.Now.ToString("s") + " Selección de texto no disponible: " + ex + Environment.NewLine); } catch { } }
            }
            if (!applied && editable != null && !editable.Current.IsReadOnly)
            {
                string live = editable.Current.Value;
                if (live == checkedText && issue.Start + issue.Length <= live.Length)
                {
                    editable.SetValue(live.Substring(0, issue.Start) + replacement + live.Substring(issue.Start + issue.Length));
                    applied = true;
                }
            }
            if (!applied) { Clipboard.SetText(replacement); status.Text = "Este campo no admite reemplazo directo. Sugerencia copiada."; return; }
            checkedText = "";
            checkedFingerprint = "";
            HideMarks();
            Hide();
        }
        catch (Exception ex) { status.Text = "No se pudo modificar este campo."; try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "SpanishWriter.log"), DateTime.Now.ToString("s") + " Error al aplicar: " + ex + Environment.NewLine); } catch { } }
    }

    TextPatternRange IssueRange(Issue issue)
    {
        if (textPattern == null) return null;
        if (activeTextRange != null)
        {
            try
            {
                var line = activeTextRange.Clone();
                line.MoveEndpointByRange(TextPatternRangeEndpoint.End, line, TextPatternRangeEndpoint.Start);
                line.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, rangeOffset + issue.Start + issue.Length);
                line.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, rangeOffset + issue.Start);
                var activeField = (textOwner ?? target).Current.BoundingRectangle;
                if (line.GetText(-1) == issue.Word && InField(line, activeField)) return line;
            }
            catch { }
            if (targetConsole) return null;
        }
        else if (targetConsole) return null;
        var document = textPattern.DocumentRange;
        var field = target.Current.BoundingRectangle;
        if (target.Current.ControlType == ControlType.Group)
        {
            try
            {
                var edits = target.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
                System.Windows.Rect candidate = System.Windows.Rect.Empty;
                for (int i = 0; i < edits.Count && i < 30; i++)
                {
                    object valueObject;
                    if (!edits[i].TryGetCurrentPattern(ValuePattern.Pattern, out valueObject)) continue;
                    string value = ((ValuePattern)valueObject).Current.Value;
                    if (value != checkedText) continue;
                    var box = edits[i].Current.BoundingRectangle;
                    if (box.Width <= 0 || box.Height <= 0) continue;
                    candidate = box;
                    break;
                }
                if (candidate.IsEmpty) return null;
                field = candidate;
            }
            catch { return null; }
        }
        try
        {
            var selection = textPattern.GetSelection();
            if (selection != null && selection.Length == 1 && selection[0].GetText(-1).Length == 0)
            {
                var atCaret = selection[0].Clone();
                int after = checkedText.Length - issue.Start - issue.Length;
                if (after >= 0)
                {
                    atCaret.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, -(after + issue.Length));
                    atCaret.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, -after);
                    if (atCaret.GetText(-1) == issue.Word && InField(atCaret, field)) return atCaret;
                }
            }
        }
        catch { }
        // DocumentRange may include the entire web page. Use its offsets only
        // when the focused field itself owns TextPattern.
        object ownPattern;
        if (!target.TryGetCurrentPattern(TextPattern.Pattern, out ownPattern)) return null;
        try
        {
            var range = document.Clone();
            range.MoveEndpointByRange(TextPatternRangeEndpoint.End, range, TextPatternRangeEndpoint.Start);
            range.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, rangeOffset + issue.Start + issue.Length);
            range.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, rangeOffset + issue.Start);
            if (range.GetText(-1).TrimEnd('\r', '\n') == issue.Word && InField(range, field)) return range;
        }
        catch { }
        var search = document.Clone();
        for (int n = 0; n < 50; n++)
        {
            var found = search.FindText(issue.Word, false, false);
            if (found == null) break;
            if (InField(found, field)) return found;
            search.MoveEndpointByRange(TextPatternRangeEndpoint.Start, found, TextPatternRangeEndpoint.End);
        }
        return null;
    }

    static bool InField(TextPatternRange range, System.Windows.Rect field)
    {
        foreach (var rect in range.GetBoundingRectangles())
            if (rect.Width > 0 && rect.Height > 0 &&
                rect.Left >= field.Left - 4 && rect.Right <= field.Right + 4 &&
                rect.Top >= field.Top - 4 && rect.Bottom <= field.Bottom + 4) return true;
        return false;
    }

    static void TypeUnicode(string value)
    {
        var inputs = new List<INPUT>();
        foreach (char c in value)
        {
            inputs.Add(new INPUT { Type = 1, Keyboard = new KEYBDINPUT { Scan = c, Flags = 0x0004 } });
            inputs.Add(new INPUT { Type = 1, Keyboard = new KEYBDINPUT { Scan = c, Flags = 0x0006 } });
        }
        if (SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT))) != inputs.Count)
            throw new Exception("Windows no aceptó la escritura de la corrección.");
    }

    static void PressKey(ushort key)
    {
        var inputs = new[] {
            new INPUT { Type = 1, Keyboard = new KEYBDINPUT { Key = key } },
            new INPUT { Type = 1, Keyboard = new KEYBDINPUT { Key = key, Flags = 0x0002 } }
        };
        if (SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT))) != 2)
            throw new Exception("Windows no aceptó una tecla de edición.");
    }


    public string SelfTest()
    {
        var errors = Check("Esto es una prueva de ortografia.");
        var accents = Check("El camión llegó con música y café.");
        return "Versión " + AppVersion + "; idioma: " + speller.Language + "; errores: " + string.Join(", ", errors.Select(i => i.Word).ToArray()) + "; tildes marcadas: " + string.Join(", ", accents.Select(i => i.Word).ToArray());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Dispose(); if (wpsServer != null) wpsServer.Dispose(); tray.Visible = false; tray.Dispose(); HideMarks(); }
        base.Dispose(disposing);
    }

    [STAThread]
    static void Main(string[] args)
    {
        string log = Path.Combine(Path.GetTempPath(), "SpanishWriter.log");
        Action<string> record = message => { try { File.AppendAllText(log, DateTime.Now.ToString("s") + " " + message + Environment.NewLine); } catch { } };
        record("Inicio");
        AppDomain.CurrentDomain.UnhandledException += (s, e) => record("Excepción no controlada: " + e.ExceptionObject);
        Application.ThreadException += (s, e) => { record("Excepción de interfaz: " + e.Exception); MessageBox.Show(e.Exception.Message + "\n\nRegistro: " + log, "Corrector español"); };
        try
        {
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool first;
            using (var activation = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, "Local\\SpanishWriter-Show-6DC68A97"))
            using (var mutex = new System.Threading.Mutex(true, "Local\\SpanishWriter-6DC68A97", out first))
            {
                if (!first)
                {
                    record("Ya existe una instancia.");
                    if (args.Length == 0) activation.Set();
                    return;
                }
                using (var app = new Writer(activation))
                {
                    record("Aplicación inicializada. " + app.SelfTest());
                    if (args.Length > 0 && args[0] == "--self-test") { Console.WriteLine(app.SelfTest()); return; }
                    Application.Run(app);
                }
            }
        }
        catch (Exception ex)
        {
            record("Error: " + ex);
            if (args.Length > 0 && args[0] == "--self-test") Console.Error.WriteLine(ex.Message);
            else MessageBox.Show(ex.Message + "\n\nRegistro: " + log, "Corrector español");
        }
    }
}
