using System.Text.Json.Serialization;
using InventoryHold.Infrastructure;
using InventoryHold.WebApi;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Validate settings and connect business services to their storage, cache, and messaging implementations.
builder.Services.AddInventoryHoldServices(builder.Configuration);

// Return readable status names, such as "Active", instead of numeric enum values in JSON responses.
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Use a consistent error response format for exceptions and HTTP errors without a response body.
builder.Services.AddProblemDetails();

builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// Allow the configured frontend origins to call the API from a browser.
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
    .AllowAnyHeader()
    .AllowAnyMethod()));

WebApplication app = builder.Build();

// Register error handling before endpoints so it can handle exceptions thrown while processing requests.
app.UseExceptionHandler();

app.UseStatusCodePages();

app.UseCors();

app.MapControllers();

app.MapInventoryHealthEndpoints();

// Create indexes and seed missing products before accepting requests; preserve existing stock quantities.
await app.Services.GetRequiredService<MongoStore>().InitializeAsync(CancellationToken.None);

await app.RunAsync();
