# AI usage record

## Strategy

Used OpenAI Codex to read the assessment PDF, extract requirements, propose an implementation order, and implement the backend. Context included the complete extracted PDF requirements, the empty repository, available SDKs, and the user's instruction to finish the backend before starting the frontend. Work was organized around Contracts, Domain, Infrastructure, WebApi, and UnitTests. No sub-agents were used.

## Review and decisions

The following are AI implementation decisions, pending human review; they are not claimed as human approvals:

- Chose conditional stock updates inside a transaction instead of independent product deductions: the latter could partially deduct a multi-product request.
- Chose conditional terminal status transitions rather than separate read/restore operations to prevent duplicate restoration during release/expiry races.
- Chose a durable outbox rather than publishing immediately after committing a hold, which can lose events during a broker outage.
- Chose revision-keyed inventory caching rather than simple delete-after-write invalidation, which allows an in-flight reader to repopulate stale stock.
- Chose an expiration worker rather than a MongoDB TTL index on holds: deleting the hold does not restore stock or emit an event.

**Human audit is pending.** Before submission, the candidate must record specific suggestions they personally accepted and rejected, with their reasons. No human decisions will be invented here.

## Verification

Codex generated unit tests with strict Moq mocks covering request validation, hold creation and configured expiration, missing holds, stock conflicts, expiration-on-read, terminal release behavior, expiry sweeps, pagination validation, a concurrent cache-fill mutation, and confirmed/retried event publishing.

The official MongoDB transaction API and RabbitMQ publisher-confirm documentation were consulted:

- https://mongodb.github.io/mongo-csharp-driver/3.5.0/api/MongoDB.Driver/MongoDB.Driver.IClientSession.WithTransactionAsync.html
- https://www.rabbitmq.com/tutorials/tutorial-seven-dotnet

Verified with local .NET SDK 10.0.401:

- Release solution build succeeded with zero warnings and zero errors.
- Release API publish succeeded; local output is in the ignored `artifacts/backend` directory.
- All 20 unit-test cases passed, with zero skipped cases.
- The Node smoke script passed its syntax check.
- NuGet restore initially flagged vulnerable compression dependencies in MongoDB.Driver 3.5.0. Updated to MongoDB.Driver 3.12.0 and RabbitMQ.Client 7.2.2; restore and build then succeeded without audit warnings.
- Two initial outbox test failures exposed a fixture that recreated events with different collection identities. The fixture now reuses one event, preserving strict argument matching and publish-before-complete verification.

Docker is not installed on the development machine. Real stock races and transaction rollback require infrastructure; `scripts/backend-smoke.mjs` provides executable checks for those once Docker is available. Unit mocks alone do not prove MongoDB concurrency guarantees. Compose startup and the live smoke script remain unverified.
