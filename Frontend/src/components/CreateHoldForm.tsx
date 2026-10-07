import { useState, type FormEvent } from 'react';
import type { UseMutationResult } from '@tanstack/react-query';
import type { Product, Hold, HoldItem } from '../api';

interface CreateHoldFormProps {
    products: Product[];
    busy: boolean;
    unavailable: boolean;
    create: UseMutationResult<Hold, Error, HoldItem[], unknown>;
    onStart: () => void;
}

/** Keep quantity edits local to avoid rendering other panels on each keystroke. */
export function CreateHoldForm({ products, busy, unavailable, create, onStart }: CreateHoldFormProps) {
    const [quantities, setQuantities] = useState<Record<string, string>>({});
    const [formError, setFormError] = useState('');

    function submit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        setFormError('');
        onStart();
        create.reset();

        const items: HoldItem[] = [];

        // Client validation gives quick feedback; the API still checks concurrent stock changes.
        for (const product of products) {
            const raw = quantities[product.id] || '';
            if (!raw || raw === '0') {
                continue;
            }

            const quantity = Number(raw);
            if (!Number.isSafeInteger(quantity) || quantity < 1 || quantity > product.availableQuantity) {
                setFormError(
                    `Enter a whole quantity between 1 and ${product.availableQuantity} for ${product.name}.`,
                );
                return;
            }

            items.push({ productId: product.id, quantity });
        }

        if (!items.length) {
            setFormError('Select at least one product by entering a quantity.');
            return;
        }

        // Preserve entered quantities on failure so the user can adjust and retry.
        create.mutate(items, { onSuccess: () => setQuantities({}) });
    }

    return (
        <section className="panel create">
            <div className="panel-heading">
                <div>
                    <h2>Create a hold</h2>
                    <p>Choose products and quantities to reserve.</p>
                </div>
            </div>
            <form onSubmit={submit}>
                <fieldset disabled={busy || unavailable}>
                    <legend className="sr-only">Product quantities</legend>
                    {products.map((product) => (
                        <label className="product-input" key={product.id}>
                            <span>
                                {product.name}
                                <small>{product.availableQuantity} available</small>
                            </span>
                            <input
                                aria-label={`Quantity for ${product.name}`}
                                type="number"
                                min="0"
                                max={product.availableQuantity}
                                step="1"
                                placeholder="0"
                                disabled={product.availableQuantity === 0}
                                value={quantities[product.id] || ''}
                                onChange={(event) => {
                                    setQuantities((previous) => ({
                                        ...previous,
                                        [product.id]: event.target.value,
                                    }));
                                    setFormError('');
                                    if (create.isError) {
                                        create.reset();
                                    }
                                }}
                            />
                        </label>
                    ))}
                    <p className="hint">
                        Enter a quantity to include a product. Unselected products stay available.
                    </p>
                    <button className="primary" type="submit" disabled={!products.length}>
                        {create.isPending ? 'Creating hold…' : 'Reserve inventory →'}
                    </button>
                </fieldset>
                {(formError || create.isError) && (
                    <p role="alert" className="error">
                        {formError || create.error?.message}
                    </p>
                )}
            </form>
        </section>
    );
}
