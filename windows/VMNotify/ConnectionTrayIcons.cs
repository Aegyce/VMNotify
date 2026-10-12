using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace VMNotify;

// Build each state once; polling the connection never allocates native icons.
internal sealed class ConnectionTrayIcons : IDisposable
{
    private readonly Dictionary<ConnectionState, Icon> icons = new();
    public ConnectionTrayIcons(Icon basis) {
        try {
            foreach (var state in Enum.GetValues<ConnectionState>()) icons.Add(state, Create(basis, state));
        } catch { Dispose(); throw; }
    }
    public Icon this[ConnectionState state] => icons[state];
    public void Dispose() { foreach (var icon in icons.Values) icon.Dispose(); icons.Clear(); }

    private static Icon Create(Icon basis, ConnectionState state) {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap)) {
            graphics.DrawIcon(basis, new Rectangle(0, 0, 32, 32));
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var color = state switch {
                ConnectionState.Connected => Color.FromArgb(20, 145, 80),
                ConnectionState.Connecting or ConnectionState.Degraded => Color.FromArgb(190, 125, 0),
                _ => Color.FromArgb(100, 110, 120)
            };
            using var background = new SolidBrush(color);
            using var outline = new Pen(Color.White, 1.5f);
            using var mark = new Pen(Color.White, 2.3f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            graphics.FillEllipse(background, 16, 16, 15, 15);
            graphics.DrawEllipse(outline, 16, 16, 15, 15);
            if (state == ConnectionState.Connected) graphics.DrawLines(mark, [new(20, 23), new(23, 26), new(28, 20)]);
            else if (state == ConnectionState.Disconnected) graphics.DrawLine(mark, 20, 23.5f, 27, 23.5f);
            else {
                if (state == ConnectionState.Degraded) graphics.DrawLine(mark, 23.5f, 20, 23.5f, 23.5f);
                using var white = new SolidBrush(Color.White);
                graphics.FillEllipse(white, 22.25f, state == ConnectionState.Degraded ? 26 : 22.25f, 2.5f, 2.5f);
            }
        }
        var handle = bitmap.GetHicon();
        try { using var borrowed = Icon.FromHandle(handle); return (Icon)borrowed.Clone(); }
        finally { DestroyIcon(handle); }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
