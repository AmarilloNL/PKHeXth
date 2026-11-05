using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PKHeX.WinForms.Theming;

/// <summary>
/// Repaints the 1px non-client border of single-border inputs (text boxes, numeric fields) in palette colors:
/// subtle at rest, brighter on hover, accent while focused.
/// </summary>
internal sealed class BorderPainter : NativeWindow
{
    private static readonly ConditionalWeakTable<Control, BorderPainter> Attached = new();

    private readonly Control _c;
    private bool _hover;

    private BorderPainter(Control c)
    {
        _c = c;
        c.HandleCreated += (_, _) => AssignHandle(c.Handle);
        c.GotFocus += (_, _) => Redraw();
        c.LostFocus += (_, _) => Redraw();
        c.EnabledChanged += (_, _) => Redraw();
        c.MouseEnter += (_, _) => { _hover = true; Redraw(); };
        c.MouseLeave += (_, _) => { _hover = false; Redraw(); };
        if (c.IsHandleCreated)
            AssignHandle(c.Handle);
    }

    public static void Attach(Control c)
    {
        if (!Attached.TryGetValue(c, out var existing))
            Attached.Add(c, existing = new BorderPainter(c));
        existing.Redraw();
    }

    private void Redraw()
    {
        if (_c.IsHandleCreated)
            RedrawWindow(_c.Handle, IntPtr.Zero, IntPtr.Zero, RDW_FRAME | RDW_INVALIDATE);
    }

    private const int WM_PAINT = 0x000F;
    private const int WM_NCPAINT = 0x0085;
    private const uint RDW_INVALIDATE = 0x0001;
    private const uint RDW_FRAME = 0x0400;

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg is WM_NCPAINT or WM_PAINT && !_c.IsDisposed) // .NET may draw the frame in either pass
            Paint();
    }

    private void Paint()
    {
        var p = Theme.Current;
        bool focused = _c.ContainsFocus;
        var color = !_c.Enabled ? p.Mix(p.Border, p.Surface0, 0.4f)
            : focused ? p.Accent
            : _hover ? p.Mix(p.Border, p.TextPrimary, 0.25f)
            : p.Border;

        var hdc = GetWindowDC(Handle);
        if (hdc == IntPtr.Zero)
            return;
        try
        {
            GetWindowRect(Handle, out var r);
            using var g = Graphics.FromHdc(hdc);
            using var pen = new Pen(color);
            g.DrawRectangle(pen, 0, 0, r.Right - r.Left - 1, r.Bottom - r.Top - 1);
        }
        finally
        {
            ReleaseDC(Handle, hdc);
        }
    }

    private static readonly ConditionalWeakTable<RichTextBox, object> Framed = new();

    /// <summary>
    /// Removes a rich text box's native frame, pads its text, and has the parent draw a themed rounded frame around it.
    /// </summary>
    public static void FrameRichText(RichTextBox rtb)
    {
        rtb.BorderStyle = BorderStyle.None;
        if (!Framed.TryAdd(rtb, rtb))
            return;

        void Pad()
        {
            if (!rtb.IsHandleCreated || !rtb.Multiline)
                return;
            var r = Rectangle.Inflate(rtb.ClientRectangle, -rtb.LogicalToDeviceUnits(6), -rtb.LogicalToDeviceUnits(4));
            var rect = new RECT { Left = r.Left, Top = r.Top, Right = r.Right, Bottom = r.Bottom };
            SendMessage(rtb.Handle, EM_SETRECT, IntPtr.Zero, ref rect);
        }
        void Hook(Control? parent)
        {
            if (parent is null)
                return;
            parent.Paint += (_, e) =>
            {
                if (rtb.Parent != parent || !rtb.Visible)
                    return;
                var p = Theme.Current;
                var frame = Rectangle.Inflate(rtb.Bounds, 1, 1);
                frame.Width--;
                frame.Height--;
                Shapes.Fill(e.Graphics, frame, rtb.LogicalToDeviceUnits(4), Color.Empty, rtb.Focused ? p.Accent : p.Border);
            };
            parent.Invalidate();
        }

        rtb.HandleCreated += (_, _) => Pad();
        rtb.Resize += (_, _) => Pad();
        rtb.GotFocus += (_, _) => rtb.Parent?.Invalidate(Rectangle.Inflate(rtb.Bounds, 2, 2));
        rtb.LostFocus += (_, _) => rtb.Parent?.Invalidate(Rectangle.Inflate(rtb.Bounds, 2, 2));
        rtb.ParentChanged += (_, _) => Hook(rtb.Parent);
        Hook(rtb.Parent);
        Pad();
    }

    private const int EM_SETRECT = 0x00B3;
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr w, ref RECT r);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool RedrawWindow(IntPtr hWnd, IntPtr rect, IntPtr rgn, uint flags);
}
