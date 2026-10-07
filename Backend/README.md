# Inventory Hold Service

Backend for the Kibo senior software engineer assessment. Built with .NET 10, MongoDB, Redis, and RabbitMQ. The React frontend is in `../Frontend`; see its README for local startup instructions.

## Start the environment

Install Docker with Compose, then run from the repository root:

```sh
docker-compose up --build
```

`docker compose up --build` also works with Compose v2. API: http://localhost:8080. RabbitMQ management: http://localhost:15672 (local defaults: `assessment` / `assessment`). The API build runs the unit tests before producing the runtime image. MongoDB and RabbitMQ data persist in named volumes. Five products are seeded on startup without resetting existing quantities.

Copy `.env.example` to `.env` to change the API port, hold duration, or local RabbitMQ credentials. These are local assessment defaults; the database and broker ports bind to loopback. Connection strings, database name, exchange, queue, cache TTL, expiry polling, and CORS origins can all be overridden using .NET environment variables such as `Mongo__ConnectionString` and `Hold__DurationMinutes`.

## Build and test

With a .NET 10 SDK:

```sh
dotnet restore InventoryHold.slnx
dotnet build InventoryHold.slnx --no-restore -c Release
dotnet test InventoryHold.slnx --no-build -c Release
```

During development the SDK was installed locally in `.dotnet` because the machine's installed SDKs were 8 and 9. On this workspace, PowerShell can use:

```powershell
$env:DOTNET_CLI_HOME = Join-Path (Get-Location) '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path (Get-Location) '.nuget/packages'
& ./.dotnet/dotnet.exe test InventoryHold.slnx -c Release
```

Unit tests mock repository, cache, and messaging interfaces and require no running services. To exercise real transactions and concurrency after starting Docker:

```sh
node scripts/backend-smoke.mjs
```

The smoke script temporarily reserves stock and releases every hold it creates. Run it against an isolated assessment instance, without concurrent external clients. Set `API_URL` if using another port. Set `CHECK_EXPIRATION=1` with `HOLD_DURATION_MINUTES=1` to also wait for and verify expiration.

For a host-run API with containerized dependencies, start only the dependency services and override MongoDB's discovery for the container's advertised hostname:

```powershell
docker compose up -d mongo-init redis rabbitmq
$env:Mongo__ConnectionString = 'mongodb://localhost:27017/?replicaSet=rs0&directConnection=true'
dotnet run --project src/InventoryHold.WebApi --urls http://localhost:8080
```

## API

All timestamps are UTC ISO 8601. HTTP API enum values serialize as strings.

| Method and path | Result |
| --- | --- |
| `POST /api/holds` | `201`, hold body and `Location` header |
| `GET /api/holds/{id}` | `200` with current status; detects and processes expiry |
| `DELETE /api/holds/{id}` | `200` with terminal hold status; repeated requests are idempotent |
| `GET /api/holds?status=Active&limit=50&cursor=...` | Cursor-paginated list for the upcoming frontend |
| `GET /api/inventory` | Product IDs, names, and available quantities |
| `GET /health/live` | Process liveness |
| `GET /health/ready` | MongoDB connectivity; `503` on failure |

Example create body:

```json
{
  "items": [
    { "productId": "keyboard", "quantity": 2 },
    { "productId": "mouse", "quantity": 1 }
  ]
}
```

Invalid requests return `400`; unknown holds/products return `404`; insufficient stock returns `409`; storage unavailability returns `503`. Errors use Problem Details with a trace ID. A known expired hold returns `200` with `Expired`, preserving its history. Deleting an expired hold expires it and restores stock; deleting a released hold leaves it unchanged. Product IDs are case-sensitive and duplicate IDs are rejected. Quantity must be a positive integer; a hold accepts up to 100 distinct products.

## Design decisions

- **Layering:** Contracts holds transport models; Domain holds business services and repository/cache/publisher interfaces; Infrastructure implements those interfaces; WebApi wires controllers and workers. No infrastructure dependencies enter Domain.
- **Stock safety:** Conditional `$inc` updates require sufficient stock. Multi-product reservations, hold inserts, inventory revision updates, and outbox inserts share a MongoDB transaction. The single-node replica set in Compose enables transactions. Failed requests roll back every product update. MongoDB's transaction callback retries transient write conflicts.
- **Release and expiry:** Only `Active` can transition to a terminal state. Stock restoration and the terminal event commit together. Concurrent release/expiry transactions conflict and retry; the retry observes the terminal state and does no further restoration. The background worker sweeps 100 expired holds every five seconds by default; GET also processes detected expiry. Holds are never TTL-deleted because that would bypass restoration. During API downtime, overdue holds remain reserved until the worker restarts; the delay is conservative and cannot oversell stock.
- **Cache consistency:** Each stock transaction increments a durable inventory revision. Inventory reads use Redis keys containing this revision, double-checking it before filling the cache. A concurrent mutation cannot cause an old fill to overwrite current inventory. Old keys expire after 30 seconds by default. Reads still consult MongoDB for the revision, trading a small indexed lookup for stronger consistency. Redis failures fall back to MongoDB.
- **Event delivery:** A leased transactional outbox publishes durable messages to the topic exchange `inventory.holds`; queue `inventory.holds.events` binds to `hold.*`. Publisher confirms and mandatory routing precede marking delivery. Retries use stable event IDs. Delivery is **at least once**: consumers must deduplicate `eventId`, and events may arrive out of order. The claim lease lasts two minutes; failures retry after ten seconds. Delivered outbox records expire after seven days.
- **Listing:** Extra `GET /api/holds` supports the required active-holds UI. Pagination orders by opaque ID, not creation time. An active page can shrink when expired items are processed; clients should continue using `nextCursor` even when a page is empty.
- **Readiness:** MongoDB is required; Redis and RabbitMQ are degradable. The API can serve mutations while the broker is down because events remain in the outbox. Readiness reports storage connectivity, not every downstream service's health.

## Current verification limits

Build and unit-test results are recorded in `AI-USAGE.md`. Docker is not installed on the development machine, so the Compose environment and live smoke script have not yet been executed there. The frontend has its own production build and mocked API workflow tests; live frontend/backend integration requires a running backend.
