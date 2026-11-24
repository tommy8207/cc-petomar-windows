using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace CCPetomar;

public class HlavneOkno : Form
{
    readonly WebView2 wv = new() { Dock = DockStyle.Fill };
    readonly NotifyIcon tray = new();
    readonly System.Windows.Forms.Timer tmrSpat = new() { Interval = 10000 };
    readonly System.Windows.Forms.Timer tmrTop = new() { Interval = 4000 };
    readonly System.Windows.Forms.Timer tmrAkt = new() { Interval = 2 * 3600 * 1000 };
    ToolStripMenuItem miAuto;
    bool koniec, chybaStranka;
    FormWindowState posledny = FormWindowState.Normal;
    string balonAkcia = "";

    public bool Hovor { get; private set; }

    public HlavneOkno()
    {
        Text = Program.NAZOV;
        try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath); } catch { }
        MinimumSize = new Size(420, 400);
        var n = Program.Nast;
        Size = new Size(Math.Max(600, n.W), Math.Max(400, n.H));
        if (n.X >= 0 && n.Y >= 0 && Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(new Rectangle(n.X, n.Y, 200, 100))))
        { StartPosition = FormStartPosition.Manual; Location = new Point(n.X, n.Y); }
        else StartPosition = FormStartPosition.CenterScreen;
        if (n.Max) WindowState = FormWindowState.Maximized;
        BackColor = Color.FromArgb(10, 17, 22);
        Controls.Add(wv);

        // ikona pri hodinách
        tray.Icon = Icon ?? SystemIcons.Application;
        tray.Text = Program.NAZOV;
        tray.Visible = true;
        var m = new ContextMenuStrip();
        m.Items.Add("Otvoriť CC", null, (s, e) => Ukaz(false)).Font = new Font(m.Font, FontStyle.Bold);
        m.Items.Add("Obnoviť stránku", null, (s, e) => { Ukaz(false); Nacitaj(); });
        m.Items.Add(new ToolStripSeparator());
        miAuto = new ToolStripMenuItem("Spúšťať so systémom Windows") { Checked = n.Autostart, CheckOnClick = true };
        miAuto.CheckedChanged += (s, e) => { Program.Nast.Autostart = miAuto.Checked; Program.Nast.Uloz(); Program.Autostart(miAuto.Checked); };
        m.Items.Add(miAuto);
        m.Items.Add("Hľadať novú verziu", null, async (s, e) => await Program.Aktualizacia(this, true));
        m.Items.Add("Adresa servera…", null, (s, e) => ZmenServer());
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("O programe (v" + Program.VERZIA + ")", null, (s, e) => Hlasenie("CC Petomar pre Windows, verzia v" + Program.VERZIA + "\nServer: " + Program.Nast.Server + "\nZáznam: " + Path.Combine(Program.Data, "zaznam.txt"), false));
        m.Items.Add("Ukončiť program", null, (s, e) =>
        {
            if (Otazka("Naozaj ukončiť CC Petomar?\n\nKým je program vypnutý, okno pri hovore nevyskočí a nezaznejú správy od kolegov.")) Ukonci();
        });
        tray.ContextMenuStrip = m;
        tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) Ukaz(false); };
        tray.BalloonTipClicked += (s, e) => Ukaz(false);

        tmrSpat.Tick += (s, e) => { tmrSpat.Stop(); Nacitaj(); };
        tmrTop.Tick += (s, e) => { tmrTop.Stop(); TopMost = false; };
        tmrAkt.Tick += async (s, e) => await Program.Aktualizacia(this, false);
        Resize += (s, e) => { if (WindowState != FormWindowState.Minimized) posledny = WindowState; };
        Load += async (s, e) => await Start();
    }

    async Task Start()
    {
        try
        {
            var opt = new CoreWebView2EnvironmentOptions(
                "--autoplay-policy=no-user-gesture-required --disable-background-timer-throttling --disable-renderer-backgrounding --disable-backgrounding-occluded-windows");
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Program.Data, "WebView2"), opt);
            await wv.EnsureCoreWebView2Async(env);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            if (Otazka("V počítači chýba súčasť Microsoft Edge WebView2, ktorú CC potrebuje.\n\nOtvoriť stránku Microsoftu na jej stiahnutie? (Evergreen Bootstrapper → nainštalovať → znova spustiť CC Petomar)"))
                Process.Start(new ProcessStartInfo("https://developer.microsoft.com/microsoft-edge/webview2/") { UseShellExecute = true });
            Ukonci(); return;
        }
        catch (Exception ex) { Program.Log("webview " + ex); Hlasenie("CC sa nepodarilo spustiť: " + ex.Message, true); Ukonci(); return; }

        var cw = wv.CoreWebView2;
        cw.Settings.UserAgent = cw.Settings.UserAgent + " CCPetomarApp/" + Program.VERZIA + " CCPetomarWin";
        cw.Settings.IsStatusBarEnabled = false;
        cw.Settings.IsPasswordAutosaveEnabled = true;
        cw.Settings.IsGeneralAutofillEnabled = true;
        await cw.AddScriptToExecuteOnDocumentCreatedAsync(Skript.JS);
        cw.WebMessageReceived += Sprava;
        cw.NewWindowRequested += (s, e) => { e.Handled = true; try { Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true }); } catch { } };
        cw.NavigationCompleted += (s, e) =>
        {
            if (chybaStranka) return;
            if (!e.IsSuccess)
            {
                Program.Log("nacitanie zlyhalo " + e.WebErrorStatus);
                chybaStranka = true;
                cw.NavigateToString(Skript.Chyba(Program.Nast.Server));
                tmrSpat.Start();
            }
        };
        cw.ProcessFailed += (s, e) => { Program.Log("webview padol " + e.ProcessFailedKind); tmrSpat.Start(); };
        Nacitaj();
        tmrAkt.Start();
        _ = Task.Delay(30000).ContinueWith(_ => BeginInvoke(new Action(async () => await Program.Aktualizacia(this, false))));
    }

    void Nacitaj()
    {
        if (wv.CoreWebView2 == null) return;
        chybaStranka = false;
        try { wv.CoreWebView2.Navigate(Program.Nast.Server.TrimEnd('/') + "/cc"); } catch (Exception e) { Program.Log("navigate " + e.Message); tmrSpat.Start(); }
    }

    // správy zo stránky CC (vložený skript)
    void Sprava(object s, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var d = JsonDocument.Parse(e.WebMessageAsJson);
            var r = d.RootElement;
            string typ = r.TryGetProperty("typ", out var t) ? t.GetString() : "";
            string Str(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";
            switch (typ)
            {
                case "hovor":
                    Program.Log("hovor " + Str("f") + " " + Str("kto"));
                    Ukaz(true);
                    Balon(Str("f") == "int" ? "Volá kolega" : "Prichádza hovor", Str("kto"), ToolTipIcon.Info);
                    break;
                case "sprava":
                    Program.Log("sprava od " + Str("od"));
                    Balon("Správa od: " + Str("od"), Str("text"), ToolTipIcon.Info);
                    Blikaj();
                    break;
                case "stav":
                    Hovor = r.TryGetProperty("hovor", out var h) && h.ValueKind == JsonValueKind.True;
                    var ja = Str("meno");
                    var txt = Program.NAZOV + (ja != "" ? " · " + ja + " " + Str("ja") : "");
                    tray.Text = txt.Length > 63 ? txt[..63] : txt;
                    Text = txt;
                    break;
            }
        }
        catch (Exception ex) { Program.Log("sprava " + ex.Message); }
    }

    void Balon(string nadpis, string text, ToolTipIcon ic)
    {
        try { tray.ShowBalloonTip(8000, nadpis, string.IsNullOrWhiteSpace(text) ? " " : text, ic); } catch { }
    }

    // ukázať okno; popredie = pri hovore vyskočí pred všetky okná
    public void Ukaz(bool popredie)
    {
        if (!Visible) Show();
        ShowInTaskbar = true;
        if (WindowState == FormWindowState.Minimized) WindowState = posledny;
        if (popredie)
        {
            TopMost = true;
            tmrTop.Stop(); tmrTop.Start();
            Blikaj();
        }
        Win.DoPopredia(Handle);
        Activate();
    }

    void Blikaj() { try { Win.Blikaj(Handle); } catch { } }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        UlozPolohu();
        if (!koniec && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            if (!Program.Nast.TipLista)
            {
                Program.Nast.TipLista = true; Program.Nast.Uloz();
                Balon("CC Petomar beží ďalej", "Program je pri hodinách (ikona CC). Pri hovore sa okno samo otvorí. Úplne ukončiť: pravý klik na ikonu → Ukončiť program.", ToolTipIcon.Info);
            }
            return;
        }
        tray.Visible = false;
        base.OnFormClosing(e);
    }

    void UlozPolohu()
    {
        var n = Program.Nast;
        n.Max = WindowState == FormWindowState.Maximized;
        var b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        if (b.Width > 100) { n.X = b.X; n.Y = b.Y; n.W = b.Width; n.H = b.Height; }
        n.Uloz();
    }

    public void Ukonci()
    {
        koniec = true;
        try { tray.Visible = false; } catch { }
        Close();
        Application.Exit();
    }

    public void Hlasenie(string t, bool chyba)
    {
        using var o = new Form { TopMost = true, ShowInTaskbar = false, StartPosition = FormStartPosition.CenterScreen, Size = new Size(1, 1), Opacity = 0 };
        o.Show();
        MessageBox.Show(o, t, Program.NAZOV, MessageBoxButtons.OK, chyba ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    public bool Otazka(string t)
    {
        using var o = new Form { TopMost = true, ShowInTaskbar = false, StartPosition = FormStartPosition.CenterScreen, Size = new Size(1, 1), Opacity = 0 };
        o.Show();
        return MessageBox.Show(o, t, Program.NAZOV, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    }

    void ZmenServer()
    {
        using var f = new Form { Text = "Adresa servera CC", FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterScreen, ClientSize = new Size(440, 130), MaximizeBox = false, MinimizeBox = false, TopMost = true };
        var l = new Label { Text = "Adresa cc-servera (vo firme: http://192.168.250.181:3000)", Left = 12, Top = 12, Width = 416 };
        var tb = new TextBox { Text = Program.Nast.Server, Left = 12, Top = 38, Width = 416 };
        var ok = new Button { Text = "Uložiť", Left = 252, Top = 84, Width = 84, DialogResult = DialogResult.OK };
        var zr = new Button { Text = "Zrušiť", Left = 344, Top = 84, Width = 84, DialogResult = DialogResult.Cancel };
        f.Controls.AddRange(new Control[] { l, tb, ok, zr }); f.AcceptButton = ok; f.CancelButton = zr;
        if (f.ShowDialog() != DialogResult.OK) return;
        var v = tb.Text.Trim().TrimEnd('/');
        if (!v.StartsWith("http://") && !v.StartsWith("https://")) v = "http://" + v;
        Program.Nast.Server = v; Program.Nast.Uloz(); Nacitaj();
    }
}

static class Win
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr pid);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool FlashWindowEx(ref FLASHWINFO f);

    [StructLayout(LayoutKind.Sequential)]
    struct FLASHWINFO { public uint cbSize; public IntPtr hwnd; public uint dwFlags; public uint uCount; public uint dwTimeout; }

    public static void DoPopredia(IntPtr h)
    {
        try
        {
            var fg = GetForegroundWindow();
            uint a = GetWindowThreadProcessId(fg, IntPtr.Zero), b = GetCurrentThreadId();
            bool spoj = a != 0 && a != b && AttachThreadInput(a, b, true);
            ShowWindow(h, 5); // SW_SHOW
            BringWindowToTop(h);
            SetForegroundWindow(h);
            if (spoj) AttachThreadInput(a, b, false);
        }
        catch { }
    }

    public static void Blikaj(IntPtr h)
    {
        var f = new FLASHWINFO { cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(), hwnd = h, dwFlags = 3 | 12, uCount = 0, dwTimeout = 0 }; // ALL | TIMERNOFG
        FlashWindowEx(ref f);
    }
}
