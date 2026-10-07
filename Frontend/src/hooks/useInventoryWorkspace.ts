import { useInfiniteQuery, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../api';

/** Share cached API data across the dashboard and mutation workflows. */
export function useInventoryWorkspace() {
    const client = useQueryClient();
    const inventory = useQuery({ queryKey: ['inventory'], queryFn: ({ signal }) => api.inventory(signal) });
    const holds = useInfiniteQuery({
        queryKey: ['holds'],
        initialPageParam: null as string | null,
        queryFn: ({ pageParam, signal }) => api.holds(pageParam, signal),
        getNextPageParam: (page) => page.nextCursor ?? undefined,
    });

    // Prevent an older response from replacing inventory fetched after a mutation.
    const synchronize = async () => {
        await Promise.all([
            client.cancelQueries({ queryKey: ['inventory'] }),
            client.cancelQueries({ queryKey: ['holds'] }),
        ]);
        await Promise.all([
            client.invalidateQueries({ queryKey: ['inventory'] }),
            client.invalidateQueries({ queryKey: ['holds'] }),
        ]);
    };

    return { inventory, holds, synchronize };
}

export type InventoryWorkspace = ReturnType<typeof useInventoryWorkspace>;
