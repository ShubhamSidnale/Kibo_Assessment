using InventoryHold.Contracts;

namespace InventoryHold.Domain.Repositories;

public interface IInventoryCache
{
    Task<InventorySnapshot?> GetAsync(long revision, CancellationToken cancellationToken);

    Task SetAsync(InventorySnapshot snapshot, TimeSpan ttl, CancellationToken cancellationToken);
}
