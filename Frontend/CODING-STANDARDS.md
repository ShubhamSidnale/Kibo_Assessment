# Frontend coding standards

- Use TypeScript with strict typing for application code and API contracts.
- Use four spaces for indentation, matching the backend convention. Do not use tabs.
- Keep source lines at or below 120 characters. Prettier targets 110 to leave room for indentation;
  ESLint enforces the 120-character maximum for JavaScript, TypeScript, and TSX.
- Always use braces for control flow, including single-statement `if`, `else`, and loops.
- Keep statements on separate lines and expand JSX, objects, and CSS into readable blocks.
- Use single quotes in TypeScript, semicolons, spaces inside object braces, and parenthesized arrow parameters.
- Use PascalCase for components and types, camelCase for functions and variables, and descriptive names.
- Keep server state in TanStack Query and UI state in React. Preserve typed API responses and visible errors.
- Use UTF-8, LF line endings, a final newline, and no trailing whitespace.
- Do not format generated files, dependency directories, or the package lockfile.
- Split components by responsibility; keep shared server access in hooks and pure helpers in utilities.
- Comment intent, consistency rules, and performance decisions rather than restating obvious code.
- Treat performance as a priority: keep frequent updates local, avoid repeated collection scans,
  reuse expensive derived data when inputs are unchanged, and measure before adding more optimization.

## Source structure

- `src/App.tsx`: page layout, mutation coordination, and release confirmation.
- `src/components/`: inventory display, hold creation, holds display, and isolated countdowns.
- `src/hooks/useInventoryWorkspace.ts`: cached API reads and inventory/hold synchronization.
- `src/api.ts`: typed API contracts and HTTP calls.
- `src/utils/time.ts`: pure countdown formatting.

## Commands

```sh
npm run format
npm run lint:fix
npm run check
npm run build
npm test
```

`format` applies Prettier; `lint:fix` adds missing control-flow braces where safe.
`check` verifies formatting and lint rules without modifying files.
If a string or expression still exceeds 120 characters, split it into readable parts and rerun the checks.
