using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace CCPetomar;

// Tri režimy okna:
//  Plne  – bežné okno CC
//  Pas   – pás CC na pravej polovici zvoleného monitora, nad všetkými oknami (pri hovore sa vysunie sám)
//  Lista – CC zbalené do lišty na pravom okraji zvoleného monitora (✕, minimalizovať alebo tlačidlo Lišta)
// Pás sa zasunie len na klik (Lišta / ✕ / –), nie samo.
public class HlavneOkno : Form
{
    enum Rezim { Plne, Pas, Lista }

    readonly WebView2 wv = new() { Dock = DockStyle.Fill };
    readonly NotifyIcon tray = new();
    readonly Lista lista = new();
    readonly System.Windows.Forms.Timer tmrSpat = new() { Interval = 10000 };
    readonly System.Windows.Forms.Timer tmrTop = new() { Interval = 4000 };
    readonly System.Windows.Forms.Timer tmrAkt = new() { Interval = 60 * 1000 }; // nová verzia programu – kontrola každú minútu
    System.Windows.Forms.Timer anim;
    ToolStripMenuItem miAuto;
    bool koniec, chybaStranka;
    readonly bool startLista;
    Rezim rezim = Rezim.Plne;
    Rectangle plne;
    bool plneMax;

    public bool Hovor { get; private set; }

    public HlavneOkno(bool startVListe)
    {
        startLista = startVListe;
        Text = Program.NAZOV;
        try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath); } catch { }
        MinimumSize = new Size(380, 400);
        var n = Program.Nast;
        Size = new Size(Math.Max(600, n.W), Math.Max(400, n.H));
        if (n.X >= 0 && n.Y >= 0 && Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(new Rectangle(n.X, n.Y, 200, 100))))
        { StartPosition = FormStartPosition.Manual; Location = new Point(n.X, n.Y); }
        else StartPosition = FormStartPosition.CenterScreen;
        plneMax = n.Max;
        if (n.Max) WindowState = FormWindowState.Maximized;
        if (startLista) Opacity = 0;
        BackColor = Color.FromArgb(10, 17, 22);
        Controls.Add(wv);

        lista.Klik = () => DoPasu(false);

        // ikona pri hodinách
        tray.Icon = Icon ?? SystemIcons.Application;
        tray.Text = Program.NAZOV;
        tray.Visible = true;
        var m = new ContextMenuStrip();
        m.Items.Add("Otvoriť CC", null, (s, e) => DoPlna()).Font = new Font(m.Font, FontStyle.Bold);
        m.Items.Add("Obnoviť stránku", null, (s, e) => Nacitaj());
        m.Items.Add(new ToolStripSeparator());
        miAuto = new ToolStripMenuItem("Spúšťať so systémom Windows") { Checked = n.Autostart, CheckOnClick = true };
        miAuto.CheckedChanged += (s, e) => { Program.Nast.Autostart = miAuto.Checked; Program.Nast.Uloz(); Program.Autostart(miAuto.Checked); };
        m.Items.Add(miAuto);
        var miMon = new ToolStripMenuItem("Lišta na monitore");
        miMon.DropDownItems.Add("…");
        miMon.DropDownOpening += (s, e) =>
        {
            miMon.DropDownItems.Clear();
            var cs = CielScreen();
            int i = 0;
            foreach (var sc in Screen.AllScreens.OrderBy(x => x.Bounds.X).ThenBy(x => x.Bounds.Y))
            {
                i++;
                var dn = sc.DeviceName;
                var it = new ToolStripMenuItem("Monitor " + i + (sc.Primary ? " (hlavný)" : "") + " – " + sc.Bounds.Width + "×" + sc.Bounds.Height) { Checked = dn == cs.DeviceName };
                it.Click += (s2, e2) => { Program.Nast.Monitor = dn; Program.Nast.Uloz(); Premiestni(); };
                miMon.DropDownItems.Add(it);
            }
        };
        m.Items.Add(miMon);
        m.Items.Add("Hľadať novú verziu", null, async (s, e) => await Program.Aktualizacia(this, true));
        m.Items.Add("Adresa servera…", null, (s, e) => ZmenServer());
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("O programe (v" + Program.VERZIA + ")", null, (s, e) => Hlasenie("CC Petomar pre Windows, verzia v" + Program.VERZIA + "\nServer: " + Program.Nast.Server + "\nZáznam: " + Path.Combine(Program.Data, "zaznam.txt"), false));
        m.Items.Add("Ukončiť program", null, (s, e) =>
        {
            if (Otazka("Naozaj ukončiť CC Petomar?\n\nKým je program vypnutý, okno pri hovore nevyskočí a nezaznejú správy od kolegov.")) Ukonci();
        });
        tray.ContextMenuStrip = m;
        tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) DoPlna(); };
        tray.BalloonTipClicked += (s, e) => DoPasu(false);

        tmrSpat.Tick += (s, e) => { tmrSpat.Stop(); Nacitaj(); };
        tmrTop.Tick += (s, e) => { tmrTop.Stop(); if (rezim == Rezim.Plne) TopMost = false; };
        tmrAkt.Tick += async (s, e) => await Program.Aktualizacia(this, false);
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (s, e) => { try { BeginInvoke(new Action(Premiestni)); } catch { } };
        Load += async (s, e) => await Start();
        Shown += (s, e) =>
        {
            if (!startLista) return;
            ZapamatajPlne();
            Hide(); Opacity = 1;
            rezim = Rezim.Lista;
            UkazListu(false);
        };
    }

    // ---------- režimy okna ----------
    static Screen CielScreen() => Screen.AllScreens.FirstOrDefault(x => x.DeviceName == Program.Nast.Monitor) ?? Screen.PrimaryScreen;
    static Rectangle WA => CielScreen().WorkingArea;
    static int PasW => Math.Max(380, WA.Width / 2);

    void Premiestni()
    {
        var wa = WA;
        if (rezim == Rezim.Lista && lista.Visible) lista.Umiestni(wa, false);
        if (rezim == Rezim.Pas) { anim?.Stop(); Bounds = new Rectangle(wa.Right - PasW, wa.Top, PasW, wa.Height); }
    }

    void ZapamatajPlne()
    {
        if (rezim != Rezim.Plne) return;
        plneMax = WindowState == FormWindowState.Maximized;
        var b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        if (b.Width > 100) plne = b;
    }

    void Posun(Form f, int z, int k, Action hotovo)
    {
        anim?.Stop(); anim?.Dispose();
        var sw = Stopwatch.StartNew();
        var t = new System.Windows.Forms.Timer { Interval = 10 };
        anim = t;
        f.Left = z;
        t.Tick += (s, e) =>
        {
            double p = Math.Min(1, sw.ElapsedMilliseconds / 230.0), q = 1 - Math.Pow(1 - p, 3);
            f.Left = (int)(z + (k - z) * q);
            if (p >= 1) { t.Stop(); if (anim == t) anim = null; t.Dispose(); hotovo?.Invoke(); }
        };
        t.Start();
    }

    void UkazListu(bool animuj)
    {
        var wa = WA;
        lista.Umiestni(wa, animuj);
        lista.Show();
        lista.TopMost = true;
        if (animuj) Posun(lista, wa.Right, wa.Right - lista.Width, null);
    }

    void DoListy()
    {
        if (rezim == Rezim.Lista) return;
        anim?.Stop();
        ZapamatajPlne();
        UlozPolohu();
        var cs = CielScreen(); var wa = cs.WorkingArea;
        rezim = Rezim.Lista;
        lista.Umiestni(wa, false);
        int ty0 = lista.Top, ty1 = lista.Bottom;
        bool efekt = Visible && WindowState != FormWindowState.Minimized && Screen.FromHandle(Handle).DeviceName == cs.DeviceName;
        Action poZbaleni = () =>
        {
            UkazListu(true);
            if (!Program.Nast.TipLista)
            {
                Program.Nast.TipLista = true; Program.Nast.Uloz();
                Balon("CC Petomar je vpravo na okraji obrazovky", "Klik na lištu CC = otvoriť. Pri hovore sa CC vysunie samo. Úplne ukončiť: pravý klik na ikonu CC pri hodinách → Ukončiť program.", ToolTipIcon.Info);
            }
        };
        if (efekt)
        {
            try
            {
                var r = Rectangle.Intersect(Bounds, cs.Bounds);
                var bmp = Genie.Snimka(r);
                Hide(); TopMost = false;
                new Genie(bmp, r, cs.Bounds, wa.Right, ty0, ty1, poZbaleni).Show();
                return;
            }
            catch (Exception e) { Program.Log("genie " + e.Message); }
        }
        if (WindowState != FormWindowState.Normal) { WindowState = FormWindowState.Normal; Bounds = wa; }
        if (!Visible) { TopMost = false; poZbaleni(); return; }
        Posun(this, Left, wa.Right, () => { Hide(); TopMost = false; poZbaleni(); });
    }

    void DoPasu(bool auto)
    {
        if (auto) NaVrch();
        if (rezim == Rezim.Pas)
        {
            TopMost = true; Win.DoPopredia(Handle); Activate();
            return;
        }
        ZapamatajPlne();
        var wa = WA;
        lista.Hide();
        if (WindowState != FormWindowState.Normal) WindowState = FormWindowState.Normal;
        int w = PasW;
        Bounds = new Rectangle(wa.Right, wa.Top, w, wa.Height);
        TopMost = true;
        rezim = Rezim.Pas;
        if (!Visible) Show();
        Win.DoPopredia(Handle);
        Posun(this, wa.Right, wa.Right - w, () => { Win.DoPopredia(Handle); Activate(); });
    }

    void DoPlna()
    {
        lista.Hide();
        if (rezim != Rezim.Plne)
        {
            anim?.Stop();
            TopMost = false;
            WindowState = FormWindowState.Normal;
            if (plne.Width > 100) Bounds = plne;
            else { var wa = WA; Bounds = new Rectangle(wa.X + 40, wa.Y + 40, Math.Min(1280, wa.Width - 80), Math.Min(860, wa.Height - 80)); }
            if (plneMax) WindowState = FormWindowState.Maximized;
            rezim = Rezim.Plne;
        }
        if (!Visible) Show();
        Win.DoPopredia(Handle); Activate();
    }

    // obraz CC úplne hore (hlavička + karta hovoru)
    void NaVrch()
    {
        try { wv.CoreWebView2?.ExecuteScriptAsync("try{window.scrollTo(0,0)}catch(e){}"); } catch { }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0112) // WM_SYSCOMMAND
        {
            int c = (int)m.WParam & 0xFFF0;
            if (c == 0xF020) { DoListy(); return; }                       // minimalizovať → lišta
            if (c == 0xF030 && rezim == Rezim.Pas) { DoPlna(); return; }  // maximalizovať v páse → celé okno
        }
        base.WndProc(ref m);
    }

    // ---------- WebView2 ----------
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
            bool Bool(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;
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
                case "lista":
                    DoListy();
                    break;
                case "stav":
                    Hovor = Bool("hovor");
                    int ms = r.TryGetProperty("ms", out var mv) && mv.ValueKind == JsonValueKind.Number ? mv.GetInt32() : 0;
                    lista.Nastav(Str("st"), ms);
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

    // popredie = hovor: z lišty sa vysunie pás nad všetky okná; celé okno sa dá navrch
    public void Ukaz(bool popredie)
    {
        if (!popredie) { DoPlna(); return; }
        NaVrch();
        if (rezim == Rezim.Plne)
        {
            if (!Visible) Show();
            TopMost = true;
            tmrTop.Stop(); tmrTop.Start();
            Win.DoPopredia(Handle); Activate();
        }
        else DoPasu(true);
        Blikaj();
    }

    void Blikaj() { try { Win.Blikaj(Handle); } catch { } }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!koniec && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            DoListy();
            return;
        }
        UlozPolohu();
        tray.Visible = false;
        base.OnFormClosing(e);
    }

    void UlozPolohu()
    {
        var n = Program.Nast;
        ZapamatajPlne();
        n.Max = plneMax;
        if (plne.Width > 100) { n.X = plne.X; n.Y = plne.Y; n.W = plne.Width; n.H = plne.Height; }
        n.Uloz();
    }

    public void Ukonci()
    {
        koniec = true;
        try { tray.Visible = false; lista.Close(); } catch { }
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

    public static IntPtr Popredie() => GetForegroundWindow();

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
        var f = new FLASHWINFO { cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(), hwnd = h, dwFlags = 3 | 12, uCount = 0, dwTimeout = 0 };
        FlashWindowEx(ref f);
    }
}
