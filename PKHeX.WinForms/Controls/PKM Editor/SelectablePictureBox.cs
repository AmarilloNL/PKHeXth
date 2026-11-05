using System;
using System.Drawing;
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

        if (BackgroundImage is { } bg)
            g.DrawImage(bg, (Width - bg.Width) / 2, (Height - bg.Height) / 2, bg.Width, bg.Height);
    }

    protected override void OnPaint(PaintEventArgs pe)
    {
        base.OnPaint(pe);
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
}
