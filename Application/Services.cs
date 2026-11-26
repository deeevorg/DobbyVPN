using RismanNG.Domain;
using System.Net.Http;
using System.Text;
namespace RismanNG.Application;
public sealed class ImportService(IProfileImporter importer, IProfileRepository profiles)
{
    public async Task<ImportResult> ImportAsync(string input, CancellationToken ct = default) { var parsed = importer.Parse(input); foreach (var profile in parsed) await profiles.SaveAsync(profile, ct); return new(parsed.Count); }
    public (IReadOnlyList<Profile> Profiles, int Rejected) ParseSubscription(string input) => importer.ParseSubscription(input);
}
public sealed class ProfileService(IProfileRepository repository)
{
    public Task<IReadOnlyList<Profile>> GetAllAsync() => repository.GetAllAsync();
    public Task SaveAsync(Profile profile) => repository.SaveAsync(profile);
    public Task DeleteAsync(Guid id) => repository.DeleteAsync(id);
    public Task SetActiveAsync(Guid id) => repository.SetActiveAsync(id);
}
public sealed class SubscriptionService(ISubscriptionRepository repository, IProfileRepository profiles, ImportService importer)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    public Task<IReadOnlyList<Subscription>> GetAllAsync() => repository.GetAllAsync();
    public async Task<ImportResult> AddAndRefreshAsync(string url, CancellationToken ct = default)
    {
        var sub = Validate(url.Trim());
        var existing = (await repository.GetAllAsync(ct)).FirstOrDefault(x => string.Equals(x.Url, sub.Url, StringComparison.OrdinalIgnoreCase));
        return await RefreshAsync(existing ?? sub, ct);
    }
    public async Task<ImportResult> RefreshAsync(Subscription sub, CancellationToken ct = default)
    {
        ValidateUrl(sub.Url);
        using var response = await Http.GetAsync(sub.Url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > 5_000_000) throw new InvalidOperationException("Subscription response is too large.");
        using var responseStream = await response.Content.ReadAsStreamAsync(ct);
        using var bounded = new MemoryStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await responseStream.ReadAsync(chunk.AsMemory(), ct)) > 0)
        {
            if (bounded.Length + count > 5_000_000) throw new InvalidOperationException("Subscription response is too large.");
            await bounded.WriteAsync(chunk.AsMemory(0, count), ct);
        }
        var content = Encoding.UTF8.GetString(bounded.ToArray());
        var (parsed, rejected) = importer.ParseSubscription(content);
        await profiles.ReplaceSubscriptionAsync(sub.Id, parsed, ct);
        var warning = rejected == 0 ? null : $"{rejected} unsupported or invalid link(s) skipped.";
        var result = new ImportResult(parsed.Count, warning);
        await repository.SaveAsync(sub with { LastUpdated = DateTimeOffset.UtcNow, LastResult = $"Imported {result.Added} profile(s)" + (warning is null ? "" : $"; {warning}") }, ct);
        return result;
    }
    private static Subscription Validate(string url) { ValidateUrl(url); return new(Guid.NewGuid(), url); }
    private static void ValidateUrl(string url) { if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) throw new InvalidOperationException("Subscription URL must use HTTPS."); }
}
