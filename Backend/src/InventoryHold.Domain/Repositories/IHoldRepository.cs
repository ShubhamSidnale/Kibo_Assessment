using InventoryHold.Contracts;

namespace InventoryHold.Domain.Repositories;
// Each mutation commits stock, hold state, inventory revision, and outbox event together.
public interface IHoldRepository
{
    Task<Hold> CreateAsync(Hold hold, CancellationToken cancellationToken);

    Task<Hold?> GetAsync(string id, CancellationToken cancellationToken);

    Task<Hold?> EndAsync(string id, HoldStatus requestedStatus, DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<HoldPage> ListAsync(HoldStatus? status, string? cursor, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> FindExpiredAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    Task<long> GetInventoryRevisionAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductResponse>> GetInventoryAsync(CancellationToken cancellationToken);
}
