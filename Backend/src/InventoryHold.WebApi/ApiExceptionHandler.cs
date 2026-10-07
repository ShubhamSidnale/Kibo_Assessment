using InventoryHold.Domain;
using Microsoft.AspNetCore.Diagnostics;
using MongoDB.Driver;

namespace InventoryHold.WebApi;

/// <summary>
/// Converts application exceptions into consistent HTTP errors without exposing internal exception details.
/// </summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        DomainException? domain = exception as DomainException;

        // Preserve business errors, classify storage/timeouts as unavailable, and treat other failures as unexpected.
        int status = domain?.StatusCode ?? (exception is MongoException or TimeoutException ? 503 : 500);

        // Expected validation and stock errors are returned to clients; unexpected failures also need server logs.
        if (domain is null)
        {
            logger.LogError(exception, "Request failed: {TraceId}", context.TraceIdentifier);
        }

        context.Response.StatusCode = status;

        // Include the request trace ID so a client-reported failure can be matched to server logs.
        await Results.Problem(
            statusCode: status,
            title: domain?.Code ?? (status == 503 ? "service_unavailable" : "internal_error"),
            detail: domain?.Message ?? "The service could not complete the request. Please retry.",
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = context.TraceIdentifier
            }).ExecuteAsync(context);

        return true;
    }
}
