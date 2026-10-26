import test from 'node:test';
import assert from 'node:assert/strict';
import { assignmentsPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/assignments.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

const version = Buffer.alloc(16, 7).toString('base64');
const row = { id: 1, assetId: 10, assetCode: 'AST-000010', assetName: '<script>Fixture</script>', assignedUserId: 2,
  assignedUserName: 'Người nhận cũ', assignedAt: '2026-10-01T08:00:00Z', returnedAt: null, rowVersion: version, purchaseCost: 999999 };
const page = items => ({ items, page: 1, pageSize: 20, totalItems: items.length, totalPages: 1 });
const findButton = (node, text) => descendants(node).find(item => item.tagName === 'BUTTON' && item.textContent === text);
const input = name => document.getElementById('field-' + name);
function fixture(grants = ['assignments.read', 'assignments.assign', 'assignments.return', 'assignments.transfer', 'assets.read', 'users.lookup', 'departments.read']) {
  const calls = [], services = { assignmentEnabled: true, mode: 'api', auth: { session: { id: 7, roles: ['ADMIN_IT'], permissions: grants } },
    assets: { list: async () => page([{ id: 10, assetCode: 'AST-000010', name: 'Trong kho', isArchived: false }]) },
    users: { lookup: async () => page([{ id: 4, displayName: 'Người nhận mới' }]) },
    departments: { list: async () => page([{ id: 9, name: 'Kỹ thuật' }]) },
    assignments: { list: async query => { calls.push(['list', query]); return page([structuredClone(row)]); },
      get: async id => { calls.push(['get', id]); return structuredClone(row); },
      history: async (id, query) => { calls.push(['history', id, query]); return page([structuredClone(row)]); },
      assign: async dto => { calls.push(['assign', dto]); return {}; },
      returnAsset: async (id, dto) => { calls.push(['return', id, dto]); return {}; },
      transfer: async (id, dto) => { calls.push(['transfer', id, dto]); return {}; } }
  };
  return { services, calls };
}

test('assignment UI is closed by default and denies missing read grant without any request', async () => {
  const dom = installDom('/assignments');
  try {
    const { services, calls } = fixture(); services.assignmentEnabled = false;
    await assert.rejects(assignmentsPage(services), { status: 404 });
    services.assignmentEnabled = true; services.auth.session.permissions = [];
    await assert.rejects(assignmentsPage(services), { status: 403 }); assert.equal(calls.length, 0);
  } finally { dom.restore(); }
});

test('read-only assignment list shows safe labels without cost or mutation controls', async () => {
  const dom = installDom('/assignments');
  try {
    const { services } = fixture(['assignments.read']); const root = await assignmentsPage(services); dom.body.append(root);
    assert.match(root.textContent, /Người nhận cũ/); assert.match(root.textContent, /AST-000010/);
    assert.equal(findButton(root, 'Cấp phát tài sản'), undefined); assert.equal(findButton(root, 'Thu hồi'), undefined);
    assert.equal(findButton(root, 'Điều chuyển'), undefined); assert.doesNotMatch(root.textContent, /999999/);
    assert.equal(descendants(root).some(item => item.tagName === 'SCRIPT'), false);
  } finally { dom.restore(); }
});

for (const mode of ['assign', 'return', 'transfer']) test(`assignment ${mode} dialog uses selected target and fresh version exactly once`, async () => {
  const dom = installDom('/assignments');
  try {
    let refreshed = 0; window.dispatchEvent = event => { if (event.type === 'frontend-refresh') refreshed++; };
    const { services, calls } = fixture(); const root = await assignmentsPage(services); dom.body.append(root);
    const open = mode === 'assign' ? 'Cấp phát tài sản' : mode === 'return' ? 'Thu hồi' : 'Điều chuyển';
    findButton(root, open).fire('click');
    const label = mode === 'assign' ? 'Xác nhận cấp phát' : mode === 'return' ? 'Xác nhận thu hồi' : 'Xác nhận điều chuyển';
    await until(() => Boolean(findButton(dom.body, label)));
    if (mode !== 'return') {
      await until(() => input('assignmentRecipient')?.textContent.includes('Người nhận mới'));
      input('assignmentRecipient').value = '4';
      if (mode === 'assign') { await until(() => input('assignmentAsset')?.textContent.includes('AST-000010')); input('assignmentAsset').value = '10'; }
    }
    input('assignmentNote').value = 'Bàn giao thực tế';
    const confirm = findButton(dom.body, label); confirm.fire('click'); confirm.fire('click');
    await until(() => refreshed === 1);
    const writes = calls.filter(([kind]) => kind === mode); assert.equal(writes.length, 1);
    const dto = writes[0].at(-1); assert.equal(dto.note, 'Bàn giao thực tế');
    if (mode === 'assign') { assert.equal(dto.assetId, 10); assert.equal(dto.userId, 4); }
    else assert.equal(dto.rowVersion, version);
    if (mode === 'transfer') assert.equal(dto.userId, 4);
  } finally { dom.restore(); }
});

test('assignment detail uses the asset history endpoint and retains URL date/status filters', async () => {
  const dom = installDom('/assignments/1?status=Returned&from=2026-01-01T00%3A00%3A00Z&page=2');
  try {
    const { services, calls } = fixture(); const root = await assignmentsPage(services, 1); dom.body.append(root);
    const history = calls.find(([kind]) => kind === 'history'); assert.equal(history[1], 10);
    assert.equal(history[2].status, 'Returned'); assert.equal(history[2].page, 2);
    assert.equal(history[2].from, '2026-01-01T00:00:00Z'); assert.match(root.textContent, /Chi tiết cấp phát/);
  } finally { dom.restore(); }
});

for (const status of [403, 409, 503, 0]) test(`assignment write ${status} blocks blind retry and keeps current session`, async () => {
  const dom = installDom('/assignments');
  try {
    const { services } = fixture(); let writes = 0;
    services.assignments.returnAsset = async () => { writes++; throw Object.assign(new Error('Fixture failure'), { status }); };
    const root = await assignmentsPage(services); dom.body.append(root); findButton(root, 'Thu hồi').fire('click');
    await until(() => Boolean(findButton(dom.body, 'Xác nhận thu hồi'))); input('assignmentNote').value = 'Thu hồi';
    const confirm = findButton(dom.body, 'Xác nhận thu hồi'); confirm.fire('click');
    await until(() => Boolean(findButton(dom.body, 'Đóng và tải lại'))); confirm.fire('click');
    assert.equal(writes, 1); assert.equal(confirm.disabled, true); assert.equal(services.auth.session.id, 7);
  } finally { dom.restore(); }
});

test('detached assignment dialog does not refresh or announce a late successful mutation', async () => {
  const dom = installDom('/assignments');
  try {
    const { services } = fixture(); let release, refreshed = 0;
    window.dispatchEvent = () => { refreshed++; };
    services.assignments.returnAsset = () => new Promise(resolve => { release = resolve; });
    const root = await assignmentsPage(services); dom.body.append(root); findButton(root, 'Thu hồi').fire('click');
    await until(() => Boolean(findButton(dom.body, 'Xác nhận thu hồi'))); input('assignmentNote').value = 'Thu hồi';
    findButton(dom.body, 'Xác nhận thu hồi').fire('click'); await until(() => Boolean(release));
    const modal = dom.body.querySelector('dialog.dialog'); findButton(modal, 'Hủy').fire('click'); release({});
    await new Promise(resolve => setTimeout(resolve, 10)); assert.equal(refreshed, 0);
  } finally { dom.restore(); }
});
