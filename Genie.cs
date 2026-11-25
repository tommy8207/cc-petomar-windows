using System.Drawing.Drawing2D;

namespace CCPetomar;

// Efekt „vcucnutia“ okna do lišty a vyrolovania z nej (ako minimalizovanie na Macu – genie).
// Snímka okna sa po stĺpcoch zužuje k lište a vtiahne sa do pravého okraja obrazovky (spat = naopak).
public class Genie : Form
{
    readonly Bitmap src;
    readonly Rectangle okno, oblast;
    readonly int E, ty0, ty1;
    readonly bool spat;
    readonly Action hotovo;
    double p;
    const double TRVANIE = 430;

    public Genie(Bitmap snimka, Rectangle okno, Rectangle lista, int pravyOkraj, bool spat, Action hotovo)
    {
        src = snimka; this.okno = okno; E = pravyOkraj; ty0 = lista.Top; ty1 = lista.Bottom; this.spat = spat; this.hotovo = hotovo;
        oblast = Rectangle.Union(okno, lista);
        oblast = Rectangle.FromLTRB(oblast.Left, oblast.Top, Math.Max(oblast.Right, E), oblast.Bottom);
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        DoubleBuffered = true;
        Bounds = oblast;
        p = spat ? 1 : 0;
    }

    protected override CreateParams CreateParams
    {
        get { var c = base.CreateParams; c.ExStyle |= 0x80 | 0x08000000 | 0x20; return c; } // TOOLWINDOW | NOACTIVATE | TRANSPARENT
    }
    protected override bool ShowWithoutActivation => true;

    // okno cez dva monitory: nemeniť veľkosť pri zmene DPI
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x02E0) { m.Result = IntPtr.Zero; return; } // WM_DPICHANGED
        base.WndProc(ref m);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Animacia.Spusti(this, TRVANIE, t =>
        {
            p = spat ? 1 - t : t;
            Invalidate(); Update();
        }, () =>
        {
            try { hotovo?.Invoke(); } catch { }
            BeginInvoke(new Action(Close));
        });
    }

    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(Color.Magenta);

    protected override void OnPaint(PaintEventArgs e)
    {
        if (p >= 1) return;
        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.CompositingQuality = CompositingQuality.HighSpeed;
        g.SmoothingMode = SmoothingMode.None;

        // fáza 1: okno sa pri lište zúži do krivky, fáza 2: vtiahne sa do okraja (zrýchľuje)
        double pa = Math.Min(1, p / 0.5); pa = pa * pa * (3 - 2 * pa);
        double pb = Math.Max(0, (p - 0.25) / 0.75); pb = pb * pb * pb;
        int x0 = okno.X, w = okno.Width, y0 = okno.Y, y1 = okno.Bottom;
        double rozsah = Math.Max(1, E - x0), posun = pb * (E - x0), kx = (double)src.Width / w;
        int sl = Math.Max(3, w / 320);
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
                new RectangleF((float)(sx * kx), 0, (float)(sw2 * kx), src.Height), GraphicsUnit.Pixel);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        src.Dispose();
        base.OnFormClosed(e);
    }
}
