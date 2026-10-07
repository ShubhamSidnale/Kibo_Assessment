using System.Text.Json;
using InventoryHold.Domain;
using InventoryHold.Domain.Repositories;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace InventoryHold.Infrastructure;

public sealed class RedisInventoryCache(IConnectionMultiplexer connection, RedisOptions options,
    ILogger<RedisInventoryCache> logger) : IInventoryCache
{
    private string Key(long revision) => $"{options.KeyPrefix}inventory:{revision}";

    public async Task<InventorySnapshot?> GetAsync(long revision, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            RedisValue value = await connection.GetDatabase().StringGetAsync(Key(revision));

            return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<InventorySnapshot>((string)value!);
        }
        catch (Exception ex) when (ex is RedisException or JsonException)
        {
            logger.LogWarning(ex, "Inventory cache read failed; reading MongoDB instead");

            return null;
        }
    }

    public async Task SetAsync(InventorySnapshot snapshot, TimeSpan ttl, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await connection.GetDatabase().StringSetAsync(Key(snapshot.Revision),
                JsonSerializer.Serialize(snapshot), ttl);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Inventory cache write failed");
        }
    }
}
