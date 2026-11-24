using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace CCPetomar;

// Efekt „vcucnutia“ okna do lišty (ako minimalizovanie na Macu – genie).
// Snímka okna sa po stĺpcoch zužuje k lište a vtiahne sa do pravého okraja obrazovky.
public class Genie : Form
{
    readonly Bitmap src;
    readonly Rectangle zdroj, oblast;
    readonly int E, ty0, ty1;
    readonly Action hotovo;
    readonly Stopwatch sw = new();
    readonly System.Windows.Forms.Timer t = new() { Interval = 10 };
    const double TRVANIE = 520;
    bool koniec;

    public Genie(Bitmap snimka, Rectangle okno, Rectangle obrazovka, int pravyOkraj, int listaHore, int listaDole, Action hotovo)
    {
        src = snimka; zdroj = okno; oblast = obrazovka; E = pravyOkraj; ty0 = listaHore; ty1 = listaDole; this.hotovo = hotovo;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        DoubleBuffered = true;
        Bounds = oblast;
        t.Tick += (s, e) =>
        {
            Invalidate();
            if (sw.ElapsedMilliseconds >= TRVANIE && !koniec)
            {
                koniec = true; t.Stop();
                try { hotovo?.Invoke(); } catch { }
                Close();
            }
        };
    }

    protected override CreateParams CreateParams
    {
        get { var p = base.CreateParams; p.ExStyle |= 0x80 | 0x08000000 | 0x20; return p; } // TOOLWINDOW | NOACTIVATE | TRANSPARENT (kliky idú cez)
    }
    protected override bool ShowWithoutActivation => true;

    protected override void OnShown(EventArgs e) { base.OnShown(e); sw.Start(); t.Start(); }

    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(Color.Magenta);

    protected override void OnPaint(PaintEventArgs e)
    {
        double p = Math.Min(1, sw.ElapsedMilliseconds / TRVANIE);
        if (p >= 1) return;
        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.CompositingQuality = CompositingQuality.HighSpeed;

        // fáza 1: okno sa pri lište zúži (krivka), fáza 2: vtiahne sa do okraja
        double pa = Math.Min(1, p / 0.45); pa = pa * pa * (3 - 2 * pa);
        double pb = Math.Max(0, (p - 0.3) / 0.7); pb = pb * pb * pb;
        int x0 = zdroj.X, w = zdroj.Width, y0 = zdroj.Y, y1 = zdroj.Bottom;
        double rozsah = Math.Max(1, E - x0), posun = pb * (E - x0);
        int sl = Math.Max(4, w / 260);
        for (int sx = 0; sx < w; sx += sl)
        {
            int sw2 = Math.Min(sl, w - sx);
            double X = x0 + sx + posun;
            if (X >= E) break;
            double k = Math.Clamp((X - x0) / rozsah, 0, 1); k = k * k * (3 - 2 * k);
            double a = k * pa;
            double hore = y0 + (ty0 - y0) * a, dole = y1 + (ty1 - y1) * a;
            if (dole - hore < 1) continue;
            g.DrawImage(src, new RectangleF((float)(X - oblast.X), (float)(hore - oblast.Y), sw2 + 1, (float)(dole - hore)),
                new RectangleF(sx, 0, sw2, src.Height), GraphicsUnit.Pixel);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        t.Dispose(); src.Dispose();
        base.OnFormClosed(e);
    }

    public static Bitmap Snimka(Rectangle r)
    {
        var b = new Bitmap(r.Width, r.Height);
        using var g = Graphics.FromImage(b);
        g.CopyFromScreen(r.Location, Point.Empty, r.Size);
        return b;
    }
}
