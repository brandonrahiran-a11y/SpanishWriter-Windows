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
    public string Kind = "Ortografía";
}

internal static class Theme
{
    public static float Scale = 1f;
    public static int S(int pixels) { return (int)Math.Round(pixels * Scale); }
    public static readonly Color Background = Color.FromArgb(252, 253, 254);
    public static readonly Color Border = Color.FromArgb(218, 228, 236);
    public static readonly Color Text = Color.FromArgb(24, 39, 52);
    public static readonly Color Muted = Color.FromArgb(98, 114, 128);
    public static readonly Color Accent = Color.FromArgb(0, 112, 168);
    // Un solo color para subrayados y etiquetas: el azul original del corrector.
    public static readonly Color Mark = Color.FromArgb(0, 170, 224);


    public static Color Shade(Color color, int amount)
    {
        return Color.FromArgb(color.A, Math.Max(0, color.R - amount), Math.Max(0, color.G - amount), Math.Max(0, color.B - amount));
    }

    public static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int d = Math.Max(2, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

// Botón dibujado a mano: no toma el foco, así el campo de texto conserva el cursor.
internal sealed class Chip : Control
{
    public enum Look { Primary, Secondary, Ghost }
    public Look Style = Look.Secondary;
    public Color Accent = Theme.Accent;
    bool hover, pressed;

    public Chip()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        Cursor = Cursors.Hand;
    }

    public Chip Fit()
    {
        var text = TextRenderer.MeasureText(Text, Font);
        Size = new Size(text.Width + Theme.S(Style == Look.Ghost ? 16 : 26), Theme.S(Style == Look.Ghost ? 28 : 34));
        return this;
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent != null ? Parent.BackColor : Theme.Background);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color fill, border, text;
        if (Style == Look.Primary) { fill = hover ? Theme.Shade(Accent, 22) : Accent; border = fill; text = Color.White; }
        else if (Style == Look.Secondary) { fill = hover ? Color.FromArgb(226, 241, 250) : Color.FromArgb(241, 247, 251); border = Color.FromArgb(200, 222, 236); text = Theme.Shade(Accent, 14); }
        else { fill = hover ? Color.FromArgb(236, 243, 248) : Theme.Background; border = fill; text = Accent; }
        if (pressed) { fill = Theme.Shade(fill, 18); border = Theme.Shade(border, 18); }
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = Theme.Rounded(bounds, Style == Look.Ghost ? Theme.S(6) : Height / 2))
        using (var brush = new SolidBrush(fill))
        using (var pen = new Pen(border))
        {
            g.FillPath(brush, path);
            g.DrawPath(pen, path);
        }
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, text, fill,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }
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
        Height = Theme.S(22);
        Click += (s, e) => { if (Clicked != null) Clicked(Issue); };
        MouseEnter += (s, e) => { if (Clicked != null) Clicked(Issue); };
    }
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        line = new UnderlineForm { Bounds = Bounds, Cursor = Cursors.Hand, LineColor = Theme.Mark };
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
    public Color LineColor = Theme.Mark;
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
        // Sin suavizado: TransparencyKey dejaría bordes rosados con antialiasing.
        int step = Math.Max(4, Theme.S(4)), top = Theme.S(2), bottom = top + Math.Max(3, Theme.S(3));
        using (var pen = new Pen(LineColor, Math.Max(2f, Theme.Scale * 1.6f)))
            for (int x = 0; x < Width; x += step * 2)
                e.Graphics.DrawLines(pen, new Point[] { new Point(x, top), new Point(Math.Min(Width, x + step), bottom), new Point(Math.Min(Width, x + step * 2), top) });
    }
}

internal sealed class Writer : Form
{
    const string AppVersion = "3.0";
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
    readonly Label kindLabel = new Label();
    readonly Label status = new Label();
    readonly Label heading = new Label();
    readonly Chip closeButton = new Chip();
    readonly FlowLayoutPanel rows = new FlowLayoutPanel();
    readonly FlowLayoutPanel actions = new FlowLayoutPanel();
    readonly Font chipFont = new Font("Segoe UI Semibold", 10f);
    readonly List<MarkForm> marks = new List<MarkForm>();
    readonly CustomDictionary dictionary = new CustomDictionary();
    readonly HashSet<string> ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    readonly WindowsSpeller speller;
    DateTime dictionaryStamp = DateTime.MinValue;
    DateTime lastDictionaryProbeAt = DateTime.MinValue;
    int separatorY = -1;
    ToolStripMenuItem pauseItem;
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
        try { wpsServer = new WpsAddinServer(AppDomain.CurrentDomain.BaseDirectory, SuggestForWps); }
        catch (Exception ex) { try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "SpanishWriter.log"), DateTime.Now.ToString("s") + " Servidor WPS no disponible: " + ex.Message + Environment.NewLine); } catch { } }
        speller = new WindowsSpeller(dictionary);
        dictionaryStamp = DictionaryStamp();
        using (var g = CreateGraphics()) Theme.Scale = Math.Max(1f, g.DpiX / 96f);
        Text = "Corrector español " + AppVersion;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Theme.Background;
        kindLabel.Font = new Font("Segoe UI Semibold", 7.5f);
        kindLabel.AutoSize = false;
        heading.Font = new Font("Segoe UI Semibold", 12.5f);
        heading.ForeColor = Theme.Text;
        heading.AutoEllipsis = true;
        status.ForeColor = Theme.Muted;
        status.AutoEllipsis = true;
        closeButton.Text = "✕";
        closeButton.Style = Chip.Look.Ghost;
        closeButton.Accent = Theme.Muted;
        closeButton.Font = new Font("Segoe UI", 9f);
        closeButton.Click += (s, e) => { statusOpen = false; Hide(); };
        foreach (var panel in new[] { rows, actions })
        {
            panel.FlowDirection = FlowDirection.LeftToRight;
            panel.WrapContents = true;
            panel.AutoScroll = false;
            panel.Margin = Padding.Empty;
            panel.Padding = Padding.Empty;
        }
        Controls.AddRange(new Control[] { kindLabel, heading, closeButton, status, rows, actions });
        Resize += (s, e) => { using (var path = Theme.Rounded(new Rectangle(0, 0, Width, Height), Theme.S(12))) Region = new Region(path); Invalidate(); };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Corrector español " + AppVersion).Enabled = false;
        menu.Items.Add("Mostrar panel", null, (s, e) => ShowStatus());
        menu.Items.Add("Diccionario personal…", null, (s, e) => OpenPersonalDictionary());
        menu.Items.Add(new ToolStripSeparator());
        var spanishItem = (ToolStripMenuItem)menu.Items.Add("Idioma: español", null, (s, e) => SelectEnglish(false));
        var englishItem = (ToolStripMenuItem)menu.Items.Add("Idioma: inglés (Harper)", null, (s, e) => SelectEnglish(true));
        spanishItem.Click += (s, e) => { spanishItem.Checked = true; englishItem.Checked = false; };
        englishItem.Click += (s, e) => { if (englishMode) { spanishItem.Checked = false; englishItem.Checked = true; } };
        spanishItem.Checked = true;
        menu.Items.Add(new ToolStripSeparator());
        pauseItem = (ToolStripMenuItem)menu.Items.Add("Pausar", null, (s, e) => TogglePause());
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
        ClearPanel(rows);
        ClearPanel(actions);
        kindLabel.Text = active ? "●  ACTIVO" : "●  PAUSADO";
        kindLabel.ForeColor = Theme.Mark;
        heading.Text = "Corrector español " + AppVersion;
        string engine = englishMode ? "inglés (Harper)" : speller.Language;
        status.Text = active
            ? "Motor " + engine + " · " + dictionary.Count + " palabras extra" + (dictionary.UserCount > 0 ? " (" + dictionary.UserCount + " tuyas)" : "")
            : "Pausado. Reanúdalo aquí o desde el icono junto al reloj.";
        actions.Controls.Add(Action("Diccionario personal", OpenPersonalDictionary));
        actions.Controls.Add(Action(active ? "Pausar" : "Reanudar", () => { TogglePause(); ShowStatus(); }));
        LayoutPanel();
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Bounds = new Rectangle(area.Right - Width - Theme.S(16), area.Bottom - Height - Theme.S(16), Width, Height);
        statusOpen = true;
        statusUntilUtc = DateTime.UtcNow.AddSeconds(6);
        Show();
        BringToFront();
    }

    Chip Action(string text, Action click)
    {
        var chip = new Chip { Text = text, Style = Chip.Look.Ghost, Font = Font, Margin = new Padding(0, 0, Theme.S(4), 0) };
        chip.Click += (s, e) => click();
        return chip.Fit();
    }

    // Calcula la altura del panel según el número de sugerencias y acciones.
    void LayoutPanel()
    {
        int width = Theme.S(380), pad = Theme.S(18), inner = width - pad * 2;
        int y = Theme.S(14);
        closeButton.Size = new Size(Theme.S(28), Theme.S(28));
        closeButton.Location = new Point(width - closeButton.Width - Theme.S(10), Theme.S(10));
        kindLabel.SetBounds(pad, y, inner - Theme.S(30), Theme.S(16));
        y += Theme.S(17);
        heading.SetBounds(pad, y, inner - Theme.S(30), Theme.S(28));
        y += Theme.S(30);
        // Control.Visible devuelve false mientras el panel está oculto: usar variables.
        bool hasStatus = status.Text.Length > 0, hasRows = rows.Controls.Count > 0, hasActions = actions.Controls.Count > 0;
        status.Visible = hasStatus;
        rows.Visible = hasRows;
        actions.Visible = hasActions;
        if (hasStatus) { status.SetBounds(pad, y, inner, Theme.S(20)); y += Theme.S(26); }
        if (hasRows)
        {
            foreach (Control chip in rows.Controls) chip.Margin = new Padding(0, 0, Theme.S(8), Theme.S(8));
            var preferred = rows.GetPreferredSize(new Size(inner + Theme.S(8), 0));
            rows.SetBounds(pad, y, inner + Theme.S(8), preferred.Height);
            y += preferred.Height + Theme.S(2);
        }
        separatorY = -1;
        if (hasActions)
        {
            separatorY = y;
            y += Theme.S(6);
            actions.SetBounds(pad - Theme.S(8), y, inner + Theme.S(16), Theme.S(30));
            y += Theme.S(30);
        }
        Size = new Size(width, y + Theme.S(10));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var pen = new Pen(Theme.Border))
        {
            if (separatorY >= 0) e.Graphics.DrawLine(pen, Theme.S(18), separatorY, Width - Theme.S(18), separatorY);
            using (var path = Theme.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), Theme.S(12)))
                e.Graphics.DrawPath(pen, path);
        }
    }

    void TogglePause()
    {
        active = !active;
        tray.Text = "Corrector español " + AppVersion + (active ? ": activo" : ": pausado");
        pauseItem.Text = active ? "Pausar" : "Reanudar";
        if (!active) { HideMarks(); Hide(); }
        checkedFingerprint = lastSeen = "";
    }

    void OpenPersonalDictionary()
    {
        try
        {
            if (!File.Exists(dictionary.UserFile))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dictionary.UserFile));
                File.WriteAllText(dictionary.UserFile, "# Diccionario personal de SpanishWriter. Una palabra por línea." + Environment.NewLine +
                    "# Guarda el archivo y el corrector lo recargará en unos segundos." + Environment.NewLine, new System.Text.UTF8Encoding(false));
            }
            Process.Start("notepad.exe", "\"" + dictionary.UserFile + "\"");
        }
        catch (Exception ex) { MessageBox.Show("No se pudo abrir el diccionario personal: " + ex.Message, "Corrector español"); }
    }

    DateTime DictionaryStamp()
    {
        try { return File.Exists(dictionary.UserFile) ? File.GetLastWriteTimeUtc(dictionary.UserFile) : DateTime.MinValue; }
        catch { return DateTime.MinValue; }
    }

    // Recarga el diccionario personal si el usuario lo editó a mano.
    void ReloadDictionaryIfChanged()
    {
        if ((DateTime.UtcNow - lastDictionaryProbeAt).TotalSeconds < 3) return;
        lastDictionaryProbeAt = DateTime.UtcNow;
        var stamp = DictionaryStamp();
        if (stamp == dictionaryStamp) return;
        // Si el archivo está bloqueado (por ejemplo, mientras se guarda), se reintenta luego.
        if (!dictionary.Reload()) return;
        dictionaryStamp = stamp;
        checkedFingerprint = lastSeen = "";
    }

    List<Issue> Check(string text)
    {
        if (englishMode) return harper.Check(text);
        var native = speller.Check(text);
        var accents = AccentRules.Check(text);
        foreach (var accent in accents)
        {
            accent.Kind = "Tilde";
            native.RemoveAll(x => x.Start < accent.Start + accent.Length && accent.Start < x.Start + x.Length);
            native.Add(accent);
        }
        return native.Where(x => !ignored.Contains(x.Word)).OrderBy(x => x.Start).Take(WindowsSpeller.MaxIssues).ToList();
    }

    string SuggestForWps(string word)
    {
        if (InvokeRequired) return (string)Invoke(new Func<string, string>(SuggestForWps), new object[] { word });
        if (!active || englishMode || String.IsNullOrWhiteSpace(word)) return "{\"suggestions\":[]}";
        var exact = Check(word).FirstOrDefault(x => x.Start == 0 && x.Length == word.Length);
        return new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(
            new { suggestions = exact == null ? new string[0] : exact.Suggestions });
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
        ReloadDictionaryIfChanged();
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

    // Controls.Clear() no libera los controles: desecharlos evita acumular recursos.
    static void ClearPanel(Control panel)
    {
        var old = panel.Controls.Cast<Control>().ToArray();
        panel.Controls.Clear();
        foreach (var control in old) control.Dispose();
    }

    void ShowSuggestions(Issue issue, Rectangle mark)
    {
        statusOpen = false;
        ClearPanel(rows);
        ClearPanel(actions);
        bool accent = issue.Kind == "Tilde";
        kindLabel.Text = accent ? "TILDE" : "ORTOGRAFÍA";
        kindLabel.ForeColor = Theme.Mark;
        heading.Text = issue.Word;
        if (issue.Suggestions.Length == 0) status.Text = "No hay sugerencias para esta palabra.";
        else if (targetConsole || (editable == null && textPattern == null)) status.Text = "Elige una sugerencia para copiarla.";
        else status.Text = accent ? "Falta la tilde en este contexto." : "¿Quisiste decir…?";
        for (int i = 0; i < issue.Suggestions.Length; i++)
        {
            string replacement = issue.Suggestions[i];
            var chip = new Chip { Text = replacement, Style = i == 0 ? Chip.Look.Primary : Chip.Look.Secondary, Font = chipFont };
            chip.Click += (s, e) => Apply(issue, replacement);
            rows.Controls.Add(chip.Fit());
        }
        if (!accent) actions.Controls.Add(Action("+ Agregar al diccionario", () => AddToDictionary(issue)));
        actions.Controls.Add(Action("Ignorar", () => IgnoreWord(issue)));
        LayoutPanel();
        var screen = Screen.FromPoint(mark.Location).WorkingArea;
        Left = Math.Max(screen.Left, Math.Min(screen.Right - Width, mark.Left));
        Top = mark.Bottom + Height + Theme.S(6) > screen.Bottom ? Math.Max(screen.Top, mark.Top - Height - Theme.S(6)) : mark.Bottom + Theme.S(6);
        if (!Visible) Show();
        Invalidate();
    }

    void AddToDictionary(Issue issue)
    {
        try
        {
            if (!dictionary.Add(issue.Word)) { status.Text = "Solo se pueden agregar palabras sin números ni espacios."; return; }
            dictionaryStamp = DictionaryStamp();
            Recheck();
        }
        catch (Exception ex) { status.Text = "No se pudo guardar: " + ex.Message; }
    }

    void IgnoreWord(Issue issue)
    {
        ignored.Add(issue.Word);
        Recheck();
    }

    void Recheck()
    {
        checkedFingerprint = lastSeen = "";
        HideMarks();
        Hide();
    }

    void Apply(Issue issue, string replacement)
    {
        bool inputStarted = false;
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
                        applied = ReplacementGuard.TryReplace(
                            () => {
                                if (!SetForegroundWindow(targetWindow))
                                    throw new Exception("No se pudo activar el campo de texto.");
                                target.SetFocus();
                            },
                            () => GetForegroundWindow() == targetWindow &&
                                target.Equals(AutomationElement.FocusedElement),
                            () => range.GetText(-1) == issue.Word &&
                                (editable == null || editable.Current.Value == checkedText),
                            () => range.Select(),
                            () => {
                                var selected = textPattern.GetSelection();
                                return selected != null && selected.Length == 1 &&
                                    selected[0].GetText(-1) == issue.Word &&
                                    selected[0].CompareEndpoints(TextPatternRangeEndpoint.Start, range, TextPatternRangeEndpoint.Start) == 0 &&
                                    selected[0].CompareEndpoints(TextPatternRangeEndpoint.End, range, TextPatternRangeEndpoint.End) == 0;
                            },
                            () => { inputStarted = true; TypeUnicode(replacement); });
                    }
                }
                catch (Exception ex)
                {
                    // SendInput can fail after sending part of the text. Do not retry
                    // through SetValue after any input has already been submitted.
                    if (inputStarted) throw;
                    try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "SpanishWriter.log"), DateTime.Now.ToString("s") + " Selección de texto no disponible: " + ex + Environment.NewLine); } catch { }
                }
            }
            if (!applied && editable != null && !editable.Current.IsReadOnly)
            {
                string live = editable.Current.Value;
                if (live == checkedText && issue.Start >= 0 && issue.Length > 0 &&
                    issue.Start <= live.Length - issue.Length &&
                    live.Substring(issue.Start, issue.Length) == issue.Word)
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
        catch (Exception ex)
        {
            if (inputStarted)
            {
                ClearPanel(rows);
                HideMarks();
                checkedText = checkedFingerprint = "";
            }
            status.Text = inputStarted ? "Windows no confirmó la corrección completa. Revisa el texto." : "No se pudo modificar este campo.";
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "SpanishWriter.log"), DateTime.Now.ToString("s") + " Error al aplicar: " + ex + Environment.NewLine); } catch { }
        }
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
        if (disposing) { timer.Dispose(); chipFont.Dispose(); speller.Dispose(); if (wpsServer != null) wpsServer.Dispose(); tray.Visible = false; tray.Dispose(); HideMarks(); }
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
