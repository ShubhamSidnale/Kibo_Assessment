using InventoryHold.Contracts;
using InventoryHold.Domain;
using MongoDB.Bson.Serialization.Attributes;

namespace InventoryHold.Infrastructure;

internal sealed class ProductDocument
{
    [BsonId]
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public int AvailableQuantity { get; set; }
}

internal sealed class HoldDocument
{
    [BsonId]
    public string Id { get; set; } = "";

    public List<ItemDocument> Items { get; set; } = [];

    public HoldStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? EndedAt { get; set; }

    public Hold ToDomain() => new(Id, Items.Select(i => new HoldItem(i.ProductId, i.Quantity)).ToArray(), Status,
        new(CreatedAt, TimeSpan.Zero), new(ExpiresAt, TimeSpan.Zero),
            EndedAt is { } ended ? new DateTimeOffset(ended, TimeSpan.Zero) : null);

    public static HoldDocument From(Hold hold) => new()
    {
        Id = hold.Id,
        Items = hold.Items.Select(i => new ItemDocument { ProductId = i.ProductId, Quantity = i.Quantity }).ToList(),
        Status = hold.Status,
        CreatedAt = hold.CreatedAt.UtcDateTime,
        ExpiresAt = hold.ExpiresAt.UtcDateTime,
        EndedAt = hold.EndedAt?.UtcDateTime
    };
}

internal sealed class ItemDocument
{
    public string ProductId { get; set; } = "";

    public int Quantity { get; set; }
}

internal sealed class RevisionDocument
{
    [BsonId]
    public string Id { get; set; } = "inventory";

    public long Revision { get; set; }
}

internal sealed class OutboxDocument
{
    [BsonId]
    public string Id { get; set; } = "";

    public string Payload { get; set; } = "";

    public DateTime NextAttemptAt { get; set; }
    public string? Owner { get; set; }
    public DateTime? DeliveredAt { get; set; }
}
