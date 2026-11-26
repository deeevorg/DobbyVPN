using RismanNG.Domain;
namespace RismanNG.Application;
public interface IProfileRepository { Task<IReadOnlyList<Profile>> GetAllAsync(CancellationToken ct = default); Task SaveAsync(Profile profile, CancellationToken ct = default); Task DeleteAsync(Guid id, CancellationToken ct = default); Task SetActiveAsync(Guid id, CancellationToken ct = default); Task ReplaceSubscriptionAsync(Guid subscriptionId, IReadOnlyList<Profile> profiles, CancellationToken ct = default); }
public interface ISubscriptionRepository { Task<IReadOnlyList<Subscription>> GetAllAsync(CancellationToken ct = default); Task SaveAsync(Subscription subscription, CancellationToken ct = default); }
public interface IProfileImporter { IReadOnlyList<Profile> Parse(string input); (IReadOnlyList<Profile> Profiles, int Rejected) ParseSubscription(string input); }
public interface ISingBoxConfigFactory { string Create(Profile profile, int localPort, int apiPort); }
public interface IXrayConfigFactory { string Create(Profile profile, int localPort, int apiPort); }
public interface IConnectionService { event EventHandler<ConnectionMetrics>? MetricsUpdated; string ActiveCore { get; } Task ConnectAsync(CancellationToken ct = default); Task DisconnectAsync(CancellationToken ct = default); }
