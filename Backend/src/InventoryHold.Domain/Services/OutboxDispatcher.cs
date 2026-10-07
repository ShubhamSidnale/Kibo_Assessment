using InventoryHold.Domain.Repositories;

namespace InventoryHold.Domain.Services;

public sealed class OutboxDispatcher(IOutboxRepository repository, IEventPublisher publisher, TimeProvider clock)
{
    private readonly string owner = Guid.NewGuid().ToString("N");

    public async Task<bool> DispatchOneAsync(CancellationToken ct = default)
    {
        OutboxMessage? message = await repository.ClaimAsync(owner, clock.GetUtcNow(), ct);

        if (message is null)
        {
            return false;
        }

        try
        {
            await publisher.PublishAsync(message.Event, ct);

            await repository.CompleteAsync(message.Id, owner, ct);

            return true;
        }
        catch
        {
            if (!ct.IsCancellationRequested)
            {
                await repository.RetryAsync(message.Id, owner, clock.GetUtcNow().AddSeconds(10), ct);
            }

            throw;
        }
    }
}
