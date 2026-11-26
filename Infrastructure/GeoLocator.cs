using System.Net;
using System.Text.Json;

namespace RismanNG.Infrastructure;
public sealed record GeoLocation(string Ip, double Latitude, double Longitude, string Label);
public static class GeoLocator
{
    public static async Task<GeoLocation?> LocateAsync(string ip, CancellationToken ct)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            using var response = await client.GetAsync($"https://ipwho.is/{Uri.EscapeDataString(ip)}", ct);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct)); var root = json.RootElement;
            if (!root.TryGetProperty("success", out var success) || !success.GetBoolean()) return null;
            var city = root.TryGetProperty("city", out var c) ? c.GetString() : null; var country = root.TryGetProperty("country", out var countryValue) ? countryValue.GetString() : null;
            return new(ip, root.GetProperty("latitude").GetDouble(), root.GetProperty("longitude").GetDouble(), string.Join(", ", new[] { city, country }.Where(x => !string.IsNullOrWhiteSpace(x))));
        }
        catch { return null; }
    }
}
