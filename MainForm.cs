using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using RismanNG.Domain;
using RismanNG.Infrastructure;

namespace RismanNG;

public sealed class MainForm : Form
{
    private static readonly Color Canvas = Color.FromArgb(14, 18, 28);
    private static readonly Color Surface = Color.FromArgb(25, 31, 45);
    private static readonly Color SurfaceRaised = Color.FromArgb(34, 43, 61);
    private static readonly Color Accent = Color.FromArgb(67, 142, 255);
    private AppServices? _services;
    private AppServices Services => _services ?? throw new InvalidOperationException("Application services are still starting.");
    private readonly ListBox _profiles = new() { Dock = DockStyle.Fill, DisplayMember = nameof(Profile.DisplayName), DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 48, BorderStyle = BorderStyle.None, BackColor = Surface, ForeColor = Color.White };
    private readonly Label _status = new() { AutoSize = true, Text = "DISCONNECTED", ForeColor = Color.FromArgb(148, 163, 184), Font = new Font("Segoe UI Semibold", 9) };
    private readonly Label _connectionTitle = new() { AutoSize = true, Text = "Ready to connect", ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 17) };
    private readonly Label _connectionDetail = new() { AutoSize = true, Text = "Choose a profile and start a protected connection.", ForeColor = Color.FromArgb(148, 163, 184), Font = new Font("Segoe UI", 9) };
    private readonly Label _message = new() { AutoSize = false, Height = 44, Dock = DockStyle.Bottom, ForeColor = Color.FromArgb(250, 204, 21), Padding = new Padding(18, 8, 18, 0), TextAlign = ContentAlignment.TopLeft };
    private readonly Label _connectionPill = new() { AutoSize = true, Padding = new Padding(10, 5, 10, 5), Font = new Font("Segoe UI Semibold", 8), BackColor = Color.FromArgb(30, 41, 59) };
    private readonly Label _trafficValue = MetricValue("Waiting for traffic");
    private readonly Label _latencyValue = MetricValue("Waiting for test");
    private readonly Label _exitValue = MetricValue("Waiting for route");
    private readonly Button _connect = new() { Text = "Connect", Width = 138, Height = 42 };
    private readonly Button _import = new() { Text = "Import clipboard", Width = 130, Height = 38 };
    private readonly Button _subscriptions = new() { Text = "Subscriptions", Width = 120, Height = 38 };
    private readonly Button _manageProfiles = new() { Text = "Manage profiles", Width = 126, Height = 38 };
    private readonly Button _hide = new() { Text = "Minimize to tray", Width = 122, Height = 38 };
    private readonly NotifyIcon _tray = new();
    private readonly Icon _trayDisconnected;
    private readonly Icon _trayConnecting;
    private readonly Icon _trayConnected;
    private readonly Icon _trayError;
    private readonly ToolStripMenuItem _trayState = new() { Enabled = false };
    private readonly ToolStripMenuItem _trayIp = new() { Enabled = false };
    private readonly ToolStripMenuItem _trayTraffic = new() { Enabled = false };
    private readonly ConnectionMap _map = new() { Dock = DockStyle.Fill };
    private readonly Panel _connectVisual = new() { Dock = DockStyle.Left, Width = 148, BackColor = Canvas, Cursor = Cursors.Hand };
    private string? _exitIp; private TimeSpan? _serverLatency;
    private bool _connectionExpected; private bool _connectionStarting; private bool _suppressProfileSelection; private bool _allowExit; private bool _trayTipShown;
    private int _connectionSession;
    private Guid? _connectedProfileId;

    public MainForm()
    {
        _trayDisconnected = CreateTrayIcon(Color.FromArgb(100, 116, 139), "•"); _trayConnecting = CreateTrayIcon(Color.FromArgb(251, 191, 36), "…"); _trayConnected = CreateTrayIcon(Color.FromArgb(74, 222, 128), "✓"); _trayError = CreateTrayIcon(Color.FromArgb(248, 113, 113), "!");
        Text = "PingVPN"; StartPosition = FormStartPosition.CenterScreen; Size = new Size(980, 660); MinimumSize = new Size(820, 600); BackColor = Canvas; ForeColor = Color.White; TopMost = false;
        BuildTray(); BuildLayout();
        Load += async (_, _) =>
        {
            _services = await Task.Run(AppServices.Create);
            _services.Connection.MetricsUpdated += UpdateMetrics;
            await RefreshAsync();
            if (_profiles.Items.Count == 0 && WelcomeDialog.ShouldShow())
            {
                using var welcome = new WelcomeDialog();
                var import = welcome.ShowDialog(this) == DialogResult.Yes;
                try { WelcomeDialog.MarkSeen(); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                if (import) ImportClipboard(this, EventArgs.Empty);
            }
        };
        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized) HideToTray(); };
        FormClosing += (_, e) => { if (!_allowExit) { e.Cancel = true; HideToTray(); } else _tray.Visible = false; };
    }

    private void BuildLayout()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 84, Padding = new Padding(26, 18, 26, 12) };
        header.Paint += (_, e) => { using var brush = new LinearGradientBrush(header.ClientRectangle, Color.FromArgb(23, 35, 58), Canvas, 0f); e.Graphics.FillRectangle(brush, header.ClientRectangle); };
        var brand = new Label { Text = "PingVPN", AutoSize = true, Font = new Font("Segoe UI", 24, FontStyle.Bold), ForeColor = Color.White, Location = new Point(26, 12) };
        var subtitle = new Label { Text = "PRIVATE CONNECTION CONTROL", AutoSize = true, Font = new Font("Segoe UI Semibold", 8), ForeColor = Color.FromArgb(96, 165, 250), Location = new Point(29, 54) };
        _status.BackColor = Color.FromArgb(15, 23, 42); _status.Padding = new Padding(10, 5, 10, 5); _status.Location = new Point(850, 22); _status.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        header.Controls.Add(brand); header.Controls.Add(subtitle); header.Controls.Add(_status);

        StyleButton(_connect, Accent, Color.White); StyleButton(_import, SurfaceRaised, Color.White); StyleButton(_subscriptions, SurfaceRaised, Color.White); StyleButton(_manageProfiles, SurfaceRaised, Color.White); StyleButton(_hide, SurfaceRaised, Color.FromArgb(203, 213, 225));
        _import.Click += ImportClipboard; _subscriptions.Click += ManageSubscriptions; _manageProfiles.Click += ManageProfiles; _connect.Click += Connect; _hide.Click += (_, _) => HideToTray();
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 68, Padding = new Padding(26, 14, 26, 10), BackColor = Canvas, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        actions.Controls.Add(_connect); actions.Controls.Add(_import); actions.Controls.Add(_subscriptions); actions.Controls.Add(_manageProfiles); actions.Controls.Add(_hide);

        var sidebarTitle = new Label { Text = "PROFILES", Dock = DockStyle.Top, Height = 42, Padding = new Padding(17, 13, 0, 0), ForeColor = Color.FromArgb(148, 163, 184), Font = new Font("Segoe UI Semibold", 9) };
        var sidebar = new Panel { Dock = DockStyle.Right, Width = 260, BackColor = Surface, Padding = new Padding(10, 0, 10, 10) };
        sidebar.Controls.Add(_profiles); sidebar.Controls.Add(sidebarTitle);
        _profiles.DrawItem += DrawProfile; _profiles.SelectedIndexChanged += SelectProfile;

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 8, 24, 12), BackColor = Canvas };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Canvas, Margin = Padding.Empty, Padding = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 142)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var card = new Panel { Dock = DockStyle.Fill, BackColor = Canvas, Margin = Padding.Empty };
        _connectVisual.Paint += DrawConnectionCard; _connectVisual.Click += (_, _) => { if (_connect.Enabled) Connect(_connect, EventArgs.Empty); };
        var statusPanel = new Panel { Dock = DockStyle.Fill, BackColor = Canvas };
        statusPanel.Controls.Add(_connectionPill); statusPanel.Controls.Add(_connectionDetail); statusPanel.Controls.Add(_connectionTitle);
        _connectionTitle.AutoSize = false; _connectionTitle.Height = 36; _connectionTitle.AutoEllipsis = true;
        _connectionDetail.AutoSize = false; _connectionDetail.Height = 32; _connectionDetail.AutoEllipsis = true;
        statusPanel.Resize += (_, _) => { _connectionTitle.Width = Math.Max(80, statusPanel.ClientSize.Width - 8); _connectionDetail.Width = _connectionTitle.Width; };
        _connectionTitle.Location = new Point(0, 16); _connectionDetail.Location = new Point(0, 52); _connectionPill.Location = new Point(0, 82);
        card.Controls.Add(statusPanel); card.Controls.Add(_connectVisual);
        var diagnosticTitle = new Label { Text = "LIVE CONNECTION METRICS", Dock = DockStyle.Fill, Padding = new Padding(2, 6, 0, 0), ForeColor = Color.FromArgb(148, 163, 184), Font = new Font("Segoe UI Semibold", 9), Margin = Padding.Empty };
        var metricsGrid = new TableLayoutPanel { Dock = DockStyle.Fill, Height = 82, ColumnCount = 3, RowCount = 1, BackColor = Canvas, Padding = Padding.Empty, Margin = Padding.Empty };
        metricsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f)); metricsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f)); metricsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        metricsGrid.Controls.Add(CreateMetricCard("LIVE TRAFFIC", _trafficValue), 0, 0); metricsGrid.Controls.Add(CreateMetricCard("SERVER TCP", _latencyValue), 1, 0); metricsGrid.Controls.Add(CreateMetricCard("CURRENT EXIT IP", _exitValue), 2, 0);
        _map.Dock = DockStyle.Fill; _map.Margin = new Padding(0, 8, 0, 0);
        layout.Controls.Add(card, 0, 0); layout.Controls.Add(diagnosticTitle, 0, 1); layout.Controls.Add(metricsGrid, 0, 2); layout.Controls.Add(_map, 0, 3); content.Controls.Add(layout);
        Controls.Add(content); Controls.Add(sidebar); Controls.Add(_message); Controls.Add(actions); Controls.Add(header);
        void ApplyResponsiveLayout()
        {
            var compact = ClientSize.Height < 720;
            sidebar.Width = Math.Clamp(ClientSize.Width / 4, 220, 300);
            header.Height = compact ? 72 : 84;
            actions.Height = compact ? 58 : 68;
            actions.Padding = compact ? new Padding(26, 8, 26, 4) : new Padding(26, 14, 26, 10);
            _message.Height = compact ? 34 : 44;
            layout.RowStyles[0].Height = compact ? 104 : 142;
            layout.RowStyles[1].Height = compact ? 24 : 28;
            layout.RowStyles[2].Height = compact ? 72 : 82;
            _connectVisual.Width = compact ? 108 : 148;
            _connectionTitle.Location = new Point(0, compact ? 5 : 16);
            _connectionDetail.Location = new Point(0, compact ? 38 : 52);
            _connectionPill.Location = new Point(0, compact ? 72 : 82);
            _connectionTitle.Width = Math.Max(80, statusPanel.ClientSize.Width - 8);
            _connectionDetail.Width = _connectionTitle.Width;
            _connectVisual.Invalidate();
        }
        Resize += (_, _) => ApplyResponsiveLayout();
        ApplyResponsiveLayout();
    }

    private void BuildTray()
    {
        var menu = new ContextMenuStrip { BackColor = Surface, ForeColor = Color.White };
        menu.Items.Add(_trayState); menu.Items.Add(_trayIp); menu.Items.Add(_trayTraffic); menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Show PingVPN", null, (_, _) => RestoreWindow());
        menu.Items.Add("Connect / disconnect", null, (_, _) => BeginInvoke(() => Connect(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("Exit", null, async (_, _) => await ExitApplicationAsync());
        _tray.Icon = _trayDisconnected; _tray.Text = "PingVPN · Disconnected"; _tray.ContextMenuStrip = menu; _tray.Visible = true; _tray.DoubleClick += (_, _) => RestoreWindow();
    }

    private static void StyleButton(Button button, Color backColor, Color foreColor)
    {
        button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 0; button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(backColor, .10f); button.BackColor = backColor; button.ForeColor = foreColor; button.Font = new Font("Segoe UI Semibold", 9); button.Cursor = Cursors.Hand;
    }
    private static Label MetricValue(string text) => new() { Text = text, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 12), Padding = new Padding(16, 14, 10, 0) };
    private static Panel CreateMetricCard(string caption, Label value)
    {
        var card = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Margin = new Padding(0, 0, 10, 0) };
        var label = new Label { Text = caption, Dock = DockStyle.Top, Height = 30, Padding = new Padding(16, 12, 0, 0), ForeColor = Color.FromArgb(148, 163, 184), Font = new Font("Segoe UI Semibold", 8) };
        card.Controls.Add(value); card.Controls.Add(label); return card;
    }

    private void DrawProfile(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || _profiles.Items[e.Index] is not Profile profile) return;
        var selected = (e.State & DrawItemState.Selected) != 0;
        using var background = new SolidBrush(selected ? Color.FromArgb(37, 99, 235) : Surface); e.Graphics.FillRectangle(background, e.Bounds);
        using var icon = new SolidBrush(selected ? Color.White : profile.IsActive ? Color.FromArgb(74, 222, 128) : Color.FromArgb(100, 116, 139)); e.Graphics.FillEllipse(icon, e.Bounds.X + 10, e.Bounds.Y + 18, 10, 10);
        using var nameFont = new Font("Segoe UI Semibold", 9); using var subFont = new Font("Segoe UI", 8); using var text = new SolidBrush(Color.White); using var sub = new SolidBrush(selected ? Color.FromArgb(219, 234, 254) : Color.FromArgb(148, 163, 184));
        var textWidth = Math.Max(30, e.Bounds.Width - 36);
        TextRenderer.DrawText(e.Graphics, profile.DisplayName, nameFont, new Rectangle(e.Bounds.X + 29, e.Bounds.Y + 5, textWidth, 19), Color.White, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        TextRenderer.DrawText(e.Graphics, $"{profile.Protocol}  ·  {profile.Host}:{profile.Port}", subFont, new Rectangle(e.Bounds.X + 29, e.Bounds.Y + 25, textWidth, 17), selected ? Color.FromArgb(219, 234, 254) : Color.FromArgb(148, 163, 184), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }

    private void DrawConnectionCard(object? sender, PaintEventArgs e)
    {
        var color = _connectionStarting ? Color.FromArgb(251, 191, 36) : _connectionExpected ? Color.FromArgb(74, 222, 128) : Color.FromArgb(96, 165, 250); using var glow = new SolidBrush(Color.FromArgb(20, color)); using var ring = new Pen(color, 2); using var dot = new SolidBrush(color);
        var side = Math.Max(70, Math.Min(_connectVisual.ClientSize.Width - 12, _connectVisual.ClientSize.Height - 10)); var x = (_connectVisual.ClientSize.Width - side) / 2; var y = (_connectVisual.ClientSize.Height - side) / 2;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.FillEllipse(glow, x, y, side, side); e.Graphics.DrawEllipse(ring, x + 5, y + 5, side - 10, side - 10); e.Graphics.FillEllipse(dot, x + side / 2 - 4, y + side / 2 - 4, 8, 8);
        using var timeFont = new Font("Segoe UI", 8.5f); using var actionFont = new Font("Segoe UI Semibold", 10); using var text = new SolidBrush(Color.White); using var action = new SolidBrush(color);
        var time = _connectionStarting ? "CONNECTING" : _connectionExpected ? "CONNECTED" : "READY"; var actionText = _connectionExpected ? "Stop" : "Connect"; var timeSize = e.Graphics.MeasureString(time, timeFont); var actionSize = e.Graphics.MeasureString(actionText, actionFont);
        e.Graphics.DrawString(time, timeFont, text, x + (side - timeSize.Width) / 2, y + side * .34f); e.Graphics.DrawString(actionText, actionFont, action, x + (side - actionSize.Width) / 2, y + side * .57f);
    }

    private async Task RefreshAsync()
    {
        var profiles = (await Services.Profiles.GetAllAsync()).ToList(); var selected = profiles.FirstOrDefault(x => x.IsActive); _suppressProfileSelection = true;
        try { _profiles.DataSource = profiles; _profiles.SelectedItem = selected; } finally { _suppressProfileSelection = false; }
        if (!_connectionExpected) SetDisconnected(selected is null ? "Choose a profile to begin." : $"Selected profile: {selected.DisplayName}");
    }
    private async void ImportClipboard(object? sender, EventArgs e)
    {
        SetControls(false);
        try
        {
            var input = Clipboard.GetText().Trim();
            var isSubscription = Uri.TryCreate(input, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps;
            if (isSubscription && _connectionExpected) throw new InvalidOperationException("Disconnect before refreshing a subscription.");
            var result = isSubscription ? await Services.Subscriptions.AddAndRefreshAsync(input) : await Services.Import.ImportAsync(input);
            _message.Text = $"Imported {result.Added} profile(s)." + (result.Warning is null ? "" : $" {result.Warning}");
            await RefreshAsync();
        }
        catch (Exception ex) { _message.Text = SafeError.ForUser(ex); }
        finally { SetControls(true); }
    }
    private async void ManageSubscriptions(object? sender, EventArgs e)
    {
        if (_services is null) return;
        if (_connectionExpected) { _message.Text = "Disconnect before refreshing subscriptions."; return; }
        using var dialog = new SubscriptionDialog(Services.Subscriptions);
        dialog.ShowDialog(this);
        if (dialog.ProfilesChanged) await RefreshAsync();
    }
    private async void ManageProfiles(object? sender, EventArgs e)
    {
        if (_services is null) return;
        if (_connectionExpected) { _message.Text = "Disconnect before editing or deleting profiles."; return; }
        using var dialog = new ProfileManagerDialog(Services.Profiles);
        dialog.ShowDialog(this);
        if (dialog.ProfilesChanged) await RefreshAsync();
    }
    private async void SelectProfile(object? sender, EventArgs e)
    {
        if (_suppressProfileSelection || _profiles.SelectedItem is not Profile profile) return;
        try { await Services.Profiles.SetActiveAsync(profile.Id); if (!_connectionExpected) { ResetMetrics(); SetDisconnected($"Selected profile: {profile.DisplayName}"); return; } if (_connectedProfileId == profile.Id) return; SetControls(false); SetStarting($"Switching to {profile.DisplayName}…"); _connectionExpected = false; await Services.Connection.DisconnectAsync(); ResetMetrics(); await StartConnectionAsync(profile); }
        catch (Exception ex) { await StopAfterFailureAsync(ex); } finally { SetControls(true); }
    }
    private async void Connect(object? sender, EventArgs e)
    {
        SetControls(false);
        try { if (_connect.Text == "Disconnect") { _connectionExpected = false; _connectedProfileId = null; await Services.Connection.DisconnectAsync(); ResetMetrics(); SetDisconnected("Connection stopped. System proxy was restored."); } else if (_profiles.SelectedItem is Profile profile) { await Services.Profiles.SetActiveAsync(profile.Id); await StartConnectionAsync(profile); } else throw new InvalidOperationException("Select a profile before connecting."); }
        catch (Exception ex) { await StopAfterFailureAsync(ex); } finally { SetControls(true); }
    }
    private async Task StartConnectionAsync(Profile profile)
    {
        ResetMetrics(); _connectionExpected = true; var session = ++_connectionSession; SetStarting($"Connecting to {profile.DisplayName}…"); _ = LoadUserLocationAsync(session); await Services.Connection.ConnectAsync(); _connectionStarting = false; _connectVisual.Invalidate(); _connectedProfileId = profile.Id; _connect.Text = "Disconnect"; _connect.BackColor = Color.FromArgb(220, 38, 38); _status.Text = "CONNECTED"; _status.ForeColor = Color.FromArgb(74, 222, 128); _connectionTitle.Text = "Protected connection is active"; _connectionDetail.Text = $"{profile.DisplayName} · {Services.Connection.ActiveCore} · System proxy enabled"; _connectionPill.Text = "●  CONNECTED"; _connectionPill.ForeColor = Color.FromArgb(74, 222, 128); SetTray("Connected");
    }
    private async Task StopAfterFailureAsync(Exception exception)
    {
        _connectionExpected = false; _connectionStarting = false; ++_connectionSession; _connectedProfileId = null; try { await Services.Connection.DisconnectAsync(); } catch { } ResetMetrics(); _connect.Text = "Connect"; _connect.BackColor = Accent; _status.Text = "ERROR"; _status.ForeColor = Color.FromArgb(248, 113, 113); _connectionTitle.Text = "Connection needs attention"; _connectionDetail.Text = SafeError.ForUser(exception); _connectionPill.Text = "●  ERROR"; _connectionPill.ForeColor = Color.FromArgb(248, 113, 113); _connectVisual.Invalidate(); SetTray("Connection error"); _message.Text = SafeError.ForUser(exception);
    }
    private void SetControls(bool enabled) { _connect.Enabled = enabled; _profiles.Enabled = enabled; _import.Enabled = enabled; _subscriptions.Enabled = enabled; _manageProfiles.Enabled = enabled; }
    private void SetStarting(string detail) { _connectionStarting = true; _status.Text = "CONNECTING"; _status.ForeColor = Color.FromArgb(251, 191, 36); _connectionTitle.Text = "Preparing secure route"; _connectionDetail.Text = detail; _connectionPill.Text = "●  CONNECTING"; _connectionPill.ForeColor = Color.FromArgb(251, 191, 36); _connectVisual.Invalidate(); SetTray("Connecting"); }
    private void SetDisconnected(string detail) { _connectionStarting = false; _connectionExpected = false; _status.Text = "DISCONNECTED"; _status.ForeColor = Color.FromArgb(148, 163, 184); _connectionTitle.Text = "Ready to connect"; _connectionDetail.Text = detail; _connectionPill.Text = "●  DISCONNECTED"; _connectionPill.ForeColor = Color.FromArgb(148, 163, 184); _connect.Text = "Connect"; _connect.BackColor = Accent; _connectVisual.Invalidate(); SetTray("Disconnected"); }
    private void SetTray(string state)
    {
        var nextIcon = state switch { "Connected" => _trayConnected, "Connecting" => _trayConnecting, "Disconnected" => _trayDisconnected, _ => _trayError };
        var wasVisible = _tray.Visible; if (wasVisible) _tray.Visible = false; _tray.Icon = nextIcon; if (wasVisible) _tray.Visible = true;
        _trayState.Text = $"Status: {state}"; _trayIp.Text = $"Exit IP: {_exitIp ?? "checking…"}"; _trayTraffic.Text = _trafficValue.Text;
        var ipPart = _exitIp is null ? "" : $" · IP {_exitIp}"; _tray.Text = $"PingVPN · {state}{ipPart}";
    }
    private void UpdateMetrics(object? sender, ConnectionMetrics metrics)
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(() =>
        {
            if (!_connectionExpected || IsDisposed) return;
            if (metrics.RouteLost)
            {
                _ = HandleRouteLostAsync(metrics.Error ?? "The proxy route was lost.");
                return;
            }
            if (metrics.UploadBytes > 0 || metrics.DownloadBytes > 0)
                _trafficValue.Text = $"↑ {FormatBytes(metrics.UploadBytes)}   ↓ {FormatBytes(metrics.DownloadBytes)}";
            if (metrics.ExitIp is not null)
            {
                if (_exitIp != metrics.ExitIp) _ = LoadExitLocationAsync(metrics.ExitIp, _connectionSession);
                _exitIp = metrics.ExitIp;
            }
            if (metrics.ServerLatency is not null) _serverLatency = metrics.ServerLatency;
            _latencyValue.Text = _serverLatency is null ? "Waiting for test" : $"{_serverLatency.Value.TotalMilliseconds:F0} ms";
            _exitValue.Text = _exitIp ?? "Checking route…";
            if (metrics.Error is not null) _message.Text = metrics.Error;
            SetTray(_connectionStarting ? "Connecting" : "Connected");
        });
    }
    private async Task HandleRouteLostAsync(string error)
    {
        if (!_connectionExpected) return;
        SetControls(false);
        try { await StopAfterFailureAsync(new InvalidOperationException(error)); }
        finally { SetControls(true); }
    }
    private void ResetMetrics() { _exitIp = null; _serverLatency = null; _map.User = null; _map.Exit = null; _map.Invalidate(); _trafficValue.Text = "Waiting for traffic"; _latencyValue.Text = "Waiting for test"; _exitValue.Text = "Waiting for route"; }
    private async Task LoadUserLocationAsync(int session) { try { using var handler = new HttpClientHandler { UseProxy = false }; using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) }; var ip = (await client.GetStringAsync("https://api.ipify.org")).Trim(); var location = await GeoLocator.LocateAsync(ip, CancellationToken.None); if (session == _connectionSession && _connectionExpected && !IsDisposed) { _map.User = location; _map.Invalidate(); } } catch { } }
    private async Task LoadExitLocationAsync(string ip, int session) { var location = await GeoLocator.LocateAsync(ip, CancellationToken.None); if (session == _connectionSession && _connectionExpected && !IsDisposed) { _map.Exit = location; _map.Invalidate(); } }
    private void HideToTray() { Hide(); ShowInTaskbar = false; if (!_trayTipShown) { var status = _connectionStarting ? "Connecting" : _connectionExpected ? "Connected" : "Disconnected"; var ip = _exitIp ?? "not available yet"; _tray.ShowBalloonTip(2500, $"PingVPN · {status}", $"Exit IP: {ip}\nRight-click the tray icon for status and traffic.", ToolTipIcon.Info); _trayTipShown = true; } }
    private void RestoreWindow() { Show(); ShowInTaskbar = true; WindowState = FormWindowState.Normal; Activate(); }
    private async Task ExitApplicationAsync() { SetControls(false); _connectionExpected = false; try { if (_services is not null) await Services.Connection.DisconnectAsync(); } finally { _allowExit = true; _tray.Visible = false; Close(); } }
    private static Icon CreateTrayIcon(Color color, string symbol)
    {
        using var bitmap = new Bitmap(32, 32); using var graphics = Graphics.FromImage(bitmap); graphics.SmoothingMode = SmoothingMode.AntiAlias; graphics.Clear(Color.Transparent);
        using var ring = new Pen(Color.FromArgb(100, color), 3); using var fill = new SolidBrush(color); graphics.FillEllipse(fill, 5, 5, 22, 22); graphics.DrawEllipse(ring, 2, 2, 28, 28);
        using var font = new Font("Segoe UI Symbol", symbol == "…" ? 12 : 16, FontStyle.Bold); using var text = new SolidBrush(Color.FromArgb(15, 23, 42)); var size = graphics.MeasureString(symbol, font); graphics.DrawString(symbol, font, text, 16 - size.Width / 2, 15 - size.Height / 2);
        var handle = bitmap.GetHicon(); try { using var icon = Icon.FromHandle(handle); return (Icon)icon.Clone(); } finally { DestroyIcon(handle); }
    }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool DestroyIcon(IntPtr hIcon);
    private static string FormatBytes(long value) => value < 1024 ? $"{value} B" : value < 1024 * 1024 ? $"{value / 1024d:F1} KB" : $"{value / 1024d / 1024d:F1} MB";
}
