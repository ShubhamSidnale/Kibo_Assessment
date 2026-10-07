import type { Product } from '../api';
import type { InventoryWorkspace } from '../hooks/useInventoryWorkspace';

interface InventoryPanelProps {
    inventory: InventoryWorkspace['inventory'];
    products: Product[];
}

export function InventoryPanel({ inventory, products }: InventoryPanelProps) {
    return (
        <section className="panel inventory">
            <div className="panel-heading">
                <div>
                    <h2>Current inventory</h2>
                    <p>Available stock, after active reservations.</p>
                </div>
                <span className="tag">LIVE STOCK</span>
            </div>
            {inventory.isPending && (
                <p role="status" className="empty">
                    Loading inventory…
                </p>
            )}
            {inventory.isError && (
                <p role="alert" className="error">
                    {inventory.error.message}{' '}
                    <button className="text-button" onClick={() => void inventory.refetch()}>
                        Retry inventory
                    </button>
                </p>
            )}
            {inventory.data && (
                <table>
                    <thead>
                        <tr>
                            <th>Product</th>
                            <th>Available</th>
                            <th>Stock status</th>
                        </tr>
                    </thead>
                    <tbody>
                        {products.map((product) => (
                            <tr key={product.id}>
                                <td>
                                    <strong>{product.name}</strong>
                                    <small>{product.id}</small>
                                </td>
                                <td className="quantity">{product.availableQuantity.toLocaleString()}</td>
                                <td>
                                    <span
                                        className={
                                            product.availableQuantity === 0 ? 'badge empty-stock' : 'badge'
                                        }
                                    >
                                        {product.availableQuantity === 0 ? 'Out of stock' : 'In stock'}
                                    </span>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            )}
            {inventory.data && !products.length && <p className="empty">No products in the inventory yet.</p>}
        </section>
    );
}
