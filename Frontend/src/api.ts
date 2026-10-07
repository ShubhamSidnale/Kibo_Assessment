export interface Product {
    id: string;
    name: string;
    availableQuantity: number;
}
export interface HoldItem {
    productId: string;
    quantity: number;
}
export type HoldStatus = 'Active' | 'Released' | 'Expired';
export interface Hold {
    id: string;
    items: HoldItem[];
    status: HoldStatus;
    createdAt: string;
    expiresAt: string;
    endedAt: string | null;
}
export interface HoldPage {
    items: Hold[];
    nextCursor: string | null;
}
export interface ProblemDetails {
    title?: string;
    detail?: string;
    traceId?: string;
    errors?: Record<string, string[]>;
}

const baseUrl = (import.meta.env.VITE_API_BASE_URL || '/api').replace(/\/$/, '');
export async function request<T>(path: string, options?: RequestInit): Promise<T> {
    let response: Response;
    try {
        response = await fetch(`${baseUrl}${path}`, options);
    } catch (error) {
        if (error instanceof Error && error.name === 'AbortError') {
            throw error;
        }
        throw new Error('Cannot reach the API. Check that the backend is running and try again.');
    }
    if (!response.ok) {
        const problem = (await response.json().catch(() => ({}))) as ProblemDetails;
        const validation = Object.values(problem.errors || {})
            .flat()
            .join(' ');
        throw new Error(
            validation || problem.detail || problem.title || `Request failed (${response.status}).`,
        );
    }
    return response.json() as Promise<T>;
}
export const api = {
    inventory: (signal?: AbortSignal) => request<Product[]>('/inventory', { signal }),
    holds: (cursor: string | null, signal?: AbortSignal) =>
        request<HoldPage>(`/holds?limit=50${cursor ? `&cursor=${encodeURIComponent(cursor)}` : ''}`, {
            signal,
        }),
    create: (items: HoldItem[]) =>
        request<Hold>('/holds', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ items }),
        }),
    release: (id: string) => request<Hold>(`/holds/${encodeURIComponent(id)}`, { method: 'DELETE' }),
};
