using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Forms.Automation;
using PKHeX.WinForms.Theming;

namespace PKHeX.WinForms.Controls;

/// <summary>
/// PictureBox control that can be focused and selected.
/// </summary>
/// <remarks>Draws a focus ring, and can be tabbed between, raising events for screen readers.</remarks>
public class SelectablePictureBox : PictureBox
{
    public SelectablePictureBox() => SetStyle(ControlStyles.Selectable, TabStop = true);

    public static int FocusBorderDeflate { get; set; }

    /// <summary>
    /// Draws the control as a rounded slot tile (used by box/party grids) instead of a plain rectangle.
    /// </summary>
    public bool DrawTile { get; set; }

    /// <summary>
    /// Shows the visible part of the sprite enlarged to fill the control (always on for <see cref="DrawTile"/>).
    /// </summary>
    public bool ZoomSprite { get; set; }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        base.OnMouseDown(e);
    }
    protected override void OnEnter(EventArgs e)
    {
        Invalidate();
        base.OnEnter(e);
        AccessibilityObject.RaiseAutomationNotification(AutomationNotificationKind.Other,
            AutomationNotificationProcessing.All, AccessibleDescription ?? AccessibleName ?? string.Empty);
    }
    protected override void OnLeave(EventArgs e)
    {
        Invalidate();
        base.OnLeave(e);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (!DrawTile)
        {
            base.OnPaintBackground(e);
            return;
        }

        var g = e.Graphics;
        var p = Theme.Current;
        ButtonRenderer.DrawParentBackground(g, ClientRectangle, this);

        // Over a box wallpaper keep the art visible; otherwise draw a solid tile.
        bool wallpaper = Parent?.BackgroundImage is not null;
        var tile = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = LogicalToDeviceUnits(6);
        var fill = wallpaper ? Color.FromArgb(40, p.Surface0) : p.Surface1;
        var border = wallpaper ? Color.FromArgb(90, Color.White) : p.Border;
        Shapes.Fill(g, tile, radius, fill, border);

        if (BackColor.A != 0) // slot state tint (e.g. invalid data)
            Shapes.Fill(g, tile, radius, BackColor);

        if (BackgroundImage is { } bg) // hover / drag highlight: a full-sprite glow, stretched over the tile
        {
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.DrawImage(bg, Rectangle.Inflate(tile, -2, -2));
        }
    }

    protected override void OnPaint(PaintEventArgs pe)
    {
        if ((DrawTile || ZoomSprite) && Image is Bitmap sprite)
        {
            var s = GetTileSprite(sprite);
            pe.Graphics.DrawImageUnscaled(s, (Width - s.Width) / 2, (Height - s.Height) / 2);
        }
        else
        {
            base.OnPaint(pe);
        }
        if (!Focused)
            return;
        var rc = ClientRectangle;
        rc.Inflate(-FocusBorderDeflate, -FocusBorderDeflate);
        if (!DrawTile)
        {
            ControlPaint.DrawFocusRectangle(pe.Graphics, rc);
            return;
        }
        rc.Width -= 2;
        rc.Height -= 2;
        rc.Offset(1, 1);
        Shapes.Fill(pe.Graphics, rc, LogicalToDeviceUnits(6), Color.Empty, Theme.Current.Accent, 2f);
    }

    /// <summary>
    /// The plain Pokémon of <see cref="PictureBox.Image"/> (same canvas, without item / shiny / legality marks). When set,
    /// the tile zooms the Pokémon on its own and keeps the marks at the sprite's own scale. Set it before the image.
    /// </summary>
    public Image? Subject { get; set; }

    // Rendered once per sprite and tile size.
    private Bitmap? _tileSprite;
    private Image? _tileSource, _tileSubject;
    private Size _tileSize;

    private Bitmap GetTileSprite(Bitmap src)
    {
        if (_tileSprite is not null && ReferenceEquals(_tileSource, src) && ReferenceEquals(_tileSubject, Subject) && _tileSize == Size)
            return _tileSprite;
        _tileSprite?.Dispose();
        (_tileSource, _tileSubject, _tileSize) = (src, Subject, Size);

        var result = new Bitmap(Math.Max(1, Width), Math.Max(1, Height));
        using var g = Graphics.FromImage(result);
        var full = new Rectangle(Point.Empty, src.Size);
        float canvasFit = Math.Min((float)Width / src.Width, (float)Height / src.Height);
        var canvas = Centered(src.Size, canvasFit); // the whole sprite canvas, fitted to the tile

        if (Subject is not Bitmap subject || subject.Size != src.Size)
        {
            DrawPixelArt(g, src, full, canvas);
            return _tileSprite = result;
        }

        // Every Pokémon gets the same zoom (so their sizes stay comparable); large ones shrink to stay inside the tile.
        // The marks stay where and how big they are on the canvas, so none is cropped or blown up.
        var box = OpaqueBounds(subject);
        int pad = LogicalToDeviceUnits(6);
        float contentFit = Math.Min((float)(Width - (2 * pad)) / box.Width, (float)(Height - (2 * pad)) / box.Height);
        float zoom = Math.Max(Math.Min(canvasFit * 1.4f, contentFit), Math.Min(1f, canvasFit));
        var (under, over) = SplitMarks(src, subject);
        DrawPixelArt(g, under, full, canvas); // backgrounds / stripes
        DrawPixelArt(g, subject, box, Centered(box.Size, zoom));
        DrawPixelArt(g, over, full, canvas); // item, shiny star, flags
        under.Dispose();
        over.Dispose();
        return _tileSprite = result;
    }

    private Rectangle Centered(Size size, float scale)
    {
        int w = Math.Max(1, (int)Math.Round(size.Width * scale));
        int h = Math.Max(1, (int)Math.Round(size.Height * scale));
        return new Rectangle((Width - w) / 2, (Height - h) / 2, w, h);
    }

    /// <summary> Pixel art at any size: enlarge by a whole factor with hard edges, then smooth to the exact size (even pixels, no blur). </summary>
    private static void DrawPixelArt(Graphics g, Bitmap src, Rectangle from, Rectangle to)
    {
        int k = (int)Math.Ceiling(Math.Max((float)to.Width / from.Width, (float)to.Height / from.Height));
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        if (k <= 1)
        {
            g.DrawImage(src, to, from, GraphicsUnit.Pixel);
            return;
        }
        using var up = new Bitmap(from.Width * k, from.Height * k);
        using (var gu = Graphics.FromImage(up))
        {
            gu.InterpolationMode = InterpolationMode.NearestNeighbor;
            gu.PixelOffsetMode = PixelOffsetMode.Half;
            gu.DrawImage(src, new Rectangle(Point.Empty, up.Size), from, GraphicsUnit.Pixel);
        }
        g.DrawImage(up, to);
    }

    /// <summary>
    /// Pixels where the full sprite differs from the plain Pokémon: the marks. Translucent ones (tera / encounter
    /// backgrounds and stripes) go under the Pokémon, opaque ones (item, shiny star, flags) over it.
    /// </summary>
    private static (Bitmap Under, Bitmap Over) SplitMarks(Bitmap full, Bitmap subject)
    {
        var a = ReadPixels(full);
        var b = ReadPixels(subject);
        var under = new int[a.Length];
        var over = new int[a.Length];
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] == b[i])
                continue;
            if (((uint)a[i] >> 24) >= 200)
                over[i] = a[i];
            else
                under[i] = a[i];
        }
        return (ToBitmap(under, full.Width, full.Height), ToBitmap(over, full.Width, full.Height));
    }

    private static int[] ReadPixels(Bitmap bmp)
    {
        var data = bmp.LockBits(new Rectangle(Point.Empty, bmp.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var px = new int[bmp.Width * bmp.Height];
            for (int y = 0; y < bmp.Height; y++)
                Marshal.Copy(data.Scan0 + (y * data.Stride), px, y * bmp.Width, bmp.Width);
            return px;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    private static Bitmap ToBitmap(int[] px, int width, int height)
    {
        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < height; y++)
            Marshal.Copy(px, y * width, data.Scan0 + (y * data.Stride), width);
        bmp.UnlockBits(data);
        return bmp;
    }

    /// <summary> Bounding box of the pixels that are not (nearly) transparent; the whole image if there are none. </summary>
    private static Rectangle OpaqueBounds(Bitmap bmp)
    {
        var px = ReadPixels(bmp);
        int left = bmp.Width, top = bmp.Height, right = -1, bottom = -1;
        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                if (((uint)px[(y * bmp.Width) + x] >> 24) < 16)
                    continue;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = y;
            }
        }
        return right < 0 ? new Rectangle(Point.Empty, bmp.Size) : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _tileSprite?.Dispose();
        base.Dispose(disposing);
    }
}
