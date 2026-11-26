using System.Text.Json;
using RismanNG.Application;
using RismanNG.Domain;

namespace RismanNG.Infrastructure;

public sealed class XrayConfigFactory : IXrayConfigFactory
{
    public string Create(Profile profile, int localPort, int apiPort)
    {
        if (profile.Protocol is Protocol.SingBoxJson or Protocol.Hysteria2)
            throw new InvalidOperationException("This profile must be run by sing-box.");

        var outbound = new Dictionary<string, object?> { ["tag"] = "proxy", ["protocol"] = profile.Protocol.ToString().ToLowerInvariant() };
        switch (profile.Protocol)
        {
            case Protocol.Vless:
                outbound["settings"] = new { vnext = new[] { new { address = profile.Host, port = profile.Port, users = new[] { new { id = profile.Secret, encryption = profile.Options.TryGetValue("encryption", out var encryption) ? encryption : "none", flow = profile.Options.TryGetValue("flow", out var flow) ? flow : null } } } } };
                break;
            case Protocol.Vmess:
                outbound["settings"] = new { vnext = new[] { new { address = profile.Host, port = profile.Port, users = new[] { new { id = profile.Secret, security = "auto" } } } } };
                break;
            case Protocol.Trojan:
                outbound["settings"] = new { servers = new[] { new { address = profile.Host, port = profile.Port, password = profile.Secret } } };
                break;
            case Protocol.Shadowsocks:
                var parts = profile.Secret.Split(':', 2);
                if (parts.Length != 2) throw new InvalidOperationException("Invalid Shadowsocks profile.");
                outbound["settings"] = new { servers = new[] { new { address = profile.Host, port = profile.Port, method = parts[0], password = parts[1] } } };
                break;
            default: throw new InvalidOperationException("This profile is not supported by Xray.");
        }

        var stream = new Dictionary<string, object?> { ["network"] = profile.Options.TryGetValue("type", out var transport) ? transport : "tcp", ["security"] = "none" };
        if (transport == "ws") stream["wsSettings"] = new { path = profile.Options.TryGetValue("path", out var path) ? path : "/", headers = profile.Options.TryGetValue("host", out var host) ? new { Host = host } : null };
        if (transport == "grpc") stream["grpcSettings"] = new { serviceName = profile.Options.TryGetValue("serviceName", out var service) ? service : "" };
        if (profile.Options.TryGetValue("security", out var security) && security is "tls" or "reality")
        {
            stream["security"] = security;
            var serverName = profile.Options.TryGetValue("sni", out var sni) ? sni : profile.Host;
            var insecure = (profile.Options.TryGetValue("allowInsecure", out var allowInsecure) && allowInsecure is "1" or "true") || (profile.Options.TryGetValue("insecure", out var insecureOption) && insecureOption is "1" or "true");
            if (security == "tls") stream["tlsSettings"] = new { serverName, allowInsecure = insecure, alpn = profile.Options.TryGetValue("alpn", out var alpn) ? alpn.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) : null, fingerprint = profile.Options.TryGetValue("fp", out var fp) ? fp : null };
            else stream["realitySettings"] = new { show = false, serverName, fingerprint = profile.Options.TryGetValue("fp", out var fp) ? fp : "chrome", publicKey = profile.Options["pbk"], shortId = profile.Options.TryGetValue("sid", out var sid) ? sid : "", spiderX = profile.Options.TryGetValue("spx", out var spx) ? spx : "" };
        }
        outbound["streamSettings"] = stream;

        var configuration = new
        {
            log = new { loglevel = "warning" },
            api = new { tag = "api", services = new[] { "StatsService" } },
            stats = new { },
            policy = new { system = new { statsOutboundUplink = true, statsOutboundDownlink = true } },
            inbounds = new object[]
            {
                new { tag = "http-in", listen = "127.0.0.1", port = localPort, protocol = "http", settings = new { } },
                new { tag = "api", listen = "127.0.0.1", port = apiPort, protocol = "dokodemo-door", settings = new { address = "127.0.0.1" } }
            },
            outbounds = new object[] { outbound, new { tag = "direct", protocol = "freedom" } },
            routing = new { rules = new[] { new { type = "field", inboundTag = new[] { "api" }, outboundTag = "api" } } }
        };
        return JsonSerializer.Serialize(configuration, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
    }
}
