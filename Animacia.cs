using System.Diagnostics;

namespace CCPetomar;

// Plynulá animácia v rytme obrazovky: každý snímok počká na obnovenie displeja (DwmFlush),
// medzi snímkami sa spracujú správy okna (BeginInvoke) – nič nezamrzne.
public class Animacia
{
    readonly Control vlastnik;
    readonly double trvanie;
    readonly Action<double> krok;
    readonly Action hotovo;
    readonly Stopwatch sw = Stopwatch.StartNew();
    bool stop;

    Animacia(Control c, double ms, Action<double> k, Action h) { vlastnik = c; trvanie = ms; krok = k; hotovo = h; }

    public static Animacia Spusti(Control c, double ms, Action<double> krok, Action hotovo)
    {
        var a = new Animacia(c, ms, krok, hotovo);
        a.Dalsi();
        return a;
    }

    public void Zastav() => stop = true;

    void Dalsi()
    {
        if (stop) return;
        double p = Math.Min(1, sw.Elapsed.TotalMilliseconds / trvanie);
        try { krok(p); } catch { }
        if (p >= 1) { stop = true; try { hotovo?.Invoke(); } catch { } return; }
        Win.Snimok();
        try { vlastnik.BeginInvoke(new Action(Dalsi)); } catch { }
    }
}
