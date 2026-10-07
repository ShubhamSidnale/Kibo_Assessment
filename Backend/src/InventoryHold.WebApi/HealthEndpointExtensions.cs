using InventoryHold.Infrastructure;
using MongoDB.Bson;
using MongoDB.Driver;

namespace InventoryHold.WebApi;

public static class HealthEndpointExtensions
{
    /// <summary>
    /// Adds separate endpoints for process liveness and MongoDB connectivity.
    /// </summary>
    public static WebApplication MapInventoryHealthEndpoints(this WebApplication app)
    {
        MongoOptions options = app.Services.GetRequiredService<MongoOptions>();

        // Liveness checks only that the API can respond; it does not depend on external services.
        app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));

        // MongoDB is required for stock transactions. Redis and RabbitMQ outages allow degraded operation.
        // This probe checks connectivity; it does not verify every transaction prerequisite or downstream service.
        app.MapGet("/health/ready", async (IMongoClient client, CancellationToken ct) =>
        {
            try
            {
                await client.GetDatabase(options.Database).RunCommandAsync<BsonDocument>(
                    new BsonDocument("ping", 1), cancellationToken: ct);

                return Results.Ok(new { status = "ready" });
            }
            // Report dependency failures as unavailable, while allowing request cancellation to propagate.
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                return Results.StatusCode(503);
            }

        });

        return app;
    }
}
