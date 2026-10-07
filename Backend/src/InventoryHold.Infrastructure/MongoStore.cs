using System.Text.Json;
using InventoryHold.Contracts;
using InventoryHold.Domain;
using InventoryHold.Domain.Repositories;
using MongoDB.Driver;

namespace InventoryHold.Infrastructure;

public sealed class MongoStore : IHoldRepository, IOutboxRepository
{
    private readonly IMongoClient client;

    private readonly IMongoCollection<ProductDocument> products;

    private readonly IMongoCollection<HoldDocument> holds;

    private readonly IMongoCollection<RevisionDocument> revisions;

    private readonly IMongoCollection<OutboxDocument> outbox;

    private static readonly TransactionOptions TransactionSettings = new(readConcern: ReadConcern.Snapshot,
        readPreference: ReadPreference.Primary, writeConcern: WriteConcern.WMajority);

    public MongoStore(IMongoClient client, MongoOptions options)
    {
        this.client = client;

        IMongoDatabase database = client.GetDatabase(options.Database);

        products = database.GetCollection<ProductDocument>("products");

        holds = database.GetCollection<HoldDocument>("holds");

        revisions = database.GetCollection<RevisionDocument>("revisions");

        outbox = database.GetCollection<OutboxDocument>("outbox");
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        await holds.Indexes.CreateOneAsync(new CreateIndexModel<HoldDocument>(Builders<HoldDocument>.IndexKeys.
            Ascending(h => h.Status).Ascending(h => h.ExpiresAt)), cancellationToken: ct);

        await outbox.Indexes.CreateOneAsync(new CreateIndexModel<OutboxDocument>(Builders<OutboxDocument>.IndexKeys.
            Ascending(o => o.DeliveredAt).Ascending(o => o.NextAttemptAt)), cancellationToken: ct);

        // Completed events are retained for seven days. Holds are not TTL-deleted: stock must be restored first.
        await outbox.Indexes.CreateOneAsync(new CreateIndexModel<OutboxDocument>(Builders<OutboxDocument>.IndexKeys.
            Ascending(o => o.DeliveredAt), new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(7) }),
                cancellationToken: ct);

        await revisions.UpdateOneAsync(r => r.Id == "inventory", Builders<RevisionDocument>.Update.SetOnInsert(r =>
            r.Revision, 0), new UpdateOptions { IsUpsert = true }, ct);

        (string, string, int)[] seeds = new[]
        {
            ("keyboard", "Mechanical Keyboard", 25),
            ("mouse", "Wireless Mouse", 40),
            ("monitor", "27-inch Monitor", 15),
            ("headphones", "Studio Headphones", 30),
            ("webcam", "HD Webcam", 20)
        };

        using IClientSessionHandle session = await client.StartSessionAsync(cancellationToken: ct);

        await session.WithTransactionAsync(async (s, token) =>
        {
            bool changed = false;

            foreach ((string id, string name, int quantity) in seeds)
            {
                UpdateResult result = await products.UpdateOneAsync(s, p => p.Id == id,
                    Builders<ProductDocument>.Update.SetOnInsert(p => p.Name, name).SetOnInsert(p =>
                        p.AvailableQuantity, quantity), new UpdateOptions { IsUpsert = true }, token);

                changed |= result.UpsertedId is not null;
            }

            if (changed)
            {
                await BumpRevisionAsync(s, token);
            }

            return true;

        }, TransactionSettings, ct);
    }

    public async Task<Hold> CreateAsync(Hold hold, CancellationToken ct)
    {
        using IClientSessionHandle session = await client.StartSessionAsync(cancellationToken: ct);

        return await session.WithTransactionAsync(async (s, token) =>
        {
            foreach (HoldItem item in hold.Items.OrderBy(i => i.ProductId, StringComparer.Ordinal))
            {
                UpdateResult result = await products.UpdateOneAsync(s, p => p.Id == item.ProductId &&
                    p.AvailableQuantity >= item.Quantity, Builders<ProductDocument>.Update.Inc(p =>
                        p.AvailableQuantity, -item.Quantity), cancellationToken: token);

                if (result.ModifiedCount != 1)
                {
                    bool exists = await products.Find(s, p => p.Id == item.ProductId).AnyAsync(token);

                    if (!exists)
                    {
                        throw DomainException.NotFound($"Product '{item.ProductId}'");
                    }

                    throw new DomainException("insufficient_stock", $"Insufficient stock for '{item.ProductId}'.", 409);
                }
            }

            await holds.InsertOneAsync(s, HoldDocument.From(hold), cancellationToken: token);

            await BumpRevisionAsync(s, token);

            await AppendEventAsync(s, hold, "HoldCreated", hold.CreatedAt, token);

            return hold;

        }, TransactionSettings, ct);
    }

    public async Task<Hold?> GetAsync(string id, CancellationToken ct) => (await holds.Find(h =>
        h.Id == id).FirstOrDefaultAsync(ct))?.ToDomain();

    public async Task<Hold?> EndAsync(string id, HoldStatus requestedStatus, DateTimeOffset now, CancellationToken ct)
    {
        if (requestedStatus == HoldStatus.Active)
        {
            throw new ArgumentException("A terminal status is required.", nameof(requestedStatus));
        }

        using IClientSessionHandle session = await client.StartSessionAsync(cancellationToken: ct);

        return await session.WithTransactionAsync<Hold?>(async (s, token) =>
        {
            HoldDocument? document = await holds.Find(s, h => h.Id == id).FirstOrDefaultAsync(token);

            if (document is null)
            {
                return null;
            }

            Hold hold = document.ToDomain();

            if (hold.Status != HoldStatus.Active)
            {
                return hold;
            }

            if (requestedStatus == HoldStatus.Expired && hold.ExpiresAt > now)
            {
                return hold;
            }

            HoldStatus status = hold.ExpiresAt <= now ? HoldStatus.Expired : HoldStatus.Released;

            UpdateResult result = await holds.UpdateOneAsync(s, h => h.Id == id && h.Status == HoldStatus.Active,
                Builders<HoldDocument>.Update.Set(h => h.Status, status).Set(h => h.EndedAt, now.UtcDateTime),
                    cancellationToken: token);

            if (result.ModifiedCount != 1)
            {
                throw new InvalidOperationException("Hold state changed inside a transaction.");
            }

            foreach (HoldItem item in hold.Items)
            {
                UpdateResult restored = await products.UpdateOneAsync(s, p => p.Id == item.ProductId,
                    Builders<ProductDocument>.Update.Inc(p => p.AvailableQuantity, item.Quantity),
                        cancellationToken: token);

                if (restored.ModifiedCount != 1)
                {
                    throw new InvalidOperationException("A held product is missing.");
                }
            }

            Hold ended = hold with
            {
                Status = status,
                EndedAt = now
            };

            await BumpRevisionAsync(s, token);

            await AppendEventAsync(s, ended, status == HoldStatus.Expired ? "HoldExpired" : "HoldReleased", now, token);

            return ended;

        }, TransactionSettings, ct);
    }

    public async Task<HoldPage> ListAsync(HoldStatus? status, string? cursor, int limit, CancellationToken ct)
    {
        FilterDefinition<HoldDocument> filter = Builders<HoldDocument>.Filter.Empty;

        if (status.HasValue)
        {
            filter &= Builders<HoldDocument>.Filter.Eq(h => h.Status, status.Value);
        }

        if (cursor is not null)
        {
            filter &= Builders<HoldDocument>.Filter.Gt(h => h.Id, cursor);
        }

        List<HoldDocument> docs = await holds.Find(filter).SortBy(h => h.Id).Limit(limit + 1).ToListAsync(ct);

        return new(docs.Take(limit).Select(h => h.ToDomain()).ToArray(),
            docs.Count > limit ? docs[limit - 1].Id : null);
    }

    public async Task<IReadOnlyList<string>> FindExpiredAsync(DateTimeOffset now, int limit,
        CancellationToken ct) => await holds.Find(h => h.Status == HoldStatus.Active &&
            h.ExpiresAt <= now.UtcDateTime).SortBy(h => h.ExpiresAt).Limit(limit).Project(h => h.Id).ToListAsync(ct);

    public async Task<long> GetInventoryRevisionAsync(CancellationToken ct) => (await revisions.Find(r =>
        r.Id == "inventory").FirstAsync(ct)).Revision;

    public async Task<IReadOnlyList<ProductResponse>> GetInventoryAsync(CancellationToken ct) =>
        (await products.Find(Builders<ProductDocument>.Filter.Empty).SortBy(p =>
            p.Name).ToListAsync(ct)).Select(p => new ProductResponse(p.Id, p.Name, p.AvailableQuantity)).ToArray();

    private Task BumpRevisionAsync(IClientSessionHandle session, CancellationToken ct) =>
        revisions.UpdateOneAsync(session, r => r.Id == "inventory", Builders<RevisionDocument>.Update.Inc(r =>
            r.Revision, 1), cancellationToken: ct);

    private Task AppendEventAsync(IClientSessionHandle session, Hold hold, string type, DateTimeOffset now,
        CancellationToken ct)
    {
        string id = Guid.NewGuid().ToString("N");

        HoldLifecycleEvent message = new HoldLifecycleEvent(id, type, hold.Id, hold.Items, hold.Status, now,
            hold.CreatedAt, hold.ExpiresAt);

        return outbox.InsertOneAsync(session, new OutboxDocument
        {
            Id = id,
            Payload =
            JsonSerializer.Serialize(message, JsonSerializerOptions.Web),
            NextAttemptAt = now.UtcDateTime
        },
                cancellationToken: ct);
    }

    public async Task<OutboxMessage?> ClaimAsync(string owner, DateTimeOffset now, CancellationToken ct)
    {
        OutboxDocument? document = await outbox.FindOneAndUpdateAsync(o => o.DeliveredAt == null &&
            o.NextAttemptAt <= now.UtcDateTime, Builders<OutboxDocument>.Update.Set(o => o.Owner, owner).Set(o =>
                o.NextAttemptAt, now.AddMinutes(2).UtcDateTime),
                    new FindOneAndUpdateOptions<OutboxDocument>
                    {
                        ReturnDocument = ReturnDocument.After,
                        Sort =
                        Builders<OutboxDocument>.Sort.Ascending(o => o.NextAttemptAt)
                    }, ct);

        return document is null ? null : new(document.Id,
            JsonSerializer.Deserialize<HoldLifecycleEvent>(document.Payload, JsonSerializerOptions.Web)!);
    }

    public Task CompleteAsync(string id, string owner, CancellationToken ct) => outbox.UpdateOneAsync(o =>
        o.Id == id && o.Owner == owner && o.DeliveredAt == null, Builders<OutboxDocument>.Update.Set(o =>
            o.DeliveredAt, DateTime.UtcNow).Unset(o => o.Owner), cancellationToken: ct);

    public Task RetryAsync(string id, string owner, DateTimeOffset retryAt, CancellationToken ct) =>
        outbox.UpdateOneAsync(o => o.Id == id && o.Owner == owner && o.DeliveredAt == null,
            Builders<OutboxDocument>.Update.Set(o => o.NextAttemptAt, retryAt.UtcDateTime).Unset(o => o.Owner),
                cancellationToken: ct);
}
