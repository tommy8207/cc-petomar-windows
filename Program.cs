using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Win32;

namespace CCPetomar;

// Program CC Petomar pre Windows – okno s CC (WebView2), ikona pri hodinách,
// pri prichádzajúcom hovore vyskočí pred všetky okná, zvuky bez kliknutia,
// štart s Windows, automatická aktualizácia z cc-servera (/api/win-verzia, /win-exe).
static class Program
{
    // ZVÝŠIŤ PRI KAŽDEJ NOVEJ VERZII PROGRAMU (podľa nej sa volá súbor cc-petomar-win-vN.exe)
    public const int VERZIA = 7;
    public const string NAZOV = "CC Petomar";

    static readonly string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public static readonly string Data = Path.Combine(Local, "CCPetomar");
    public static readonly string ProgDir = Path.Combine(Local, "Programs", "CC Petomar");
    public static readonly string ExeCiel = Path.Combine(ProgDir, "CCPetomar.exe");

    public static Nastavenia Nast;
    static Mutex mutex;

    [STAThread]
    static void Main(string[] args)
    {
        Directory.CreateDirectory(Data);
        Nast = Nastavenia.Nacitaj();
        Log("start v" + VERZIA + " " + string.Join(" ", args) + " z " + Environment.ProcessPath);

        bool cakaj = args.Contains("--cakaj");
        string ja = Path.GetFullPath(Environment.ProcessPath ?? "");

        // 1) Prvé spustenie (stiahnutý súbor) → nainštalovať do %LOCALAPPDATA%\Programs\CC Petomar
        if (!args.Contains("--prenosne") && !string.Equals(ja, Path.GetFullPath(ExeCiel), StringComparison.OrdinalIgnoreCase))
        {
            if (Instaluj(ja)) return;
        }

        // 2) Iba jedna kópia programu – druhé spustenie len ukáže okno
        mutex = new Mutex(false, @"Local\CCPetomar_jedna");
        bool mam;
        try { mam = mutex.WaitOne(cakaj ? 20000 : 0); } catch (AbandonedMutexException) { mam = true; }
        if (!mam)
        {
            try { using var ev = EventWaitHandle.OpenExisting(@"Local\CCPetomar_ukaz"); ev.Set(); } catch { }
            return;
        }

        Uprac();
        if (Nast.Autostart) Autostart(true);

        ApplicationConfiguration.Initialize();
        var okno = new HlavneOkno(args.Contains("--lista"));

        // signály od iných kópií: ukázať okno / skončiť (pri aktualizácii)
        var ukaz = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\CCPetomar_ukaz");
        var koniec = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\CCPetomar_koniec");
        new Thread(() =>
        {
            var h = new WaitHandle[] { ukaz, koniec };
            while (true)
            {
                int i = WaitHandle.WaitAny(h);
                try { okno.BeginInvoke(new Action(() => { if (i == 0) okno.Ukaz(false); else okno.Ukonci(); })); } catch { }
                if (i == 1) break;
            }
        }) { IsBackground = true }.Start();

        Application.Run(okno);
        try { mutex.ReleaseMutex(); } catch { }
    }

    // ---------- inštalácia ----------
    static bool Instaluj(string zdroj)
    {
        try
        {
            bool prva = !File.Exists(ExeCiel);
            Directory.CreateDirectory(ProgDir);
            Uprac();
            if (!prva) Skus(() => File.Move(ExeCiel, StaraCesta()));   // bežiaci .exe sa dá premenovať
            Skus(() => File.Copy(zdroj, ExeCiel, true));
            Zastupca(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), NAZOV + ".lnk"));
            Zastupca(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), NAZOV + ".lnk"));
            Nast.Autostart = true; Nast.Uloz(); Autostart(true);
            // stará kópia (ak beží) sa zavrie, nová počká na jej koniec
            try { using var ev = EventWaitHandle.OpenExisting(@"Local\CCPetomar_koniec"); ev.Set(); } catch { }
            Process.Start(new ProcessStartInfo(ExeCiel, "--cakaj") { UseShellExecute = false, WorkingDirectory = ProgDir });
            if (prva)
                MessageBox.Show("CC Petomar je nainštalovaný.\n\nIkona „CC Petomar“ je na ploche a v ponuke Štart.\nProgram sa spúšťa sám so systémom Windows.\n\nStiahnutý súbor môžete teraz zmazať.",
                    NAZOV + " v" + VERZIA, MessageBoxButtons.OK, MessageBoxIcon.Information);
            Log("instalacia OK (prva=" + prva + ")");
            return true;
        }
        catch (Exception e)
        {
            Log("instalacia CHYBA " + e);
            MessageBox.Show("Inštalácia sa nepodarila:\n" + e.Message + "\n\nProgram sa spustí bez inštalácie.", NAZOV, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    static void Zastupca(string lnk)
    {
        try
        {
            var t = Type.GetTypeFromProgID("WScript.Shell"); if (t == null) return;
            object sh = Activator.CreateInstance(t);
            object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
            var st = sc.GetType();
            st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { ExeCiel });
            st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { ProgDir });
            st.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { "Call centrum Petomar" });
            st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { ExeCiel + ",0" });
            st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
        }
        catch (Exception e) { Log("zastupca " + lnk + " " + e.Message); }
    }

    public static void Autostart(bool on)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (on) k.SetValue(NAZOV, "\"" + ExeCiel + "\" --lista");
            else if (k.GetValue(NAZOV) != null) k.DeleteValue(NAZOV);
        }
        catch (Exception e) { Log("autostart " + e.Message); }
    }

    // ---------- aktualizácia ----------
    static readonly HttpClient http = new() { Timeout = TimeSpan.FromMinutes(5) };
    static int ponuknute;
    public static async Task Aktualizacia(HlavneOkno okno, bool rucne)
    {
        try
        {
            var s = await http.GetStringAsync(Nast.Server + "/api/win-verzia");
            using var d = JsonDocument.Parse(s);
            int v = d.RootElement.GetProperty("verzia").GetInt32();
            if (v <= VERZIA)
            {
                if (rucne) okno.Hlasenie("Máte najnovšiu verziu programu (v" + VERZIA + ").", false);
                return;
            }
            if (!rucne && (okno.Hovor || ponuknute == v)) return;
            ponuknute = v;
            if (!string.Equals(Path.GetFullPath(Environment.ProcessPath ?? ""), Path.GetFullPath(ExeCiel), StringComparison.OrdinalIgnoreCase))
            { if (rucne) okno.Hlasenie("Program nie je nainštalovaný – aktualizácia sa nedá.", true); return; }
            if (!okno.Otazka("Je pripravená nová verzia programu CC Petomar: v" + v + " (máte v" + VERZIA + ").\n\nAktualizovať teraz? Program sa na pár sekúnd zavrie a znova otvorí.")) return;

            Uprac();
            string nove = Path.Combine(Data, "CCPetomar-nova-v" + v + "-" + DateTime.Now.Ticks + ".exe");
            using (var st = await http.GetStreamAsync(Nast.Server + "/win-exe"))
            using (var f = File.Create(nove)) await st.CopyToAsync(f);
            if (new FileInfo(nove).Length < 1_000_000) throw new Exception("stiahnutý súbor je poškodený");
            string stara = StaraCesta();
            Skus(() => File.Move(ExeCiel, stara));
            try { Skus(() => File.Move(nove, ExeCiel)); }
            catch { try { File.Move(stara, ExeCiel); } catch { } throw; }
            Log("aktualizacia na v" + v);
            Process.Start(new ProcessStartInfo(ExeCiel, "--cakaj") { UseShellExecute = false, WorkingDirectory = ProgDir });
            okno.Ukonci();
        }
        catch (Exception e)
        {
            Log("aktualizacia " + e.GetType().Name + ": " + e.Message);
            if (rucne) okno.Hlasenie("Aktualizácia sa nepodarila: " + e.Message, true);
        }
    }

    // staré verzie: každá vlastné meno (prepísať bežiaci/zamknutý súbor Windows nedovolí)
    static string StaraCesta() => ExeCiel + ".old-" + DateTime.Now.Ticks;

    // zmazať staré verzie a nedokončené stiahnutia (čo sa nedá, ostane na budúce)
    static void Uprac()
    {
        try { foreach (var f in Directory.GetFiles(ProgDir, "CCPetomar.exe.old*")) try { File.Delete(f); } catch { } } catch { }
        try { foreach (var f in Directory.GetFiles(Data, "CCPetomar-nova*.exe")) try { File.Delete(f); } catch { } } catch { }
    }

    // súbor môže chvíľu držať antivírus – skúsiť viackrát
    static void Skus(Action a)
    {
        for (int i = 0; ; i++)
        {
            try { a(); return; }
            catch (Exception e) when (i < 9 && (e is IOException || e is UnauthorizedAccessException)) { Thread.Sleep(500); }
        }
    }

    // ---------- záznam ----------
    static readonly object logLock = new();
    public static void Log(string t)
    {
        try
        {
            lock (logLock)
            {
                var p = Path.Combine(Data, "zaznam.txt");
                if (File.Exists(p) && new FileInfo(p).Length > 500_000) File.Move(p, p + ".1", true);
                File.AppendAllText(p, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + t + Environment.NewLine);
            }
        }
        catch { }
    }
}

public class Nastavenia
{
    public string Server { get; set; } = "http://192.168.250.181:3000";
    public bool Autostart { get; set; } = true;
    public int X { get; set; } = -1;
    public int Y { get; set; } = -1;
    public int W { get; set; } = 1280;
    public int H { get; set; } = 860;
    public bool Max { get; set; }
    public int PasW { get; set; }
    public string Monitor { get; set; } = "";
    public bool TipLista { get; set; }

    static string Cesta => Path.Combine(Program.Data, "nastavenia.json");
    public static Nastavenia Nacitaj()
    {
        try { if (File.Exists(Cesta)) return JsonSerializer.Deserialize<Nastavenia>(File.ReadAllText(Cesta)) ?? new(); } catch { }
        return new Nastavenia();
    }
    public void Uloz()
    {
        try { File.WriteAllText(Cesta, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); } catch { }
    }
}
