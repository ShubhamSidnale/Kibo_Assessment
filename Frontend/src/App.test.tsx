// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import App from './App';
import { remaining } from './utils/time';
import type { Hold } from './api';

let stock: number;
let hold: Hold | undefined;
let failCreate: boolean;
let requests: string[];
beforeEach(() => {
    stock = 10;
    hold = undefined;
    failCreate = false;
    requests = [];
    HTMLDialogElement.prototype.showModal = function () {
        this.setAttribute('open', '');
    };
    HTMLDialogElement.prototype.close = function () {
        this.removeAttribute('open');
    };
    vi.stubGlobal(
        'fetch',
        vi.fn(async (url: string, options?: RequestInit) => {
            requests.push(`${options?.method || 'GET'} ${url}`);
            if (url.includes('/inventory')) {
                return Response.json([{ id: 'keyboard', name: 'Keyboard', availableQuantity: stock }]);
            }
            if (options?.method === 'POST') {
                if (failCreate) {
                    return Response.json({ detail: 'Insufficient stock for keyboard.' }, { status: 409 });
                }
                const { items } = JSON.parse(options.body as string);
                stock -= items[0].quantity;
                hold = {
                    id: 'hold-1',
                    items,
                    status: 'Active',
                    createdAt: new Date().toISOString(),
                    expiresAt: new Date(Date.now() + 60_000).toISOString(),
                    endedAt: null,
                };
                return Response.json(hold, { status: 201 });
            }
            if (options?.method === 'DELETE' && hold) {
                stock += hold.items[0].quantity;
                hold = { ...hold, status: 'Released', endedAt: new Date().toISOString() };
                return Response.json(hold);
            }
            return Response.json({ items: hold ? [hold] : [], nextCursor: null });
        }),
    );
});
afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
});
function setup() {
    const client = new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    render(
        <QueryClientProvider client={client}>
            <App />
        </QueryClientProvider>,
    );
    return userEvent.setup();
}
describe('inventory workflow', () => {
    it('refreshes stock and holds after creation and only releases after confirmation', async () => {
        const user = setup();
        const input = await screen.findByRole('spinbutton', { name: 'Quantity for Keyboard' });
        await user.type(input, '2');
        await user.click(screen.getByRole('button', { name: 'Reserve inventory →' }));
        await screen.findByText('Hold hold-1');
        const table = screen.getByRole('table');
        await waitFor(() => expect(within(table).getByText('8')).toBeInTheDocument());
        await user.click(screen.getByRole('button', { name: 'Release hold' }));
        expect(requests.some((request) => request.startsWith('DELETE'))).toBe(false);
        await user.click(screen.getByRole('button', { name: 'Keep hold' }));
        expect(requests.some((request) => request.startsWith('DELETE'))).toBe(false);
        await user.click(screen.getByRole('button', { name: 'Release hold' }));
        await user.click(screen.getByRole('button', { name: 'Confirm release' }));
        await screen.findByText('Released');
        await waitFor(() => expect(within(table).getByText('10')).toBeInTheDocument());
    });
    it('surfaces API errors and preserves the entered quantities', async () => {
        failCreate = true;
        const user = setup();
        const input = await screen.findByRole('spinbutton', { name: 'Quantity for Keyboard' });
        await user.type(input, '2');
        await user.click(screen.getByRole('button', { name: 'Reserve inventory →' }));
        expect(await screen.findByRole('alert')).toHaveTextContent('Insufficient stock for keyboard.');
        expect(input).toHaveValue(2);
    });
    it('rejects an empty reservation without sending a mutation', async () => {
        const user = setup();
        await screen.findByRole('spinbutton', { name: 'Quantity for Keyboard' });
        await user.click(screen.getByRole('button', { name: 'Reserve inventory →' }));
        expect(screen.getByRole('alert')).toHaveTextContent('Select at least one product');
        expect(requests.some((request) => request.startsWith('POST'))).toBe(false);
    });
    it('clamps expired countdowns to zero', () => {
        expect(remaining('2026-01-01T00:01:01Z', Date.parse('2026-01-01T00:00:00Z'))).toBe('1:01');
        expect(remaining('2026-01-01T00:00:00Z', Date.parse('2026-01-01T00:01:00Z'))).toBe('0:00');
    });
});
