using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;
using RismanNG.Application;
using RismanNG.Domain;
using RismanNG.Infrastructure;

namespace RismanNG;
public partial class MainWindow : Window
{
    private readonly AppServices _services = AppServices.Create();
    private readonly ObservableCollection<Profile> _profiles = [];
    private readonly ObservableCollection<Subscription> _subscriptions = [];
    private ConnectionState _state = ConnectionState.Disconnected;
    public MainWindow() { StartupTrace.Write("MainWindow constructor entered"); InitializeComponent(); StartupTrace.Write("MainWindow XAML loaded"); Loaded += OnLoaded; Closing += (_, _) => _services.Connection.DisconnectAsync().GetAwaiter().GetResult(); }
    private async void OnLoaded(object sender, RoutedEventArgs e) { await RefreshAsync(); }
    private async Task RefreshAsync() { _profiles.Clear(); foreach (var p in await _services.Profiles.GetAllAsync()) _profiles.Add(p); _subscriptions.Clear(); foreach (var s in await _services.Subscriptions.GetAllAsync()) _subscriptions.Add(s); ProfilesList.ItemsSource = _profiles; SubscriptionsList.ItemsSource = _subscriptions; UpdateSelection(); }
    private void Show(UIElement item, string title) { foreach (var v in new[] { DashboardView, ServersView, SubscriptionsView, SettingsView }) v.Visibility = Visibility.Collapsed; item.Visibility = Visibility.Visible; PageTitle.Text = title; }
    private void Dashboard_Click(object s, RoutedEventArgs e) => Show(DashboardView, "Dashboard"); private void Servers_Click(object s, RoutedEventArgs e) => Show(ServersView, "Servers"); private void Subscriptions_Click(object s, RoutedEventArgs e) => Show(SubscriptionsView, "Subscriptions"); private void Settings_Click(object s, RoutedEventArgs e) => Show(SettingsView, "Settings");
    private async void ImportClipboard_Click(object s, RoutedEventArgs e) { try { var input = Clipboard.GetText(); var result = await _services.Import.ImportAsync(input); MessageText.Text = $"Imported {result.Added} profile(s). {result.Warning}"; await RefreshAsync(); } catch (Exception ex) { MessageText.Text = SafeError.ForUser(ex); } }
    private async void ImportFile_Click(object s, RoutedEventArgs e) { var picker = new OpenFileDialog { Filter = "Config or text|*.json;*.txt|All files|*.*" }; if (picker.ShowDialog() == true) { try { var result = await _services.Import.ImportAsync(await File.ReadAllTextAsync(picker.FileName)); MessageText.Text = $"Imported {result.Added} profile(s). {result.Warning}"; await RefreshAsync(); } catch (Exception ex) { MessageText.Text = SafeError.ForUser(ex); } } }
    private async void Delete_Click(object s, RoutedEventArgs e) { if (ProfilesList.SelectedItem is Profile p) { await _services.Profiles.DeleteAsync(p.Id); await RefreshAsync(); } }
    private async void ProfilesList_SelectionChanged(object s, System.Windows.Controls.SelectionChangedEventArgs e) { if (ProfilesList.SelectedItem is Profile p) { await _services.Profiles.SetActiveAsync(p.Id); await RefreshAsync(); } }
    private async void Connect_Click(object s, RoutedEventArgs e) { try { if (_state is ConnectionState.Connected or ConnectionState.Starting) { await _services.Connection.DisconnectAsync(); _state = ConnectionState.Disconnected; } else { _state = ConnectionState.Starting; UpdateStatus(); await _services.Connection.ConnectAsync(); _state = ConnectionState.Connected; } } catch (Exception ex) { _state = ConnectionState.Error; MessageText.Text = SafeError.ForUser(ex); } UpdateStatus(); }
    private async void AddSubscription_Click(object s, RoutedEventArgs e) { try { await _services.Subscriptions.AddAndRefreshAsync(SubscriptionUrl.Text); SubscriptionUrl.Text = ""; await RefreshAsync(); MessageText.Text = "Subscription refreshed."; } catch (Exception ex) { MessageText.Text = SafeError.ForUser(ex); } }
    private async void RefreshSubscription_Click(object s, RoutedEventArgs e) { if (SubscriptionsList.SelectedItem is Subscription sub) { try { await _services.Subscriptions.RefreshAsync(sub); await RefreshAsync(); MessageText.Text = "Subscription refreshed."; } catch (Exception ex) { MessageText.Text = SafeError.ForUser(ex); } } }
    private void UpdateSelection() { var p = _profiles.FirstOrDefault(x => x.IsActive); ActiveProfileText.Text = p is null ? "No profile selected" : $"{p.DisplayName} · {p.Protocol}"; }
    private void UpdateStatus() { StatusText.Text = _state.ToString(); ConnectButton.Content = _state == ConnectionState.Connected ? "Disconnect" : "Connect"; }
}
