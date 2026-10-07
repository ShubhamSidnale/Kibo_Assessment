# Assignment coverage review

Reviewed on 7 October 2026 against all five pages of the supplied assignment PDF.

## Requirement coverage

| Requirement | Result | Evidence |
| --- | --- | --- |
| .NET 10, MongoDB, Redis, RabbitMQ in Compose | Implemented and running | `Backend/docker-compose.yml`; API running and dependency containers healthy |
| Configurable connections and hold duration | Implemented | `appsettings.json`, environment overrides, default 15-minute holds |
| Required DDD project structure | Implemented | Contracts, Domain, Infrastructure, WebApi, UnitTests |
| POST hold, atomic inventory deduction | Implemented and live-tested | MongoDB transactions and conditional stock updates; concurrency and rollback smoke checks pass |
| GET hold, missing hold and expiry handling | Implemented | 404 smoke check and expiration-on-read unit test |
| DELETE hold, restore inventory, idempotency | Implemented and live-tested | Concurrent repeated releases restore stock once |
| Inventory endpoint and at least five seeded products | Implemented and live-tested | Five products returned by running API |
| Meaningful errors and HTTP statuses | Implemented and tested | Problem Details; validation 400, missing 404, stock conflict 409, unavailable 503 |
| HoldCreated, HoldReleased, HoldExpired events | Implemented | Transactional outbox, durable RabbitMQ exchange/queue and publisher confirms |
| Redis caching, TTL and mutation consistency | Implemented and tested | Revision-aware cache, 30-second default TTL, cache-fill race unit test and live stock checks |
| At least five infrastructure-free mocked unit tests | Covered | 20 xUnit cases; repository/cache mocks and outbox messaging mocks |
| React, TypeScript, Vite single-page frontend | Covered | Frontend production build passes |
| Inventory display, product quantities, create hold | Covered | Typed API data and local form validation |
| Holds with status, items and time remaining | Covered | Paginated list, countdowns, terminal history |
| Release with confirmation | Covered and tested | Native confirmation dialog; cancel does not send DELETE |
| State management and automatic synchronization | Covered and tested | TanStack Query, invalidation after mutations, five-second polling |
| API errors and loading states | Covered and tested | Error messages, preserved form input, pending-state controls |
| Frontend outside Docker | Permitted and implemented | `npm run dev`; Vite proxy to API |
| Dockerfile, README and design decisions | Present | Backend Dockerfile and component-level READMEs |
| Required AI usage documentation | Incomplete | File exists, but human audit is explicitly pending and frontend work is absent |
| Exact startup command from repository root | Missing | Compose is under Backend; root currently needs `docker compose -f Backend/docker-compose.yml up --build` |
| GitHub repository link for submission | Not verified | No repository metadata or submission URL was found at workspace root |

## Verification performed

- Frontend formatting check, ESLint, TypeScript and Vite production build passed.
- All four frontend tests passed.
- All 20 backend unit tests passed again during this review after restoring NuGet packages.
  The Docker backend image build also previously ran these tests successfully.
- Live smoke script passed validation, multi-product transaction rollback, eight concurrent
  reservations competing for the same stock, eight repeated releases, inventory consistency,
  pagination and missing-hold handling. Every hold created by that script was released.
- RabbitMQ's `inventory.holds.events` queue contained ten ready messages; this confirms queued
  delivery but does not by itself validate the payload of every event type.
- Backend readiness, frontend-proxied inventory/hold reads and allowed-origin CORS were verified
  when both applications were started earlier in this session.

## Required submission follow-up

1. Provide a root Compose entry point and root README so the exact advertised command works
   from the repository root. The frontend may continue to run separately, as the PDF permits.
2. Complete the human audit in `AI-USAGE.md` with actual accepted/rejected AI suggestions and
   reasons. Update it to cover frontend development and the now-successful Docker/live checks.
3. Remove stale statements in the backend README claiming Docker startup and smoke checks
   have not been performed.
4. Publish the intended source and supply the GitHub repository URL. Confirm the required
   two-day timeline against the actual date the assignment was received; that date is unknown.

## Remaining verification limits

- Automatic expiry and release-versus-expiry races are implemented and unit-tested, but the
  optional live expiry smoke check was not run because this environment uses 15-minute holds.
- Event payloads for all three lifecycle types and broker-outage recovery have not been
  independently inspected end to end.
- Frontend tests use mocked HTTP; browser create/release interaction against the real API has
  not been automated. Live HTTP checks cover the backend and the frontend proxy.
- No load benchmark was performed. Performance-oriented design is present, but throughput
  and latency under load are not claimed.

The functional implementation covers the requested features. Submission packaging and the
mandatory human AI audit still need completion before calling the entire assignment complete.
