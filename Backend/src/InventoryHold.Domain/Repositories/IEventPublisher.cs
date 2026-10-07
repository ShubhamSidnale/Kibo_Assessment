using InventoryHold.Contracts;

namespace InventoryHold.Domain.Repositories;

public interface IEventPublisher
{
    Task PublishAsync(HoldLifecycleEvent message, CancellationToken cancellationToken);
}
