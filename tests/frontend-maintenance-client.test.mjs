import test from 'node:test';
import assert from 'node:assert/strict';
import { createMaintenanceService } from '../src/ItAssetManagement.Api/wwwroot/js/services/maintenance-service.js';
import { ServiceError } from '../src/ItAssetManagement.Api/wwwroot/js/services/service-error.js';

const rowVersion = Buffer.alloc(16, 7).toString('base64');
function fixture() {
  const calls = [];
  const service = createMaintenanceService({ idPath: String, request: async (...args) => { calls.push(args); return { id: 4 }; } });
  return { service, calls };
}
test('maintenance commands use canonical routes and preserve concurrency and explicit cost clearing', async () => {
  const { service, calls } = fixture();
  await service.create({ assetId: 2, description: 'Fan failure', priority: 'HIGH' });
  await service.assign(4, { rowVersion, technicianId: 7, note: 'Assign technician' });
  await service.start(4, { rowVersion, note: 'Start work' });
  await service.resolve(4, { rowVersion, resolution: 'Fan replaced', actualCost: 123.45 });
  await service.update(4, { rowVersion, actualCost: null, note: 'Clear incorrect estimate' });
  await service.fail(4, { rowVersion, reason: 'Cannot repair' });
  await service.cancel(4, { rowVersion, reason: 'Duplicate request' });
  assert.deepEqual(calls.map(([url, options]) => [url, options.method]), [
    ['/maintenance-tickets', 'POST'], ['/maintenance-tickets/4/assign', 'POST'],
    ['/maintenance-tickets/4/start', 'POST'], ['/maintenance-tickets/4/resolve', 'POST'],
    ['/maintenance-tickets/4', 'PUT'], ['/maintenance-tickets/4/fail', 'POST'], ['/maintenance-tickets/4/cancel', 'POST']
  ]);
  assert.equal(calls[4][1].body.actualCost, null);
  assert.equal(calls[1][1].body.rowVersion, rowVersion);
  assert.equal(Object.hasOwn(calls[0][1].body, 'ticketCode'), false);
});
test('maintenance rejects malformed or unsafe writes before transport', async () => {
  const { service, calls } = fixture();
  for (const operation of [
    () => service.start(4, null), () => service.cancel(4, []),
    () => service.create({ assetId: 2, description: 'Fault', ticketCode: 'BT999999' }),
    () => service.assign(4, { rowVersion, technicianId: 0, note: 'Assign' }),
    () => service.start(4, { rowVersion: 'bad', note: 'Start' }),
    () => service.resolve(4, { rowVersion, resolution: 'Done', actualCost: 1.001 }),
    () => service.resolve(4, { rowVersion, resolution: 'Done', actualCost: 1e16 }),
    () => service.fail(4, { rowVersion, reason: ' ' }),
    () => service.update(4, { rowVersion, note: 'Edit', dueAt: '2026-02-31T00:00:00Z' }),
    () => service.start(4, { rowVersion, note: 'Start', startedAt: '2026-10-25T10:00:00+07:00' })
  ]) await assert.rejects(operation, error => error instanceof ServiceError && error.status === 400);
  assert.equal(calls.length, 0);
});
test('maintenance filters, history and technician lookup preserve query boundaries', async () => {
  const { service, calls } = fixture();
  await service.list({ assetId: 2, priority: 'HIGH', keyword: 'A&B', status: 'PENDING', sortBy: 'priority', sortDirection: 'desc' });
  await service.history(4, { eventType: 'COST_UPDATED', page: 2 });
  await service.technicians({ keyword: 'Thủy', pageSize: 20 });
  assert.equal(new URL(calls[0][0], 'http://fixture.test').searchParams.get('keyword'), 'A&B');
  assert.match(calls[1][0], /^\/maintenance-tickets\/4\/history\?/);
  assert.match(calls[2][0], /^\/maintenance-tickets\/technicians\?/);
  for (const query of [{ status: 'UNKNOWN' }, { pageSize: 101 }, { page: 2147483647, pageSize: 100 },
    { from: '2026-10-25T00:00:00Z', to: '2026-10-24T00:00:00Z' }, { technicianId: true }, { unsupported: 1 }]) {
    assert.throws(() => service.list(query), { status: 400 });
  }
  assert.equal(calls.length, 3);
});
test('maintenance propagates rollout and stale-version errors without retrying writes', async () => {
  for (const status of [401, 403, 409, 503]) {
    let count = 0;
    const service = createMaintenanceService({ idPath: String, request: async () => { count++; throw new ServiceError(status, 'FIXTURE', 'Failure'); } });
    await assert.rejects(service.cancel(4, { rowVersion, reason: 'Cancel' }), { status });
    assert.equal(count, 1);
  }
});
