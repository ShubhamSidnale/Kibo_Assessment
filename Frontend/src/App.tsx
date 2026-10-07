import { useEffect, useMemo, useRef, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { api, type Hold, type Product } from './api';
import { useInventoryWorkspace } from './hooks/useInventoryWorkspace';
import { InventoryPanel } from './components/InventoryPanel';
import { CreateHoldForm } from './components/CreateHoldForm';
import { HoldsPanel } from './components/HoldsPanel';

const emptyProducts: Product[] = [];

export default function App() {
    const { inventory, holds, synchronize } = useInventoryWorkspace();
    const [notice, setNotice] = useState('');
    const [confirm, setConfirm] = useState<Hold | null>(null);
    const dialog = useRef<HTMLDialogElement>(null);
    const releaseTrigger = useRef<HTMLButtonElement | null>(null);

    // Return keyboard focus to the release button when the confirmation closes.
    useEffect(() => {
        if (confirm) {
            dialog.current?.showModal();
        } else if (dialog.current?.open) {
            dialog.current.close();
            releaseTrigger.current?.focus();
        }
    }, [confirm]);

    const create = useMutation({
        mutationFn: api.create,
        onSuccess: async () => {
            setNotice('Hold created. Stock is reserved until the hold expires or is released.');
            await synchronize();
        },
        // A lost response may follow a committed mutation; refresh authoritative stock.
        onError: synchronize,
    });

    const release = useMutation({
        mutationFn: api.release,
        onSuccess: async (hold) => {
            setConfirm(null);
            setNotice(`Hold ${hold.status.toLowerCase()}. Inventory has been updated.`);
            await synchronize();
        },
        onError: synchronize,
    });

    const products = inventory.data || emptyProducts;

    // Index product names once per inventory change instead of scanning for every hold item.
    const productNames = useMemo(
        () => new Map(products.map((product) => [product.id, product.name])),
        [products],
    );

    // Reuse the deduplicated list until the paginated API data changes.
    const allHolds = useMemo(
        () => [
            ...new Map(
                (holds.data?.pages.flatMap((page) => page.items) || []).map((hold) => [hold.id, hold]),
            ).values(),
        ],
        [holds.data],
    );

    const activeCount = allHolds.filter((hold) => hold.status === 'Active').length;
    const busy = create.isPending || release.isPending;

    return (
        <div className="shell">
            <header>
                <a className="brand" href="#">
                    KIBO<span> Inventory</span>
                </a>
                <span className="environment">Inventory workspace</span>
            </header>
            <main>
                <div className="intro">
                    <div>
                        <p className="eyebrow">STOCK & RESERVATIONS</p>
                        <h1>Inventory overview</h1>
                        <p>Reserve what you need. Keep every unit accounted for.</p>
                    </div>
                    <button
                        className="secondary"
                        disabled={inventory.isFetching || holds.isFetching}
                        onClick={() => void synchronize()}
                    >
                        {inventory.isFetching || holds.isFetching ? 'Syncing…' : '↻ Refresh data'}
                    </button>
                </div>
                <div className="stats">
                    <div>
                        <span>Available units</span>
                        <strong>
                            {inventory.data
                                ? products
                                      .reduce((sum, product) => sum + product.availableQuantity, 0)
                                      .toLocaleString()
                                : '—'}
                        </strong>
                    </div>
                    <div>
                        <span>Products</span>
                        <strong>{inventory.data ? products.length : '—'}</strong>
                    </div>
                    <div>
                        <span>Active holds · loaded</span>
                        <strong>{holds.data ? activeCount : '—'}</strong>
                    </div>
                    <div className="sync-note">
                        ↻
                        <span>
                            Automatically syncs every 5 seconds
                            <br />
                            <small>Expiration times are set by the server</small>
                        </span>
                    </div>
                </div>
                {notice && (
                    <p className="notice" role="status">
                        {notice}
                    </p>
                )}
                <div className="workspace">
                    <InventoryPanel inventory={inventory} products={products} />
                    <CreateHoldForm
                        products={products}
                        busy={busy}
                        unavailable={!inventory.data || inventory.isError}
                        create={create}
                        onStart={() => setNotice('')}
                    />
                </div>
                <HoldsPanel
                    holds={holds}
                    allHolds={allHolds}
                    productNames={productNames}
                    activeCount={activeCount}
                    busy={busy}
                    onRelease={(hold, trigger) => {
                        releaseTrigger.current = trigger;
                        release.reset();
                        setConfirm(hold);
                    }}
                />
                <footer>
                    Inventory Hold Service<span>Built for a clear view of your stock.</span>
                </footer>
            </main>
            <dialog
                ref={dialog}
                onCancel={(event) => {
                    event.preventDefault();
                    if (!release.isPending) {
                        setConfirm(null);
                    }
                }}
                aria-labelledby="release-title"
            >
                <h2 id="release-title">Release this hold?</h2>
                <p>The reserved quantities will return to available inventory.</p>
                <p className="dialog-id">{confirm?.id}</p>
                {release.isError && (
                    <p className="error" role="alert">
                        {release.error.message}
                    </p>
                )}
                <div className="dialog-actions">
                    <button
                        className="secondary"
                        autoFocus
                        disabled={release.isPending}
                        onClick={() => setConfirm(null)}
                    >
                        Keep hold
                    </button>
                    <button
                        className="primary"
                        disabled={release.isPending}
                        onClick={() => confirm && release.mutate(confirm.id)}
                    >
                        {release.isPending ? 'Releasing…' : 'Confirm release'}
                    </button>
                </div>
            </dialog>
        </div>
    );
}
