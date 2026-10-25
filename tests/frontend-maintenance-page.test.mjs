import test from 'node:test';
import assert from 'node:assert/strict';
import { maintenancePage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/maintenance.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

const version = Buffer.alloc(16, 7).toString('base64');
const ticket = { id: 4, ticketCode: 'BT000004', assetCode: 'AST-000004', assetName: 'Laptop', title: '<script>Fault</script>',
  description: 'Fan failure', status: 'PENDING', priority: 'HIGH', assignedToUserId: 7, rowVersion: version, actualCost: 987654 };
const page = items => ({ items, page: 1, pageSize: 20, totalItems: items.length, totalPages: 1 });
const button = (node, text) => descendants(node).find(item => item.tagName === 'BUTTON' && item.textContent === text);
const input = name => document.getElementById('field-maint-' + name);
function fixture(role = 'ADMIN_IT', permissions = ['maintenance.read', 'maintenance.create', 'maintenance.assign', 'maintenance.work', 'maintenance.resolve', 'maintenance.update', 'maintenance.cancel', 'maintenance.cost.write', 'assets.read']) {
  const calls = [];
  return { calls, services: { maintenanceEnabled: true, auth: { session: { id: 7, roles: [role], permissions } },
    assets: { list: async () => page([{ id: 2, assetCode: 'AST-000002', name: 'Laptop' }]) },
    maintenance: {
      list: async query => { calls.push(['list', query]); return page([structuredClone(ticket)]); },
      get: async id => { calls.push(['get', id]); return structuredClone(ticket); },
      history: async (id, query) => { calls.push(['history', id, query]); return page([]); },
      create: async dto => { calls.push(['create', dto]); return { id: 5 }; },
      start: async (id, dto) => { calls.push(['start', id, dto]); return {}; }
    } } };
}
test('maintenance page denies disabled or unauthorized access before transport', async () => {
  const dom = installDom('/maintenance');
  try {
    const { services, calls } = fixture(); services.maintenanceEnabled = false;
    await assert.rejects(maintenancePage(services), { status: 404 });
    services.maintenanceEnabled = true; services.auth.session.permissions = [];
    await assert.rejects(maintenancePage(services), { status: 403 }); assert.equal(calls.length, 0);
  } finally { dom.restore(); }
});
test('technician UI masks costs and hides global assignment/cancel controls', async () => {
  const dom = installDom('/maintenance');
  try {
    const { services } = fixture('TECHNICAL_SUPPORT', ['maintenance.read', 'maintenance.work']);
    const root = await maintenancePage(services); dom.body.append(root);
    assert.ok(button(root, 'Bắt đầu xử lý')); assert.equal(button(root, 'Giao kỹ thuật viên'), undefined);
    assert.equal(button(root, 'Hủy phiếu'), undefined); assert.doesNotMatch(root.textContent, /987654/);
    assert.equal(descendants(root).some(item => item.tagName === 'SCRIPT'), false);
  } finally { dom.restore(); }
});
test('maintenance create sends backend-generated code and one write despite repeated clicks', async () => {
  const dom = installDom('/maintenance');
  try {
    let refresh = 0; window.dispatchEvent = event => { if (event.type === 'frontend-refresh') refresh++; };
    const { services, calls } = fixture(); const root = await maintenancePage(services); dom.body.append(root);
    button(root, 'Mở phiếu bảo trì').fire('click');
    await until(() => input('target')?.textContent.includes('AST-000002'));
    input('target').value = '2'; input('title').value = 'Fan'; input('description').value = 'Broken fan';
    const confirm = button(dom.body, 'Xác nhận'); confirm.fire('click'); confirm.fire('click');
    await until(() => refresh === 1);
    const writes = calls.filter(([kind]) => kind === 'create'); assert.equal(writes.length, 1);
    assert.equal(writes[0][1].assetId, 2); assert.equal(Object.hasOwn(writes[0][1], 'ticketCode'), false);
  } finally { dom.restore(); }
});
test('maintenance start uses refreshed version and blocks blind retries after conflict', async () => {
  const dom = installDom('/maintenance');
  try {
    const { services, calls } = fixture(); let writes = 0;
    services.maintenance.start = async () => { writes++; throw Object.assign(new Error('Stale'), { status: 409 }); };
    const root = await maintenancePage(services); dom.body.append(root);
    button(root, 'Bắt đầu xử lý').fire('click'); await until(() => Boolean(input('note')));
    input('note').value = 'Begin repair'; const confirm = button(dom.body, 'Xác nhận'); confirm.fire('click');
    await until(() => Boolean(button(dom.body, 'Đóng và tải lại'))); confirm.fire('click');
    assert.equal(writes, 1); assert.ok(calls.some(([kind]) => kind === 'get'));
  } finally { dom.restore(); }
});
test('maintenance detail loads paged history and discards responses after session changes', async () => {
  const dom = installDom('/maintenance/4?historyPage=2');
  try {
    const { services, calls } = fixture(); const root = await maintenancePage(services, 4); dom.body.append(root);
    assert.deepEqual(calls.find(([kind]) => kind === 'history'), ['history', 4, { page: 2, pageSize: 20 }]);
    services.maintenance.get = async () => { services.auth.session = { id: 8, roles: [], permissions: [] }; return ticket; };
    await assert.rejects(maintenancePage(services, 4), { status: 401 });
  } finally { dom.restore(); }
});
