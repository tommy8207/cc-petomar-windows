using System.Drawing.Drawing2D;

namespace CCPetomar;

// Úzka lišta CC na pravom okraji obrazovky (stále navrchu). Klik = vysunúť pás CC.
public class Lista : Form
{
    public Action Klik;
    Color bodka = Color.FromArgb(154, 170, 180);
    int zmeskane;
    readonly Icon ikona;
    readonly ToolTip tip = new();

    public Lista()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(17, 27, 34);
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        Size = new Size(LogicalToDeviceUnits(46), LogicalToDeviceUnits(190));
        try { ikona = Icon.ExtractAssociatedIcon(Environment.ProcessPath); } catch { }
        tip.SetToolTip(this, "CC Petomar – klik = otvoriť");
        MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) Klik?.Invoke(); };
    }

    protected override CreateParams CreateParams
    {
        get { var p = base.CreateParams; p.ExStyle |= 0x80 | 0x08000000; return p; } // TOOLWINDOW | NOACTIVATE
    }
    protected override bool ShowWithoutActivation => true;

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        int r = LogicalToDeviceUnits(14);
        using var g = new GraphicsPath();
        g.AddArc(0, 0, r * 2, r * 2, 180, 90);
        g.AddLine(r, 0, Width, 0);
        g.AddLine(Width, 0, Width, Height);
        g.AddLine(Width, Height, r, Height);
        g.AddArc(0, Height - r * 2, r * 2, r * 2, 90, 90);
        g.CloseFigure();
        Region = new Region(g);
    }

    public void Nastav(string stav, int ms)
    {
        var c = stav == "voľný" ? Color.FromArgb(74, 222, 128)
              : stav == "zvoní" || stav == "v hovore" ? Color.FromArgb(242, 176, 76)
              : stav == "nedostupný" ? Color.FromArgb(248, 113, 113)
              : Color.FromArgb(154, 170, 180);
        if (c == bodka && ms == zmeskane) return;
        bodka = c; zmeskane = ms;
        tip.SetToolTip(this, "CC Petomar · " + (stav == "" ? "" : stav) + (ms > 0 ? " · zmeškané: " + ms : "") + " – klik = otvoriť");
        Invalidate();
    }

    public void Umiestni(Rectangle wa, bool mimo)
    {
        Top = wa.Top + (wa.Height - Height) / 2;
        Left = mimo ? wa.Right : wa.Right - Width;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int u = LogicalToDeviceUnits(1), w = Width;
        using (var p = new Pen(Color.FromArgb(43, 191, 193), 2 * u)) g.DrawLine(p, u, LogicalToDeviceUnits(14), u, Height - LogicalToDeviceUnits(14));
        int y = LogicalToDeviceUnits(12), ic = LogicalToDeviceUnits(30);
        if (ikona != null) g.DrawIcon(ikona, new Rectangle((w - ic) / 2, y, ic, ic));
        y += ic + LogicalToDeviceUnits(14);
        int d = LogicalToDeviceUnits(14);
        using (var b = new SolidBrush(bodka)) g.FillEllipse(b, (w - d) / 2, y, d, d);
        y += d + LogicalToDeviceUnits(14);
        if (zmeskane > 0)
        {
            int bw = LogicalToDeviceUnits(30), bh = LogicalToDeviceUnits(22);
            var rc = new Rectangle((w - bw) / 2, y, bw, bh);
            using (var b = new SolidBrush(Color.FromArgb(248, 113, 113))) g.FillEllipse(b, rc);
            using var f = new Font("Segoe UI", 9f, FontStyle.Bold);
            TextRenderer.DrawText(g, zmeskane > 99 ? "99" : zmeskane.ToString(), f, rc, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        using var f2 = new Font("Segoe UI", 14f, FontStyle.Bold);
        TextRenderer.DrawText(g, "‹", f2, new Rectangle(0, Height - LogicalToDeviceUnits(40), w, LogicalToDeviceUnits(30)), Color.FromArgb(43, 191, 193), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}
