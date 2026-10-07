using InventoryHold.Contracts;

namespace InventoryHold.Domain.Repositories;

public interface IOutboxRepository
{
    Task<OutboxMessage?> ClaimAsync(string owner, DateTimeOffset now, CancellationToken cancellationToken);

    Task CompleteAsync(string id, string owner, CancellationToken cancellationToken);

    Task RetryAsync(string id, string owner, DateTimeOffset retryAt, CancellationToken cancellationToken);
}
