using System.Drawing.Imaging;
using System.IO;

namespace VMNotify;

public partial class MainWindow
{
    private void PreviewConnectionTray(string directory) {
        var before = receiver.Status;
        var beforeState = receiver.ConnectionState;
        try {
            foreach (var state in Enum.GetValues<ConnectionState>()) {
                receiver.SetStatus("托盘状态预览", state);
                for (int i = 0; i < 100; i++) RefreshStatus();
                if (!ReferenceEquals(tray.Icon, connectionTrayIcons[state]))
                    throw new InvalidOperationException("Tray did not follow connection state: " + state);
                using var bitmap = tray.Icon!.ToBitmap();
                bitmap.Save(Path.Combine(directory, "connection-tray-" + state + ".png"), ImageFormat.Png);
            }
        } finally { receiver.SetStatus(before, beforeState); RefreshStatus(); }
    }
}
