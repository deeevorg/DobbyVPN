using RismanNG.Application;
namespace RismanNG.Infrastructure;
public sealed class AppServices
{
    public required ProfileService Profiles { get; init; } public required SubscriptionService Subscriptions { get; init; } public required ImportService Import { get; init; } public required IConnectionService Connection { get; init; } public required AppSettings Settings { get; init; }
    public static AppServices Create() { var store = new LocalStorage(); IProfileRepository profiles = store; ISubscriptionRepository subscriptions = store; var settings = new AppSettings(); var proxy = new WindowsProxyManager(); _ = Task.Run(() => proxy.RecoverAsync()); var import = new ImportService(new ProfileImporter(), profiles); return new() { Profiles = new(profiles), Subscriptions = new(subscriptions, profiles, import), Import = import, Connection = new ConnectionService(profiles, new SingBoxConfigFactory(), new XrayConfigFactory(), settings, proxy), Settings = settings }; }
}
