using System.Drawing;
using System.Windows.Forms;

namespace RismanNG;

internal sealed class WelcomeDialog : Form
{
    private static readonly string MarkerPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RismanNG", "welcome-seen");

    internal static bool ShouldShow() => !File.Exists(MarkerPath);

    internal static void MarkSeen()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath)!);
        File.WriteAllText(MarkerPath, "1");
    }

    internal WelcomeDialog()
    {
        Text = "Welcome to PingVPN";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(660, 440);
        MinimumSize = new Size(600, 420);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(14, 18, 28);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);

        var accent = new Panel { Dock = DockStyle.Top, Height = 5, BackColor = Color.FromArgb(67, 142, 255) };
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(36, 30, 36, 28), ColumnCount = 1, RowCount = 5 };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        var eyebrow = new Label { Text = "PINGVPN  /  GETTING STARTED", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(96, 165, 250), Font = new Font("Segoe UI Semibold", 9), TextAlign = ContentAlignment.MiddleLeft };
        var title = new Label { Text = "Your connection, under your control.", Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 23), TextAlign = ContentAlignment.MiddleLeft };
        var intro = new Label { Text = "Import a configuration or subscription to get started. Xray and sing-box are included; no separate core installation is needed.", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(203, 213, 225) };
        var notice = new Label { Text = "HOW IT WORKS\nPingVPN changes the Windows system proxy while connected. Connection checks contact external IP and location services; your IP address may be visible to those services. Disconnect to restore the previous proxy settings.", Dock = DockStyle.Fill, Padding = new Padding(18, 16, 18, 12), BackColor = Color.FromArgb(25, 31, 45), ForeColor = Color.FromArgb(203, 213, 225) };

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var import = CreateButton("Import clipboard", Color.FromArgb(67, 142, 255), DialogResult.Yes, 156);
        var later = CreateButton("Explore first", Color.FromArgb(34, 43, 61), DialogResult.OK, 132);
        actions.Controls.Add(import);
        actions.Controls.Add(later);
        AcceptButton = import;
        CancelButton = later;

        content.Controls.Add(eyebrow, 0, 0);
        content.Controls.Add(title, 0, 1);
        content.Controls.Add(intro, 0, 2);
        content.Controls.Add(notice, 0, 3);
        content.Controls.Add(actions, 0, 4);
        Controls.Add(content);
        Controls.Add(accent);
    }

    private static Button CreateButton(string text, Color background, DialogResult result, int width)
    {
        var button = new Button { Text = text, Width = width, Height = 42, DialogResult = result, BackColor = background, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Margin = new Padding(8, 2, 0, 0), Font = new Font("Segoe UI Semibold", 9) };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }
}
