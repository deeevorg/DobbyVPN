using System.Drawing;
using System.Windows.Forms;
using RismanNG.Application;
using RismanNG.Domain;
using RismanNG.Infrastructure;

namespace RismanNG;

public sealed class SubscriptionDialog : Form
{
    private readonly SubscriptionService _subscriptions;
    private readonly TextBox _url = new() { Dock = DockStyle.Fill, PlaceholderText = "https://example.com/subscription", BackColor = Color.FromArgb(34, 43, 61), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10) };
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(25, 31, 45), ForeColor = Color.White, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10), IntegralHeight = false };
    private readonly Label _result = new() { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(8, 9, 8, 0), ForeColor = Color.FromArgb(148, 163, 184) };
    private readonly Button _add = new() { Text = "Add and import", Width = 130, Height = 36 };
    private readonly Button _paste = new() { Text = "Paste", Width = 76, Height = 36 };
    private readonly Button _refresh = new() { Text = "Refresh selected", Width = 140, Height = 38 };
    private readonly Button _close = new() { Text = "Close", Width = 90, Height = 38 };
    public bool ProfilesChanged { get; private set; }

    public SubscriptionDialog(SubscriptionService subscriptions)
    {
        _subscriptions = subscriptions;
        Text = "PingVPN · Subscriptions";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(680, 440);
        MinimumSize = new Size(540, 360);
        BackColor = Color.FromArgb(14, 18, 28);
        ForeColor = Color.White;
        Padding = new Padding(20);

        var title = new Label { Text = "Subscription links", Dock = DockStyle.Top, Height = 44, Font = new Font("Segoe UI Semibold", 17), ForeColor = Color.White };
        var instructions = new Label { Text = "Paste an HTTPS subscription URL to import its supported profiles.", Dock = DockStyle.Top, Height = 31, Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(148, 163, 184) };
        var input = new TableLayoutPanel { Dock = DockStyle.Top, Height = 49, ColumnCount = 3, Margin = Padding.Empty };
        input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
        input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138));
        _url.Margin = new Padding(0, 3, 8, 3);
        _paste.Margin = new Padding(0, 3, 8, 3);
        _add.Margin = new Padding(0, 3, 0, 3);
        input.Controls.Add(_url, 0, 0); input.Controls.Add(_paste, 1, 0); input.Controls.Add(_add, 2, 0);

        var listTitle = new Label { Text = "SAVED SUBSCRIPTIONS", Dock = DockStyle.Top, Height = 32, Padding = new Padding(3, 9, 0, 0), Font = new Font("Segoe UI Semibold", 9), ForeColor = Color.FromArgb(148, 163, 184) };
        var listPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(25, 31, 45), Padding = new Padding(10) };
        listPanel.Controls.Add(_list);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft };
        footer.Controls.Add(_close); footer.Controls.Add(_refresh);

        Controls.Add(listPanel);
        Controls.Add(_result);
        Controls.Add(footer);
        Controls.Add(listTitle);
        Controls.Add(input);
        Controls.Add(instructions);
        Controls.Add(title);

        Style(_add, Color.FromArgb(67, 142, 255));
        Style(_paste, Color.FromArgb(34, 43, 61));
        Style(_refresh, Color.FromArgb(34, 43, 61));
        Style(_close, Color.FromArgb(34, 43, 61));
        _paste.Click += (_, _) => { if (Clipboard.ContainsText()) _url.Text = Clipboard.GetText().Trim(); };
        _add.Click += async (_, _) => await RunAsync(() => _subscriptions.AddAndRefreshAsync(_url.Text));
        _refresh.Click += async (_, _) =>
        {
            if (_list.SelectedItem is SubscriptionEntry entry)
                await RunAsync(() => _subscriptions.RefreshAsync(entry.Item));
        };
        _close.Click += (_, _) => Close();
        _list.SelectedIndexChanged += (_, _) => _refresh.Enabled = _list.SelectedItem is SubscriptionEntry;
        _refresh.Enabled = false;
        Load += async (_, _) => await ReloadAsync();
    }

    private async Task RunAsync(Func<Task<ImportResult>> action)
    {
        SetBusy(true);
        _result.ForeColor = Color.FromArgb(148, 163, 184);
        _result.Text = "Downloading and importing profiles…";
        try
        {
            var imported = await action();
            ProfilesChanged = true;
            _result.ForeColor = Color.FromArgb(74, 222, 128);
            _result.Text = $"Imported {imported.Added} profile(s)." + (imported.Warning is null ? "" : $" {imported.Warning}");
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            _result.ForeColor = Color.FromArgb(248, 113, 113);
            _result.Text = ex is HttpRequestException ? "Could not download the subscription. Check its URL and internet access." : SafeError.ForUser(ex);
        }
        finally { SetBusy(false); }
    }

    private async Task ReloadAsync()
    {
        var items = (await _subscriptions.GetAllAsync()).Select(x => new SubscriptionEntry(x)).ToArray();
        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();
            _list.Items.AddRange(items);
            if (_list.Items.Count > 0) _list.SelectedIndex = 0;
        }
        finally { _list.EndUpdate(); }
    }

    private void SetBusy(bool busy)
    {
        _url.Enabled = !busy; _paste.Enabled = !busy; _add.Enabled = !busy;
        _list.Enabled = !busy; _refresh.Enabled = !busy && _list.SelectedItem is SubscriptionEntry;
        _close.Enabled = !busy;
        ControlBox = !busy;
    }

    private static void Style(Button button, Color color)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = color;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI Semibold", 9);
        button.Cursor = Cursors.Hand;
    }

    private sealed record SubscriptionEntry(Subscription Item)
    {
        public override string ToString()
        {
            var host = Uri.TryCreate(Item.Url, UriKind.Absolute, out var uri) ? uri.Host : "Subscription";
            var updated = Item.LastUpdated?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "Not updated";
            return $"{host}    ·    {updated}    ·    {Item.LastResult ?? "Waiting for import"}";
        }
    }
}
