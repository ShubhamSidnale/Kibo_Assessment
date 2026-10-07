import type { Hold } from '../api';
import type { InventoryWorkspace } from '../hooks/useInventoryWorkspace';
import { HoldCountdown } from './HoldCountdown';

interface HoldsPanelProps {
    holds: InventoryWorkspace['holds'];
    allHolds: Hold[];
    productNames: Map<string, string>;
    activeCount: number;
    busy: boolean;
    onRelease: (hold: Hold, trigger: HTMLButtonElement) => void;
}

export function HoldsPanel({ holds, allHolds, productNames, activeCount, busy, onRelease }: HoldsPanelProps) {
    return (
        <section className="panel holds">
            <div className="panel-heading">
                <div>
                    <h2>Holds</h2>
                    <p>Active reservations and their release history.</p>
                </div>
                <span className="tag">{activeCount} ACTIVE LOADED</span>
            </div>
            {holds.isPending && (
                <p className="empty" role="status">
                    Loading holds…
                </p>
            )}
            {holds.isError && (
                <p role="alert" className="error">
                    {holds.error.message}{' '}
                    <button className="text-button" onClick={() => void holds.refetch()}>
                        Retry holds
                    </button>
                </p>
            )}
            {holds.data && !allHolds.length && (
                <div className="empty">
                    <strong>No holds yet</strong>
                    <p>Create your first hold using the form above.</p>
                </div>
            )}
            <div className="hold-list">
                {allHolds.map((hold) => (
                    <article className="hold-card" key={hold.id}>
                        <div className="hold-top">
                            <span className={`badge ${hold.status.toLowerCase()}`}>{hold.status}</span>
                            <HoldCountdown hold={hold} />
                        </div>
                        <h3 title={hold.id}>Hold {hold.id}</h3>
                        <ul>
                            {hold.items.map((item) => (
                                <li key={item.productId}>
                                    <span>{productNames.get(item.productId) || item.productId}</span>
                                    <strong>× {item.quantity}</strong>
                                </li>
                            ))}
                        </ul>
                        <div className="hold-footer">
                            <small>Created {new Date(hold.createdAt).toLocaleString()}</small>
                            {hold.status === 'Active' && (
                                <button
                                    className="secondary"
                                    disabled={busy}
                                    onClick={(event) => {
                                        onRelease(hold, event.currentTarget);
                                    }}
                                >
                                    Release hold
                                </button>
                            )}
                        </div>
                    </article>
                ))}
            </div>
            {holds.hasNextPage && (
                <div className="load-more">
                    <button
                        className="secondary"
                        disabled={holds.isFetching}
                        onClick={() => void holds.fetchNextPage()}
                    >
                        {holds.isFetchingNextPage ? 'Loading…' : 'Load more holds'}
                    </button>
                </div>
            )}
        </section>
    );
}
