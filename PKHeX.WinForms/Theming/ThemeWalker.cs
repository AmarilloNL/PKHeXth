using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace PKHeX.WinForms.Theming;

internal static class ThemeWalker
{
    public const string OptOutTag = "notheme";

    public static void Apply(Control root, ThemePalette p)
    {
        if (root is null)
            return;
        ApplyToControl(root, p);

        if (root is Form f && f.ContextMenuStrip is { } ctx)
            StyleContextMenuStrip(ctx, p);

        foreach (Control child in root.Controls)
            Apply(child, p);
    }

    private static void ApplyToControl(Control c, ThemePalette p)
    {
        if (c.Tag is string tag && tag.Contains(OptOutTag))
            return;

        if (c is IThemedControl themed)
        {
            themed.ApplyTheme(p);
            if (c.ContextMenuStrip is { } selfCtx)
                StyleContextMenuStrip(selfCtx, p);
            return;
        }

        switch (c)
        {
            case Form form:
                form.BackColor = p.Surface0;
                form.ForeColor = p.TextPrimary;
                break;

            case MenuStrip ms:
                ms.BackColor = ms.Parent is { BackColor.A: 255 } host ? host.BackColor : p.Surface0;
                ms.ForeColor = p.TextPrimary;
                foreach (ToolStripItem i in ms.Items)
                    StyleMenuItem(i, p);
                break;

            case StatusStrip ss:
                ss.BackColor = p.Canvas;
                ss.RenderMode = ToolStripRenderMode.ManagerRenderMode;
                ss.ForeColor = p.TextMuted;
                foreach (ToolStripItem i in ss.Items)
                {
                    i.BackColor = p.Canvas;
                    if (i.ForeColor.IsSystemColor || i.ForeColor == p.TextPrimary)
                        i.ForeColor = p.TextMuted;
                }
                break;

            case ToolStrip ts:
                ts.BackColor = p.Surface0;
                ts.ForeColor = p.TextPrimary;
                foreach (ToolStripItem i in ts.Items)
                    StyleMenuItem(i, p);
                break;

            case TabControl tc:
                TabPainter.Attach(tc, p);
                break;

            case GroupBox gb:
                gb.BackColor = Color.Transparent;
                gb.ForeColor = p.TextMuted;
                if (_themedGroups.TryAdd(gb, _sentinel))
                    gb.Paint += PaintThemedGroupBox;
                gb.Invalidate();
                break;

            case Button btn:
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderColor = p.Border;
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.MouseOverBackColor = p.Surface2;
                btn.FlatAppearance.MouseDownBackColor = p.PressedSurface;
                btn.BackColor = p.Surface1;
                btn.ForeColor = p.TextPrimary;
                btn.UseVisualStyleBackColor = false;
                if (btn.BackgroundImage is null && _themedButtons.TryAdd(btn, _sentinel))
                    btn.Paint += PaintThemedButton;
                btn.Invalidate();
                break;

            case CheckBox cb:
                StyleCheckBox(cb, p);
                break;

            case RadioButton rb:
                StyleRadioButton(rb, p);
                break;

            case LinkLabel ll:
                ll.BackColor = Color.Transparent;
                ll.LinkColor = p.Accent;
                ll.ActiveLinkColor = p.AccentHover;
                ll.VisitedLinkColor = p.Accent;
                ll.ForeColor = p.TextPrimary;
                break;

            case Label lbl:
                lbl.BackColor = Color.Transparent;
                lbl.ForeColor = lbl.Text.AsSpan().TrimEnd() is [.., ':' or '：'] ? p.TextMuted : p.TextPrimary; // captions recede, values stand out
                break;

            case TextBox tb:
                tb.BackColor = p.Surface1;
                tb.ForeColor = p.TextPrimary;
                tb.BorderStyle = BorderStyle.FixedSingle;
                BorderPainter.Attach(tb);
                break;

            case MaskedTextBox mtb:
                mtb.BackColor = p.Surface1;
                mtb.ForeColor = p.TextPrimary;
                mtb.BorderStyle = BorderStyle.FixedSingle;
                BorderPainter.Attach(mtb);
                break;

            case NumericUpDown nud:
                nud.BackColor = p.Surface1;
                nud.ForeColor = p.TextPrimary;
                nud.BorderStyle = BorderStyle.FixedSingle;
                BorderPainter.Attach(nud);
                break;

            case ComboBox cbx:
                cbx.BackColor = p.Surface1;
                cbx.ForeColor = p.TextPrimary;
                // Native chrome reads better for editable combos, but native list-only combos can render with the
                // light system theme in dark mode, so those are flat + owner-drawn.
                if (cbx.DropDownStyle != ComboBoxStyle.DropDownList)
                {
                    cbx.FlatStyle = FlatStyle.Standard;
                }
                else if (cbx.DrawMode == DrawMode.Normal || _themedCombos.TryGetValue(cbx, out _))
                {
                    cbx.FlatStyle = FlatStyle.Flat;
                    cbx.DrawMode = DrawMode.OwnerDrawFixed;
                    if (_themedCombos.TryAdd(cbx, _sentinel))
                        cbx.DrawItem += DrawThemedComboItem;
                }
                BorderPainter.Attach(cbx);
                break;

            case ListBox lb:
                lb.BackColor = p.Surface1;
                lb.ForeColor = p.TextPrimary;
                lb.BorderStyle = BorderStyle.FixedSingle;
                break;

            case ListView lv:
                lv.BackColor = p.Surface1;
                lv.ForeColor = p.TextPrimary;
                lv.BorderStyle = BorderStyle.FixedSingle;
                break;

            case TreeView tv:
                tv.BackColor = p.Surface1;
                tv.ForeColor = p.TextPrimary;
                tv.BorderStyle = BorderStyle.FixedSingle;
                tv.LineColor = p.Border;
                break;

            case DataGridView dgv:
                StyleDataGridView(dgv, p);
                break;

            case PropertyGrid pg:
                pg.BackColor = p.Surface0;
                pg.ViewBackColor = p.Surface1;
                pg.ViewForeColor = p.TextPrimary;
                pg.ViewBorderColor = p.Border;
                pg.CategoryForeColor = p.TextMuted;
                pg.CategorySplitterColor = p.Border;
                pg.LineColor = p.Border;
                pg.HelpBackColor = p.Surface1;
                pg.HelpForeColor = p.TextPrimary;
                pg.HelpBorderColor = p.Border;
                pg.CommandsBackColor = p.Surface1;
                pg.CommandsForeColor = p.TextPrimary;
                pg.CommandsBorderColor = p.Border;
                pg.DisabledItemForeColor = p.TextDisabled;
                pg.SelectedItemWithFocusBackColor = p.Accent;
                pg.SelectedItemWithFocusForeColor = p.AccentText;
                break;

            case SplitContainer sc:
                sc.BackColor = p.Canvas;
                sc.Panel1.BackColor = p.Canvas;
                sc.Panel2.BackColor = p.Canvas;
                break;

            case ProgressBar pb:
                pb.BackColor = p.Surface2;
                pb.ForeColor = p.Accent;
                break;

            case TrackBar tb:
                tb.BackColor = p.Surface0;
                tb.ForeColor = p.Accent;
                break;

            case ScrollBar sb:
                sb.BackColor = p.Surface2;
                sb.ForeColor = p.TextPrimary;
                break;

            case PictureBox pic:
                if (pic.BackColor != Color.Transparent)
                    pic.BackColor = p.Surface0;
                break;

            case Panel pan:
                if (pan.BackColor == SystemColors.Control || pan.BackColor == Color.White)
                    pan.BackColor = p.Surface0;
                pan.ForeColor = p.TextPrimary;
                break;

            case UserControl uc:
                uc.BackColor = p.Surface0;
                uc.ForeColor = p.TextPrimary;
                break;
        }

        if (c.ContextMenuStrip is { } attached)
            StyleContextMenuStrip(attached, p);
    }

    private static void StyleMenuItem(ToolStripItem item, ThemePalette p)
    {
        item.ForeColor = p.TextPrimary;
        if (item is ToolStripMenuItem && item.Owner is ToolStripDropDown)
            item.Padding = new Padding(2, 3, 2, 3); // roomier dropdown rows
        switch (item)
        {
            case ToolStripMenuItem tsmi:
                tsmi.BackColor = Color.Transparent;
                foreach (ToolStripItem sub in tsmi.DropDownItems)
                    StyleMenuItem(sub, p);
                if (tsmi.DropDown is { } dd)
                {
                    dd.BackColor = p.Surface1;
                    dd.ForeColor = p.TextPrimary;
                }
                break;
            case ToolStripComboBox cb:
                cb.BackColor = p.Surface1;
                cb.ForeColor = p.TextPrimary;
                break;
            case ToolStripTextBox tb:
                tb.BackColor = p.Surface1;
                tb.ForeColor = p.TextPrimary;
                break;
        }
    }

    private static void StyleContextMenuStrip(ContextMenuStrip ctx, ThemePalette p)
    {
        ctx.BackColor = p.Surface1;
        ctx.ForeColor = p.TextPrimary;
        foreach (ToolStripItem i in ctx.Items)
            StyleMenuItem(i, p);
    }

    private static Color ResolveParentBack(Control c, ThemePalette p)
    {
        for (var parent = c.Parent; parent is not null; parent = parent.Parent)
        {
            var bc = parent.BackColor;
            if (bc.A != 0 && bc != Color.Transparent)
                return bc;
        }
        return p.Surface0;
    }

    private static readonly ConditionalWeakTable<CheckBox, object> _themedCheckBoxes = new();
    private static readonly ConditionalWeakTable<RadioButton, object> _themedRadioButtons = new();
    private static readonly ConditionalWeakTable<Button, object> _themedButtons = new();
    private static readonly ConditionalWeakTable<ComboBox, object> _themedCombos = new();
    private static readonly ConditionalWeakTable<GroupBox, object> _themedGroups = new();

    // Replaces the etched native frame with a rounded 1px border and a semibold caption sitting in a gap.
    private static void PaintThemedGroupBox(object? sender, PaintEventArgs e)
    {
        if (sender is not GroupBox gb)
            return;
        var p = Theme.Current;
        var g = e.Graphics;
        g.Clear(ResolveParentBack(gb, p));

        using var font = new Font(gb.Font, FontStyle.Bold);
        const TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
        var size = string.IsNullOrEmpty(gb.Text) ? Size.Empty : TextRenderer.MeasureText(g, gb.Text, font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        int top = size.Height / 2;
        var frame = new Rectangle(0, top, gb.Width - 1, gb.Height - top - 1);
        Shapes.Fill(g, frame, gb.LogicalToDeviceUnits(6), Color.Empty, p.Border);
        if (size.IsEmpty)
            return;

        var caption = new Rectangle(gb.LogicalToDeviceUnits(8), 0, Math.Min(size.Width + 8, gb.Width - 16), size.Height);
        using (var gap = new SolidBrush(ResolveParentBack(gb, p)))
            g.FillRectangle(gap, caption);
        caption.Offset(4, 0);
        TextRenderer.DrawText(g, gb.Text, font, caption, gb.Enabled ? p.TextMuted : p.TextDisabled, flags);
    }

    private static void DrawThemedComboItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox cb)
            return;
        var p = Theme.Current;
        bool edit = (e.State & DrawItemState.ComboBoxEdit) != 0;
        bool selected = !edit && (e.State & DrawItemState.Selected) != 0;
        var back = selected ? p.Accent : cb.BackColor;
        var fore = !cb.Enabled ? p.TextDisabled : selected ? p.AccentText : cb.ForeColor;
        using (var bg = new SolidBrush(back))
            e.Graphics.FillRectangle(bg, e.Bounds);
        if ((uint)e.Index >= (uint)cb.Items.Count)
            return;
        var text = cb.GetItemText(cb.Items[e.Index]);
        var r = Rectangle.FromLTRB(e.Bounds.X + 3, e.Bounds.Y, e.Bounds.Right - 2, e.Bounds.Bottom);
        TextRenderer.DrawText(e.Graphics, text, e.Font ?? cb.Font, r, fore,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    // Repaints a flat button as a rounded pill-ish surface: fill, 1px border, image + text.
    private static void PaintThemedButton(object? sender, PaintEventArgs e)
    {
        if (sender is not Button b)
            return;
        var p = Theme.Current;
        var g = e.Graphics;
        var r = b.ClientRectangle;
        if (r.Width < 4 || r.Height < 4)
            return;

        using (var parent = new SolidBrush(ResolveParentBack(b, p)))
            g.FillRectangle(parent, r);

        bool hover = b.Enabled && r.Contains(b.PointToClient(Cursor.Position));
        bool pressed = hover && (Control.MouseButtons & MouseButtons.Left) != 0;
        var baseFill = b.BackColor.A == 0 ? p.Surface1 : b.BackColor;
        var fill = !b.Enabled ? p.Mix(baseFill, ResolveParentBack(b, p), 0.5f)
            : pressed ? p.PressedSurface
            : hover ? p.Mix(baseFill, p.TextPrimary, 0.08f)
            : baseFill;
        var border = b.Focused ? p.Accent : hover ? p.Mix(p.Border, p.TextPrimary, 0.15f) : p.Border;
        int radius = Math.Min(b.LogicalToDeviceUnits(6), r.Height / 2);
        Shapes.Fill(g, new Rectangle(0, 0, r.Width - 1, r.Height - 1), radius, fill, border);

        var content = Rectangle.Inflate(r, -3, -1);
        bool hasText = !string.IsNullOrEmpty(b.Text);
        if (b.Image is { } img)
        {
            int x = hasText ? content.X + 2 : (r.Width - img.Width) / 2;
            int y = (r.Height - img.Height) / 2;
            if (b.Enabled)
                g.DrawImage(img, x, y, img.Width, img.Height);
            else
                ControlPaint.DrawImageDisabled(g, img, x, y, fill);
            if (hasText)
                content = Rectangle.FromLTRB(x + img.Width + 2, content.Top, content.Right, content.Bottom);
        }
        if (!hasText)
            return;

        var flags = TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | (b.UseMnemonic ? 0 : TextFormatFlags.NoPrefix);
        flags |= b.TextAlign switch
        {
            ContentAlignment.TopLeft => TextFormatFlags.Top | TextFormatFlags.Left,
            ContentAlignment.TopCenter => TextFormatFlags.Top | TextFormatFlags.HorizontalCenter,
            ContentAlignment.TopRight => TextFormatFlags.Top | TextFormatFlags.Right,
            ContentAlignment.MiddleLeft => TextFormatFlags.VerticalCenter | TextFormatFlags.Left,
            ContentAlignment.MiddleRight => TextFormatFlags.VerticalCenter | TextFormatFlags.Right,
            ContentAlignment.BottomLeft => TextFormatFlags.Bottom | TextFormatFlags.Left,
            ContentAlignment.BottomCenter => TextFormatFlags.Bottom | TextFormatFlags.HorizontalCenter,
            ContentAlignment.BottomRight => TextFormatFlags.Bottom | TextFormatFlags.Right,
            _ => TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter,
        };
        var text = !b.Enabled ? p.TextDisabled : b.ForeColor.IsSystemColor ? p.TextPrimary : b.ForeColor;
        TextRenderer.DrawText(g, b.Text, b.Font, content, text, flags);
    }
    private static readonly object _sentinel = new();

    private static void StyleCheckBox(CheckBox cb, ThemePalette p)
    {
        cb.BackColor = ResolveParentBack(cb, p);
        cb.ForeColor = p.TextPrimary;
        cb.FlatStyle = FlatStyle.Flat;
        cb.FlatAppearance.BorderColor = p.Border;
        cb.FlatAppearance.CheckedBackColor = p.Accent;

        // CheckBox.Appearance.Button can owner-theme correctly; Normal appearance has a Windows-drawn
        // glyph that ignores palette colors. Hook a Paint override to draw a themed glyph.
        if (cb.Appearance == Appearance.Normal && _themedCheckBoxes.TryAdd(cb, _sentinel))
            cb.Paint += PaintThemedCheckBox;
        cb.Invalidate();
    }

    private static void StyleRadioButton(RadioButton rb, ThemePalette p)
    {
        rb.BackColor = ResolveParentBack(rb, p);
        rb.ForeColor = p.TextPrimary;
        rb.FlatStyle = FlatStyle.Flat;
        rb.FlatAppearance.BorderColor = p.Border;
        rb.FlatAppearance.CheckedBackColor = p.Accent;

        if (rb.Appearance == Appearance.Normal && _themedRadioButtons.TryAdd(rb, _sentinel))
            rb.Paint += PaintThemedRadioButton;
        rb.Invalidate();
    }

    private static void PaintThemedCheckBox(object? sender, PaintEventArgs e)
    {
        if (sender is not CheckBox cb)
            return;
        var pal = Theme.Current;

        int size = (int)Math.Round(13 * (cb.DeviceDpi / 96.0));
        int y = (cb.ClientSize.Height - size) / 2;
        int x = 0;
        if (cb.CheckAlign == ContentAlignment.MiddleRight || cb.CheckAlign == ContentAlignment.TopRight || cb.CheckAlign == ContentAlignment.BottomRight)
            x = cb.ClientSize.Width - size;
        if (cb.CheckAlign == ContentAlignment.TopCenter || cb.CheckAlign == ContentAlignment.BottomCenter || cb.CheckAlign == ContentAlignment.MiddleCenter)
            x = (cb.ClientSize.Width - size) / 2;

        var box = new Rectangle(x, y, size, size);
        var g = e.Graphics;

        // Erase the default glyph with the control's BackColor.
        using (var bg = new SolidBrush(cb.BackColor))
            g.FillRectangle(bg, Rectangle.Inflate(box, 2, 2));

        var fill = cb.Checked ? pal.Accent : pal.Surface1;
        Shapes.Fill(g, box with { Width = box.Width - 1, Height = box.Height - 1 }, 3, fill, cb.Checked ? pal.Accent : pal.TextDisabled);

        if (cb.CheckState == CheckState.Checked)
        {
            DrawCheckmark(g, box, pal.AccentText);
        }
        else if (cb.CheckState == CheckState.Indeterminate)
        {
            using var ind = new SolidBrush(pal.TextMuted);
            var pad = Math.Max(2, size / 4);
            g.FillRectangle(ind, box.X + pad, box.Y + pad, box.Width - (pad * 2), box.Height - (pad * 2));
        }
    }

    private static void PaintThemedRadioButton(object? sender, PaintEventArgs e)
    {
        if (sender is not RadioButton rb)
            return;
        var pal = Theme.Current;

        int size = (int)Math.Round(13 * (rb.DeviceDpi / 96.0));
        int y = (rb.ClientSize.Height - size) / 2;
        int x = 0;
        if (rb.CheckAlign == ContentAlignment.MiddleRight || rb.CheckAlign == ContentAlignment.TopRight || rb.CheckAlign == ContentAlignment.BottomRight)
            x = rb.ClientSize.Width - size;
        if (rb.CheckAlign == ContentAlignment.TopCenter || rb.CheckAlign == ContentAlignment.BottomCenter || rb.CheckAlign == ContentAlignment.MiddleCenter)
            x = (rb.ClientSize.Width - size) / 2;

        var box = new Rectangle(x, y, size, size);
        var g = e.Graphics;
        var prevSmoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var bg = new SolidBrush(rb.BackColor))
            g.FillRectangle(bg, Rectangle.Inflate(box, 2, 2));

        var fill = rb.Checked ? pal.Accent : pal.Surface1;
        using (var fillBrush = new SolidBrush(fill))
            g.FillEllipse(fillBrush, box);
        using (var pen = new Pen(pal.Border))
            g.DrawEllipse(pen, box.X, box.Y, box.Width - 1, box.Height - 1);

        if (rb.Checked)
        {
            int pad = Math.Max(2, size / 4);
            using var dot = new SolidBrush(pal.AccentText);
            g.FillEllipse(dot, box.X + pad, box.Y + pad, box.Width - (pad * 2), box.Height - (pad * 2));
        }

        g.SmoothingMode = prevSmoothing;
    }

    private static void DrawCheckmark(Graphics g, Rectangle box, Color color)
    {
        var prev = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float w = box.Width;
        float h = box.Height;
        var pts = new[]
        {
            new PointF(box.X + (w * 0.20f), box.Y + (h * 0.52f)),
            new PointF(box.X + (w * 0.44f), box.Y + (h * 0.74f)),
            new PointF(box.X + (w * 0.80f), box.Y + (h * 0.28f)),
        };
        using var pen = new Pen(color, Math.Max(1.5f, w / 8f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLines(pen, pts);
        g.SmoothingMode = prev;
    }

    private static void StyleDataGridView(DataGridView dgv, ThemePalette p)
    {
        dgv.EnableHeadersVisualStyles = false;
        dgv.BackgroundColor = p.Surface1;
        dgv.GridColor = p.Border;
        dgv.BorderStyle = BorderStyle.FixedSingle;
        dgv.ForeColor = p.TextPrimary;
        dgv.DefaultCellStyle.BackColor = p.Surface1;
        dgv.DefaultCellStyle.ForeColor = p.TextPrimary;
        dgv.DefaultCellStyle.SelectionBackColor = p.Accent;
        dgv.DefaultCellStyle.SelectionForeColor = p.AccentText;
        dgv.AlternatingRowsDefaultCellStyle.BackColor = p.Surface2;
        dgv.AlternatingRowsDefaultCellStyle.ForeColor = p.TextPrimary;
        dgv.ColumnHeadersDefaultCellStyle.BackColor = p.Surface0;
        dgv.ColumnHeadersDefaultCellStyle.ForeColor = p.TextPrimary;
        dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = p.Surface2;
        dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = p.TextPrimary;
        dgv.RowHeadersDefaultCellStyle.BackColor = p.Surface0;
        dgv.RowHeadersDefaultCellStyle.ForeColor = p.TextMuted;
        dgv.RowHeadersDefaultCellStyle.SelectionBackColor = p.Surface2;
        dgv.RowHeadersDefaultCellStyle.SelectionForeColor = p.TextPrimary;
    }
}
