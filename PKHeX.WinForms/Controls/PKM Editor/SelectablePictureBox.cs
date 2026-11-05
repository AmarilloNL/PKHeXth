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

    // Slot sprites sit on a mostly empty canvas; tiles show the visible part (Pokémon plus its item / shiny marks)
    // enlarged to fill the tile. Rendered once per sprite and tile size.
    private Bitmap? _tileSprite;
    private Image? _tileSource;
    private Size _tileSize;

    private Bitmap GetTileSprite(Bitmap src)
    {
        if (_tileSprite is not null && ReferenceEquals(_tileSource, src) && _tileSize == Size)
            return _tileSprite;
        _tileSprite?.Dispose();
        (_tileSource, _tileSize) = (src, Size);

        var box = OpaqueBounds(src);
        int pad = LogicalToDeviceUnits(6);
        float canvasFit = Math.Min((float)Width / src.Width, (float)Height / src.Height);
        float contentFit = Math.Min((float)(Width - (2 * pad)) / box.Width, (float)(Height - (2 * pad)) / box.Height);
        // Same zoom for most Pokémon (so sizes stay comparable); large ones shrink to stay inside the tile.
        float scale = Math.Max(Math.Min(canvasFit * 1.4f, contentFit), Math.Min(1f, canvasFit));

        int w = Math.Max(1, (int)Math.Round(box.Width * scale));
        int h = Math.Max(1, (int)Math.Round(box.Height * scale));
        var result = new Bitmap(w, h);
        using var g = Graphics.FromImage(result);
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        int k = (int)Math.Ceiling(scale);
        if (k <= 1)
        {
            g.DrawImage(src, new Rectangle(0, 0, w, h), box, GraphicsUnit.Pixel);
        }
        else
        {
            // Pixel art: enlarge by a whole factor with hard edges first, then smooth down to the final size.
            using var up = new Bitmap(box.Width * k, box.Height * k);
            using (var gu = Graphics.FromImage(up))
            {
                gu.InterpolationMode = InterpolationMode.NearestNeighbor;
                gu.PixelOffsetMode = PixelOffsetMode.Half;
                gu.DrawImage(src, new Rectangle(0, 0, up.Width, up.Height), box, GraphicsUnit.Pixel);
            }
            g.DrawImage(up, new Rectangle(0, 0, w, h));
        }
        return _tileSprite = result;
    }

    /// <summary> Bounding box of the pixels that are not (nearly) transparent; the whole image if there are none. </summary>
    private static Rectangle OpaqueBounds(Bitmap bmp)
    {
        var full = new Rectangle(Point.Empty, bmp.Size);
        var data = bmp.LockBits(full, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int left = bmp.Width, top = bmp.Height, right = -1, bottom = -1;
            var row = new byte[data.Stride];
            for (int y = 0; y < bmp.Height; y++)
            {
                Marshal.Copy(data.Scan0 + (y * data.Stride), row, 0, row.Length);
                for (int x = 0; x < bmp.Width; x++)
                {
                    if (row[(x * 4) + 3] < 16)
                        continue;
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = y;
                }
            }
            return right < 0 ? full : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _tileSprite?.Dispose();
        base.Dispose(disposing);
    }
}
