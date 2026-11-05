using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PKHeX.WinForms.Theming;

internal sealed class ThemedToolStripRenderer : ToolStripProfessionalRenderer
{
    private const int HoverRadius = 6;

    public ThemedToolStripRenderer() : base(new ThemedColorTable()) { }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        var p = Theme.Current;
        var bg = e.ToolStrip is ContextMenuStrip or ToolStripDropDown ? p.Surface1
            : e.ToolStrip.Parent is { BackColor.A: 255 } host ? host.BackColor
            : p.Surface0;
        using var brush = new SolidBrush(bg);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        if (e.ToolStrip is not ToolStripDropDown)
            return;
        using var pen = new Pen(Theme.Current.Border, 1f);
        var b = e.AffectedBounds;
        e.Graphics.DrawRectangle(pen, b.X, b.Y, b.Width - 1, b.Height - 1);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var item = e.Item;
        bool dropDownVisible = item is ToolStripMenuItem tsmi && tsmi.DropDown.Visible;
        if (!item.Selected && !dropDownVisible)
            return;

        var g = e.Graphics;
        var p = Theme.Current;
        var bounds = new Rectangle(Point.Empty, item.Size);
        var prev = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (item.Owner is MenuStrip)
        {
            var r = Rectangle.Inflate(bounds, -2, -2);
            using (var path = Rounded(r, HoverRadius))
            using (var fill = new SolidBrush(p.Surface2))
                g.FillPath(fill, path);

            using var accent = new Pen(p.Accent, 2f);
            g.DrawLine(accent, r.Left + 4, r.Bottom - 1, r.Right - 4, r.Bottom - 1);
        }
        else
        {
            var r = new Rectangle(bounds.X + 2, bounds.Y + 1, bounds.Width - 4, bounds.Height - 2);
            using var path = Rounded(r, HoverRadius);
            using var fill = new SolidBrush(p.Surface2);
            g.FillPath(fill, path);
        }

        g.SmoothingMode = prev;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        var p = Theme.Current;
        e.TextColor = e.Item.Enabled ? p.TextPrimary : p.TextDisabled;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Theme.Current.TextPrimary;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var bounds = e.Item.Bounds;
        using var pen = new Pen(Theme.Current.Border, 1f);
        int y = bounds.Height / 2;
        e.Graphics.DrawLine(pen, 24, y, bounds.Width - 8, y);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var g = e.Graphics;
        var p = Theme.Current;
        var r = Rectangle.Inflate(e.ImageRectangle, 1, 1);
        using (var path = Rounded(r, 3))
        using (var fill = new SolidBrush(p.Accent))
            g.FillPath(fill, path);

        using var pen = new Pen(p.AccentText, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        var b = e.ImageRectangle;
        var prev = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawLines(pen,
        [
            new Point(b.Left + 3, b.Top + (b.Height / 2)),
            new Point(b.Left + (b.Width / 2) - 1, b.Bottom - 3),
            new Point(b.Right - 3, b.Top + 3),
        ]);
        g.SmoothingMode = prev;
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(Theme.Current.Surface1);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    private static GraphicsPath Rounded(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0 || r.Width <= 0 || r.Height <= 0)
        {
            path.AddRectangle(r);
            return path;
        }

        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
