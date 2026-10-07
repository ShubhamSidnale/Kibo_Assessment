using InventoryHold.Contracts;
using InventoryHold.Domain.Repositories;

namespace InventoryHold.Domain.Services;

public sealed class HoldService(IHoldRepository repository, IInventoryCache cache, HoldOptions options,
    TimeProvider clock)
{
    public async Task<HoldResponse> CreateAsync(CreateHoldRequest request, CancellationToken ct = default)
    {
        if (request.Items is null || request.Items.Count is < 1 or > 100)
        {
            throw DomainException.Validation("Provide between 1 and 100 distinct products.");
        }

        if (request.Items.Any(i => i is null || string.IsNullOrWhiteSpace(i.ProductId) ||
            i.ProductId.Length > 100 || i.Quantity <= 0))
        {
            throw DomainException.Validation("Every item requires a product ID and a positive quantity.");
        }

        if (request.Items.Select(i => i.ProductId).Distinct(StringComparer.Ordinal).Count() != request.Items.Count)
        {
            throw DomainException.Validation("Duplicate product IDs are not allowed.");
        }

        DateTimeOffset now = clock.GetUtcNow();

        Hold hold = new Hold(Guid.NewGuid().ToString("N"), request.Items.ToArray(), HoldStatus.Active, now,
            now.AddMinutes(options.DurationMinutes));

        return (await repository.CreateAsync(hold, ct)).ToResponse();
    }

    public async Task<HoldResponse> GetAsync(string id, CancellationToken ct = default)
    {
        Hold hold = await repository.GetAsync(id, ct) ?? throw DomainException.NotFound("Hold");

        if (hold.Status == HoldStatus.Active && hold.ExpiresAt <= clock.GetUtcNow())
        {
            hold = await repository.EndAsync(id, HoldStatus.Expired, clock.GetUtcNow(),
                ct) ?? throw DomainException.NotFound("Hold");
        }

        return hold.ToResponse();
    }

    public async Task<HoldResponse> ReleaseAsync(string id, CancellationToken ct = default) =>
        (await repository.EndAsync(id, HoldStatus.Released, clock.GetUtcNow(),
            ct) ?? throw DomainException.NotFound("Hold")).ToResponse();

    public async Task ExpireAsync(CancellationToken ct = default)
    {
        DateTimeOffset now = clock.GetUtcNow();

        foreach (string id in await repository.FindExpiredAsync(now, 100, ct))
        {
            await repository.EndAsync(id, HoldStatus.Expired, now, ct);
        }
    }

    public async Task<HoldListResponse> ListAsync(HoldStatus? status, string? cursor, int limit,
        CancellationToken ct = default)
    {
        if (limit is < 1 or > 100)
        {
            throw DomainException.Validation("Limit must be between 1 and 100.");
        }

        if (status.HasValue && !Enum.IsDefined(status.Value))
        {
            throw DomainException.Validation("Invalid hold status.");
        }

        if (cursor is not null && (!Guid.TryParseExact(cursor, "N", out _) || cursor != cursor.ToLowerInvariant()))
        {
            throw DomainException.Validation("Invalid pagination cursor.");
        }

        HoldPage page = await repository.ListAsync(status, cursor, limit, ct);

        List<HoldResponse> items = new List<HoldResponse>();

        foreach (Hold hold in page.Items)
        {
            Hold current = hold;

            if (hold.Status == HoldStatus.Active && hold.ExpiresAt <= clock.GetUtcNow())
            {
                current = await repository.EndAsync(hold.Id, HoldStatus.Expired, clock.GetUtcNow(), ct) ?? hold;
            }

            if (status is null || current.Status == status)
            {
                items.Add(current.ToResponse());
            }
        }

        return new(items, page.NextCursor);
    }

    public async Task<IReadOnlyList<ProductResponse>> InventoryAsync(CancellationToken ct = default)
    {
        // A committed mutation changes the revision. An in-flight reader can only populate
        // an old cache key, which future readers never use. Redis is an optional optimization.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            long revision = await repository.GetInventoryRevisionAsync(ct);

            InventorySnapshot? cached = await cache.GetAsync(revision, ct);

            if (cached is not null)
            {
                return cached.Products;
            }

            IReadOnlyList<ProductResponse> products = await repository.GetInventoryAsync(ct);

            if (await repository.GetInventoryRevisionAsync(ct) != revision)
            {
                continue;
            }

            await cache.SetAsync(new(revision, products), TimeSpan.FromSeconds(options.InventoryCacheSeconds), ct);

            return products;
        }

        return await repository.GetInventoryAsync(ct);
    }
}
