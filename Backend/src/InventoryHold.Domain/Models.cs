using InventoryHold.Contracts;

namespace InventoryHold.Domain;

public sealed record Hold(string Id, IReadOnlyList<HoldItem> Items, HoldStatus Status, DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt, DateTimeOffset? EndedAt = null)
{
    public HoldResponse ToResponse() => new(Id, Items, Status, CreatedAt, ExpiresAt, EndedAt);
}

public sealed record InventorySnapshot(long Revision, IReadOnlyList<ProductResponse> Products);

public sealed record HoldPage(IReadOnlyList<Hold> Items, string? NextCursor);

public sealed record OutboxMessage(string Id, HoldLifecycleEvent Event);

public sealed class HoldOptions
{
    public int DurationMinutes { get; set; } = 15;

    public int ExpirationPollSeconds { get; set; } = 5;

    public int InventoryCacheSeconds { get; set; } = 30;
}

public sealed class DomainException(string code, string message, int statusCode) : Exception(message)
{
    public string Code { get; } = code;

    public int StatusCode { get; } = statusCode;

    public static DomainException NotFound(string resource) => new("not_found", $"{resource} was not found.", 404);

    public static DomainException Validation(string message) => new("invalid_request", message, 400);
}
