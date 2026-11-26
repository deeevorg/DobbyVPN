namespace RismanNG.Domain;
public enum Protocol { Vless, Vmess, Trojan, Shadowsocks, Hysteria2, SingBoxJson }
public enum ConnectionState { Disconnected, Starting, Connected, Reconnecting, Stopping, Error }
public sealed record Profile(Guid Id, string DisplayName, Protocol Protocol, string Host, int Port, string Secret, IReadOnlyDictionary<string, string> Options, bool IsActive = false, string? RawConfig = null, Guid? SubscriptionId = null);
public sealed record Subscription(Guid Id, string Url, DateTimeOffset? LastUpdated = null, string? LastResult = null);
public sealed record ImportResult(int Added, string? Warning = null);
public sealed record ConnectionMetrics(long UploadBytes, long DownloadBytes, string? ExitIp = null, TimeSpan? ServerLatency = null, string? Error = null, string? DirectPublicIp = null, bool RouteLost = false);
