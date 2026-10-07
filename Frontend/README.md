# Inventory frontend

Single-page React + TypeScript application built with Vite. Runs outside Docker alongside the existing backend.

## Run

Requires Node.js 22.15+ (or Node.js 20.19+).

Start the backend using `Backend/README.md` (API defaults to `http://localhost:8080`), then from this directory:

```sh
npm install
npm run dev
```

Open `http://localhost:5173`. On Windows, use `npm.cmd` if PowerShell blocks `npm.ps1`.

Vite proxies `/api` to the backend, avoiding browser CORS setup for local development. Copy `.env.example` to `.env` to change `API_PROXY_TARGET`. For a deployed build, set `VITE_API_BASE_URL` to the backend's API URL before building and allow the frontend origin in backend CORS. Vite's development proxy does not apply to production builds or preview.

```sh
npm run build
npm test
npm run preview
```

## Coding standards

See [CODING-STANDARDS.md](CODING-STANDARDS.md) for formatting and naming conventions.
Use `npm run format` to format source, `npm run lint:fix` to add required braces,
and `npm run check` to verify formatting and the 120-character source line limit.

## State and behavior

- TanStack Query owns inventory and paginated hold server state. Component state handles quantity input, release confirmation, notifications, and the local clock.
- Creating/releasing a hold cancels stale reads and invalidates both query caches, awaiting refresh before enabling further mutations. Failed mutations also refresh, because a lost response can occur after the server commits. API failures preserve form input and surface Problem Details messages.
- Both lists refresh every five seconds and on window focus. Countdown ticks every second from the server's UTC expiry time. Reaching zero displays “Awaiting expiration confirmation”; only the server decides whether the hold has expired and stock is restored.
- Holds include active reservations and terminal history with cursor pagination. Counts explicitly describe loaded records rather than claiming a global total. “Load more” remains available when an empty page includes a cursor.
- Release requires a native modal confirmation with focus restoration. Pending mutations disable conflicting actions. Forms validate whole positive quantities against the last known inventory; the API remains authoritative for concurrent stock changes.
- Tests use mocked HTTP responses to exercise create/release inventory synchronization, release confirmation, API conflict errors, empty input, and countdown behavior. A running backend is needed to verify real service integration.
