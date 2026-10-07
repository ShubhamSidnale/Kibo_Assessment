import assert from 'node:assert/strict';

const base = process.env.API_URL ?? 'http://localhost:8080';
const created = new Set();
async function request(path, method = 'GET', body) {
  const response = await fetch(`${base}${path}`, {
    method, headers: body ? { 'Content-Type': 'application/json' } : {},
    body: body ? JSON.stringify(body) : undefined,
  });
  const data = await response.json();
  return { response, data };
}
async function inventory() {
  const { response, data } = await request('/api/inventory');
  assert.equal(response.status, 200);
  return data;
}
async function create(items) {
  const result = await request('/api/holds', 'POST', { items });
  if (result.response.status === 201) created.add(result.data.id);
  return result;
}
try {
  const initial = await inventory();
  assert.ok(initial.length >= 5);
  const product = initial.find(p => p.availableQuantity > 0);
  assert.ok(product, 'At least one product must have available stock');
  const invalid = await create([{ productId: product.id, quantity: 0 }]);
  assert.equal(invalid.response.status, 400);

  // The valid product is updated first; failure on the later product must roll it back.
  const missing = await create([{ productId: product.id, quantity: 1 }, { productId: 'zzzz-missing-product', quantity: 1 }]);
  assert.equal(missing.response.status, 404);
  assert.deepEqual(await inventory(), initial, 'Partial stock changes must roll back');

  const attempts = await Promise.all(Array.from({ length: 8 }, () => create([
    { productId: product.id, quantity: product.availableQuantity },
  ])));
  assert.equal(attempts.filter(a => a.response.status === 201).length, 1, 'Exactly one reservation can acquire all stock');
  assert.equal(attempts.filter(a => a.response.status === 409).length, 7);
  const winner = attempts.find(a => a.response.status === 201).data;
  assert.equal((await inventory()).find(p => p.id === product.id).availableQuantity, 0);
  assert.ok(attempts.find(a => a.response.status === 201).response.headers.get('location'));
  const releases = await Promise.all(Array.from({ length: 8 }, () => request(`/api/holds/${winner.id}`, 'DELETE')));
  for (const release of releases) {
    assert.equal(release.response.status, 200);
    assert.equal(release.data.status, 'Released');
  }
  assert.deepEqual(await inventory(), initial, 'Concurrent releases must restore inventory exactly once');
  created.delete(winner.id);
  const listed = await request('/api/holds?limit=1');
  assert.equal(listed.response.status, 200);
  assert.ok(listed.data.items.length <= 1);
  assert.equal((await request('/api/holds/does-not-exist')).response.status, 404);

  if (process.env.CHECK_EXPIRATION === '1') {
    const hold = (await create([{ productId: product.id, quantity: 1 }])).data;
    const wait = Date.parse(hold.expiresAt) - Date.now() + 1500;
    assert.ok(wait < 75_000, 'Use HOLD_DURATION_MINUTES=1 for the expiration check');
    await new Promise(resolve => setTimeout(resolve, Math.max(wait, 0)));
    const ended = await Promise.all([
      request(`/api/holds/${hold.id}`), request(`/api/holds/${hold.id}`, 'DELETE'),
    ]);
    for (const result of ended) assert.equal(result.data.status, 'Expired');
    assert.deepEqual(await inventory(), initial, 'Expiration/release race must restore inventory exactly once');
    created.delete(hold.id);
  }
  console.log('Backend smoke checks passed: validation, rollback, concurrency, release, cache consistency, listing, and 404.');
} finally {
  for (const id of created) {
    await request(`/api/holds/${id}`, 'DELETE').catch(error => console.error(`Cleanup failed for ${id}:`, error.message));
  }
}
