using InventoryHold.Domain;
using InventoryHold.Domain.Repositories;
using InventoryHold.Domain.Services;
using InventoryHold.Infrastructure;
using MongoDB.Driver;
using StackExchange.Redis;

namespace InventoryHold.WebApi;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Validates application settings and registers the services needed to reserve and restore inventory.
    /// </summary>
    public static IServiceCollection AddInventoryHoldServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind settings from configuration, including appsettings.json and environment variable overrides.
        MongoOptions mongo = configuration.GetSection("Mongo").Get<MongoOptions>() ?? new();

        RedisOptions redis = configuration.GetSection("Redis").Get<RedisOptions>() ?? new();

        RabbitOptions rabbit = configuration.GetSection("Rabbit").Get<RabbitOptions>() ?? new();

        HoldOptions holds = configuration.GetSection("Hold").Get<HoldOptions>() ?? new();

        // Reject missing or malformed connection settings at startup instead of during a customer request.
        if (string.IsNullOrWhiteSpace(mongo.ConnectionString) || string.IsNullOrWhiteSpace(mongo.Database) ||
            string.IsNullOrWhiteSpace(redis.ConnectionString) ||
            !Uri.TryCreate(rabbit.ConnectionString, UriKind.Absolute, out Uri? rabbitUri) ||
            rabbitUri.Scheme is not ("amqp" or "amqps") ||
            string.IsNullOrWhiteSpace(rabbit.Exchange) || string.IsNullOrWhiteSpace(rabbit.Queue))
        {
            throw new InvalidOperationException("Configure Mongo, Redis, and Rabbit connection settings.");
        }

        // Keep hold duration, expiration polling, and cache lifetime within the supported operating ranges.
        if (holds.DurationMinutes is < 1 or > 1440 || holds.ExpirationPollSeconds is < 1 or > 60 ||
            holds.InventoryCacheSeconds is < 1 or > 300)
        {
            throw new InvalidOperationException("Hold settings are outside the supported ranges.");
        }

        // Share one settings instance per section across controllers, services, and background workers.
        services.AddSingleton(mongo);

        services.AddSingleton(redis);

        services.AddSingleton(rabbit);

        services.AddSingleton(holds);

        // Inject the clock so tests can control expiration without waiting for real time to pass.
        services.AddSingleton(TimeProvider.System);

        MongoClientSettings mongoSettings = MongoClientSettings.FromConnectionString(mongo.ConnectionString);

        // Bound the wait for an available MongoDB server so storage outages do not stall requests indefinitely.
        mongoSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);

        // Reuse the MongoDB client's connection pool for all requests and background work.
        services.AddSingleton<IMongoClient>(new MongoClient(mongoSettings));

        services.AddSingleton<MongoStore>();

        // Both repository interfaces resolve to the same store, which implements transactional stock and outbox writes.
        services.AddSingleton<IHoldRepository>(sp => sp.GetRequiredService<MongoStore>());

        services.AddSingleton<IOutboxRepository>(sp => sp.GetRequiredService<MongoStore>());

        // Share Redis connections; the cache implementation falls back to MongoDB reads when Redis is unavailable.
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis.ConnectionString));

        services.AddSingleton<IInventoryCache, RedisInventoryCache>();

        // The dispatcher sends committed outbox events through RabbitMQ and retries failed deliveries.
        services.AddSingleton<IEventPublisher, RabbitEventPublisher>();

        // Keep business rules in domain services so HTTP controllers only handle request and response concerns.
        services.AddSingleton<HoldService>();

        services.AddSingleton<OutboxDispatcher>();

        // Restore stock for expired holds even when no client requests those holds.
        services.AddHostedService<ExpirationWorker>();

        // Deliver queued events independently of HTTP requests, including after an API restart.
        services.AddHostedService<OutboxWorker>();

        return services;
    }
}
