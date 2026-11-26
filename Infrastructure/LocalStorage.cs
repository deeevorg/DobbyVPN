using System.Security.Cryptography;
using System.Text.Json;
using RismanNG.Application;
using RismanNG.Domain;
namespace RismanNG.Infrastructure;
public sealed class LocalStorage : IProfileRepository, ISubscriptionRepository
{
    private readonly string _directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RismanNG");
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string ProfilesPath => Path.Combine(_directory, "profiles.protected"); private string SubscriptionsPath => Path.Combine(_directory, "subscriptions.protected");
    Task<IReadOnlyList<Profile>> IProfileRepository.GetAllAsync(CancellationToken ct) => ReadAsync<Profile>(ProfilesPath, ct);
    Task<IReadOnlyList<Subscription>> ISubscriptionRepository.GetAllAsync(CancellationToken ct) => ReadAsync<Subscription>(SubscriptionsPath, ct);
    public async Task SaveAsync(Profile profile, CancellationToken ct = default) { var all = (await ((IProfileRepository)this).GetAllAsync(ct)).Where(x => x.Id != profile.Id).ToList(); all.Add(profile); await WriteAsync(ProfilesPath, all, ct); }
    public async Task SaveAsync(Subscription item, CancellationToken ct = default) { var all = (await ((ISubscriptionRepository)this).GetAllAsync(ct)).Where(x => x.Id != item.Id).ToList(); all.Add(item); await WriteAsync(SubscriptionsPath, all, ct); }
    public async Task DeleteAsync(Guid id, CancellationToken ct = default) { var all = (await ((IProfileRepository)this).GetAllAsync(ct)).Where(x => x.Id != id).ToList(); await WriteAsync(ProfilesPath, all, ct); }
    public async Task SetActiveAsync(Guid id, CancellationToken ct = default) { var all = (await ((IProfileRepository)this).GetAllAsync(ct)).Select(x => x with { IsActive = x.Id == id }).ToList(); await WriteAsync(ProfilesPath, all, ct); }
    public async Task ReplaceSubscriptionAsync(Guid subscriptionId, IReadOnlyList<Profile> incoming, CancellationToken ct = default)
    {
        var all = (await ((IProfileRepository)this).GetAllAsync(ct)).ToList();
        var previous = all.Where(x => x.SubscriptionId == subscriptionId).ToList();
        var keep = all.Where(x => x.SubscriptionId != subscriptionId).ToList();
        var oldSubscriptionWasActive = previous.Any(x => x.IsActive);
        foreach (var item in incoming)
        {
            var match = previous.FirstOrDefault(old => SameEndpoint(old, item));
            if (match is not null) previous.Remove(match);
            keep.Add(item with { Id = match?.Id ?? item.Id, IsActive = match?.IsActive ?? false, SubscriptionId = subscriptionId });
        }
        if (oldSubscriptionWasActive && !keep.Any(x => x.IsActive) && incoming.Count > 0)
        {
            var first = keep.FindIndex(x => x.SubscriptionId == subscriptionId);
            keep[first] = keep[first] with { IsActive = true };
        }
        await WriteAsync(ProfilesPath, keep, ct);
    }
    private static bool SameEndpoint(Profile left, Profile right) =>
        left.Protocol == right.Protocol && left.Host == right.Host && left.Port == right.Port &&
        left.Secret == right.Secret && left.RawConfig == right.RawConfig &&
        left.Options.Count == right.Options.Count && left.Options.All(x => right.Options.TryGetValue(x.Key, out var value) && value == x.Value);
    private async Task<IReadOnlyList<T>> ReadAsync<T>(string path, CancellationToken ct) { if (!File.Exists(path)) return []; await _gate.WaitAsync(ct); try { var bytes = await File.ReadAllBytesAsync(path, ct); var clear = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser); return JsonSerializer.Deserialize<List<T>>(clear) ?? []; } finally { _gate.Release(); } }
    private async Task WriteAsync<T>(string path, IReadOnlyList<T> value, CancellationToken ct) { Directory.CreateDirectory(_directory); await _gate.WaitAsync(ct); try { var clear = JsonSerializer.SerializeToUtf8Bytes(value); var data = ProtectedData.Protect(clear, null, DataProtectionScope.CurrentUser); await File.WriteAllBytesAsync(path, data, ct); } finally { _gate.Release(); } }
}
public sealed class AppSettings
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RismanNG", "settings.json");
    public string? SingBoxPath { get; set; }
    public async Task LoadAsync() { if (File.Exists(_path)) { var saved = JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(_path).ConfigureAwait(false)); SingBoxPath = saved?.SingBoxPath; } }
    public async Task SaveAsync() { Directory.CreateDirectory(Path.GetDirectoryName(_path)!); await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(this)); }
}
