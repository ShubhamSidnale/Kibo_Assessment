namespace InventoryHold.Contracts;

public enum HoldStatus
{
    Active,
    Released,
    Expired
}

public sealed record HoldItem(string ProductId, int Quantity);

public sealed record CreateHoldRequest(IReadOnlyList<HoldItem>? Items);

public sealed record HoldResponse(string Id, IReadOnlyList<HoldItem> Items, HoldStatus Status,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? EndedAt);

public sealed record ProductResponse(string Id, string Name, int AvailableQuantity);

public sealed record HoldListResponse(IReadOnlyList<HoldResponse> Items, string? NextCursor);

public sealed record HoldLifecycleEvent(string EventId, string EventType, string HoldId,
    IReadOnlyList<HoldItem> Items, HoldStatus Status, DateTimeOffset OccurredAt, DateTimeOffset CreatedAt,
        DateTimeOffset ExpiresAt);
