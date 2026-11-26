using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
using RismanNG.Application;
using RismanNG.Domain;
using RismanNG.Infrastructure;

namespace RismanNG;

public sealed class ProfileManagerDialog : Form
{
    private readonly ProfileService _profiles;
    private readonly ListBox _list = new()
    {
        Dock = DockStyle.Fill, DisplayMember = nameof(Profile.DisplayName),
        BackColor = Color.FromArgb(25, 31, 45), ForeColor = Color.White,
        BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 11), ItemHeight = 30,
        IntegralHeight = false
    };
    private readonly Label _message = new()
    {
        Dock = DockStyle.Bottom, Height = 48, ForeColor = Color.FromArgb(148, 163, 184),
        Padding = new Padding(6, 9, 6, 0), AutoEllipsis = true
    };
    private readonly Button _edit = MakeButton("Edit", Color.FromArgb(67, 142, 255));
    private readonly Button _delete = MakeButton("Delete", Color.FromArgb(172, 42, 54));
    private readonly Button _activate = MakeButton("Set active", Color.FromArgb(34, 43, 61));
    private readonly Button _close = MakeButton("Close", Color.FromArgb(34, 43, 61));
    public bool ProfilesChanged { get; private set; }

    public ProfileManagerDialog(ProfileService profiles)
    {
        _profiles = profiles;
        Text = "PingVPN · Manage profiles";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(620, 500);
        MinimumSize = new Size(480, 380);
        BackColor = Color.FromArgb(14, 18, 28);
        ForeColor = Color.White;
        Padding = new Padding(20);

        var title = new Label { Text = "Manage profiles", Dock = DockStyle.Top, Height = 45, Font = new Font("Segoe UI Semibold", 17) };
        var description = new Label { Text = "Choose a profile to edit, activate, or delete.", Dock = DockStyle.Top, Height = 31, ForeColor = Color.FromArgb(148, 163, 184) };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        _edit.Width = 92; _delete.Width = 92; _activate.Width = 108; _close.Width = 92;
        buttons.Controls.Add(_edit); buttons.Controls.Add(_delete); buttons.Controls.Add(_activate); buttons.Controls.Add(_close);
        var listPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = Color.FromArgb(25, 31, 45) };
        listPanel.Controls.Add(_list);
        Controls.Add(listPanel);
        Controls.Add(_message);
        Controls.Add(buttons);
        Controls.Add(description);
        Controls.Add(title);

        _list.SelectedIndexChanged += (_, _) => UpdateButtons();
        _list.DoubleClick += async (_, _) => await EditSelectedAsync();
        _edit.Click += async (_, _) => await EditSelectedAsync();
        _delete.Click += async (_, _) => await DeleteSelectedAsync();
        _activate.Click += async (_, _) => await ActivateSelectedAsync();
        _close.Click += (_, _) => Close();
        Load += async (_, _) => await ReloadAsync(null);
        UpdateButtons();
    }

    private async Task ReloadAsync(Guid? preferred)
    {
        var profiles = (await _profiles.GetAllAsync()).ToList();
        _list.BeginUpdate();
        try
        {
            _list.DataSource = null;
            _list.DataSource = profiles;
            _list.SelectedItem = profiles.FirstOrDefault(x => x.Id == preferred) ?? profiles.FirstOrDefault(x => x.IsActive);
        }
        finally { _list.EndUpdate(); }
        UpdateButtons();
    }

    private async Task EditSelectedAsync()
    {
        if (_list.SelectedItem is not Profile selected) return;
        using var editor = new ProfileEditorDialog(selected);
        if (editor.ShowDialog(this) != DialogResult.OK || editor.UpdatedProfile is not Profile updated) return;
        await RunAsync(async () =>
        {
            await _profiles.SaveAsync(updated);
            ProfilesChanged = true;
            await ReloadAsync(updated.Id);
            _message.Text = $"Saved {updated.DisplayName}.";
        });
    }

    private async Task DeleteSelectedAsync()
    {
        if (_list.SelectedItem is not Profile selected) return;
        var subscriptionNote = selected.SubscriptionId is null ? "" : "\nThis profile comes from a subscription and may return on its next refresh.";
        var answer = MessageBox.Show(this, $"Delete '{selected.DisplayName}'?{subscriptionNote}", "Delete profile", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;
        await RunAsync(async () =>
        {
            await _profiles.DeleteAsync(selected.Id);
            ProfilesChanged = true;
            await ReloadAsync(null);
            _message.Text = $"Deleted {selected.DisplayName}.";
        });
    }

    private async Task ActivateSelectedAsync()
    {
        if (_list.SelectedItem is not Profile selected) return;
        await RunAsync(async () =>
        {
            await _profiles.SetActiveAsync(selected.Id);
            ProfilesChanged = true;
            await ReloadAsync(selected.Id);
            _message.Text = $"Active profile: {selected.DisplayName}.";
        });
    }

    private async Task RunAsync(Func<Task> action)
    {
        SetBusy(true);
        try { await action(); }
        catch (Exception ex) { _message.Text = SafeError.ForUser(ex); }
        finally { SetBusy(false); }
    }

    private void SetBusy(bool busy)
    {
        _list.Enabled = !busy; _close.Enabled = !busy; ControlBox = !busy;
        if (busy) { _edit.Enabled = false; _delete.Enabled = false; _activate.Enabled = false; }
        else UpdateButtons();
    }

    private void UpdateButtons()
    {
        var hasSelection = _list.SelectedItem is Profile;
        _edit.Enabled = hasSelection; _delete.Enabled = hasSelection;
        _activate.Enabled = _list.SelectedItem is Profile { IsActive: false };
    }

    private static Button MakeButton(string text, Color color)
    {
        var button = new Button { Text = text, Height = 38, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI Semibold", 9), Cursor = Cursors.Hand };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }
}

public sealed class ProfileEditorDialog : Form
{
    private static readonly HashSet<string> AllowedOptions = new(["type", "security", "sni", "flow", "encryption", "fp", "pbk", "sid", "spx", "path", "host", "serviceName", "headerType", "alpn", "allowInsecure", "insecure"], StringComparer.OrdinalIgnoreCase);
    private readonly Profile _original;
    private readonly TextBox _name = Input();
    private readonly TextBox _protocol = Input();
    private readonly TextBox _host = Input();
    private readonly TextBox _port = Input();
    private readonly TextBox _secret = Input();
    private readonly TextBox _options = Input(true, 88);
    private readonly TextBox _json = Input(true, 125);
    private readonly TextBox _replacement = Input(true, 70);
    private readonly Label _error = new() { Dock = DockStyle.Bottom, Height = 40, ForeColor = Color.FromArgb(248, 113, 113), AutoEllipsis = true };
    public Profile? UpdatedProfile { get; private set; }

    public ProfileEditorDialog(Profile profile)
    {
        _original = profile;
        Text = "PingVPN · Edit profile";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(690, 650);
        MinimumSize = new Size(540, 480);
        BackColor = Color.FromArgb(14, 18, 28);
        ForeColor = Color.White;
        Padding = new Padding(20);

        var title = new Label { Text = "Edit profile", Dock = DockStyle.Top, Height = 40, Font = new Font("Segoe UI Semibold", 17) };
        var source = new Label
        {
            Text = profile.SubscriptionId is null ? "Changes are saved to this profile." : "This profile belongs to a subscription. Refreshing it may replace your edits.",
            Dock = DockStyle.Top, Height = 32, ForeColor = Color.FromArgb(148, 163, 184), AutoEllipsis = true
        };
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(0, 0, 10, 12) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(fields, "Name", _name, 36);
        AddRow(fields, "Protocol", _protocol, 36);
        var hostRow = fields.RowCount; AddRow(fields, "Server", _host, 36);
        var portRow = fields.RowCount; AddRow(fields, "Port", _port, 36);
        var secretRow = fields.RowCount; AddRow(fields, "Credential", _secret, 36);
        var optionsRow = fields.RowCount; AddRow(fields, "Options", _options, 96);
        var jsonRow = fields.RowCount; AddRow(fields, "Sing-box JSON", _json, 132);
        AddRow(fields, "Replace with link / JSON", _replacement, 78);
        scroll.Controls.Add(fields);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.RightToLeft };
        var save = MakeButton("Save", Color.FromArgb(67, 142, 255));
        var cancel = MakeButton("Cancel", Color.FromArgb(34, 43, 61));
        save.Width = 100; cancel.Width = 100;
        footer.Controls.Add(save); footer.Controls.Add(cancel);
        Controls.Add(scroll); Controls.Add(_error); Controls.Add(footer); Controls.Add(source); Controls.Add(title);

        _name.Text = profile.DisplayName;
        _protocol.Text = profile.Protocol.ToString(); _protocol.ReadOnly = true;
        _host.Text = profile.Host; _port.Text = profile.Port == 0 ? "" : profile.Port.ToString();
        _secret.Text = profile.Secret; _secret.UseSystemPasswordChar = true;
        _options.Text = string.Join(Environment.NewLine, profile.Options.Select(x => $"{x.Key}={x.Value}"));
        _json.Text = profile.RawConfig ?? "";
        var isJson = profile.Protocol == Protocol.SingBoxJson;
        if (isJson)
        {
            foreach (var row in new[] { hostRow, portRow, secretRow, optionsRow }) HideRow(fields, row);
        }
        else HideRow(fields, jsonRow);
        _replacement.PlaceholderText = "Optional: paste one new profile link or a sing-box JSON config";
        save.Click += (_, _) => Save();
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        AcceptButton = save; CancelButton = cancel;
    }

    private void Save()
    {
        try
        {
            var name = _name.Text.Trim();
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Enter a profile name.");
            Profile result;
            if (!string.IsNullOrWhiteSpace(_replacement.Text))
            {
                var parsed = new ProfileImporter().Parse(_replacement.Text.Trim());
                if (parsed.Count != 1) throw new InvalidOperationException("Paste exactly one profile link or JSON configuration.");
                result = parsed[0] with { Id = _original.Id, DisplayName = name, IsActive = _original.IsActive, SubscriptionId = _original.SubscriptionId };
            }
            else if (_original.Protocol == Protocol.SingBoxJson)
            {
                using var document = JsonDocument.Parse(_json.Text);
                if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("Sing-box configuration must be a JSON object.");
                result = _original with { DisplayName = name, RawConfig = _json.Text };
            }
            else
            {
                var host = _host.Text.Trim();
                if (Uri.CheckHostName(host) == UriHostNameType.Unknown) throw new InvalidOperationException("Enter a valid server address.");
                if (!int.TryParse(_port.Text.Trim(), out var port) || port is < 1 or > 65535) throw new InvalidOperationException("Port must be between 1 and 65535.");
                var secret = _secret.Text.Trim();
                if (secret.Length == 0) throw new InvalidOperationException("Enter the UUID or password.");
                if (_original.Protocol is Protocol.Vless or Protocol.Vmess && !Guid.TryParse(secret, out _)) throw new InvalidOperationException("Enter a valid UUID.");
                if (_original.Protocol == Protocol.Shadowsocks && !secret.Contains(':')) throw new InvalidOperationException("Shadowsocks credential must be method:password.");
                var options = ParseOptions(_options.Text);
                result = _original with { DisplayName = name, Host = host, Port = port, Secret = secret, Options = options };
                var config = result.Protocol == Protocol.Hysteria2
                    ? new SingBoxConfigFactory().Create(result, 10801, 10802)
                    : new XrayConfigFactory().Create(result, 10801, 10802);
                using var generated = JsonDocument.Parse(config);
            }
            UpdatedProfile = result;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) { _error.Text = ex is JsonException ? "The JSON configuration is invalid." : SafeError.ForUser(ex); }
    }

    private static IReadOnlyDictionary<string, string> ParseOptions(string text)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf('=');
            if (separator <= 0) throw new InvalidOperationException("Write options as key=value, one per line.");
            var key = line[..separator].Trim();
            if (!AllowedOptions.Contains(key)) throw new InvalidOperationException($"Unsupported option: {key}.");
            if (!options.TryAdd(key, line[(separator + 1)..].Trim())) throw new InvalidOperationException($"Duplicate option: {key}.");
        }
        if (options.TryGetValue("type", out var transport) && transport is not ("tcp" or "ws" or "grpc")) throw new InvalidOperationException("Only TCP, WS, and gRPC transports are supported.");
        if (options.TryGetValue("security", out var security) && security is not ("none" or "tls" or "reality")) throw new InvalidOperationException("Unsupported security mode.");
        if (security == "reality" && (!options.ContainsKey("pbk") || !options.ContainsKey("sni"))) throw new InvalidOperationException("Reality needs pbk and sni.");
        if (options.TryGetValue("encryption", out var encryption) && encryption != "none") throw new InvalidOperationException("Only VLESS encryption=none is supported.");
        return options;
    }

    private static TextBox Input(bool multiline = false, int height = 30) => new()
    {
        Dock = DockStyle.Fill, Height = height, Multiline = multiline,
        ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
        BackColor = Color.FromArgb(34, 43, 61), ForeColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9.5f),
        Margin = new Padding(0, 2, 4, 4)
    };

    private static void AddRow(TableLayoutPanel fields, string title, Control editor, int height)
    {
        var row = fields.RowCount++;
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        fields.Controls.Add(new Label
        {
            Text = title, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(148, 163, 184), Font = new Font("Segoe UI Semibold", 9)
        }, 0, row);
        fields.Controls.Add(editor, 1, row);
    }

    private static void HideRow(TableLayoutPanel fields, int row)
    {
        fields.RowStyles[row].Height = 0;
        fields.GetControlFromPosition(0, row)!.Visible = false;
        fields.GetControlFromPosition(1, row)!.Visible = false;
    }

    private static Button MakeButton(string text, Color color)
    {
        var button = new Button { Text = text, Height = 36, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI Semibold", 9), Cursor = Cursors.Hand };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }
}
