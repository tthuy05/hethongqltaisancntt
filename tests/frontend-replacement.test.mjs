import test from 'node:test';
import assert from 'node:assert/strict';
import { createReplacementService, validateRule } from '../src/ItAssetManagement.Api/wwwroot/js/services/replacement-service.js';
import { replacementPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/replacement.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

const version = Buffer.alloc(16, 4).toString('base64');
const row = () => ({ id: 1, code: 'RR-test', name: '<script>Rule</script>', version: 1, severity: 'HIGH', minimumAgeMonths: 60,
  minimumMaintenanceCount: null, maximumMaintenanceCostRatio: null, minimumFailureCount: null, requireWarrantyExpired: false,
  priority: 100, rowVersion: version, isActive: true, estimatedUnitCost: 1000 });
const page = items => ({ items, page: 1, pageSize: 20, totalItems: items.length, totalPages: items.length ? 1 : 0 });
const button = (root, name) => descendants(root).find(n => n.tagName === 'BUTTON' && n.textContent === name);
const dialog = root => descendants(root).find(n => n.tagName === 'DIALOG');
function services(grants, options = {}) {
  return { replacementEnabled: true, auth: { session: { id: 1, roles: ['ADMIN_IT'], permissions: grants } },
    replacement: { rules: async () => page([row()]), rule: async () => row(), ...options } };
}
test('replacement client keeps versioned request shape and blocks client codes', async () => {
  const writes = [];
  const api = createReplacementService({ request: async (path, options) => { writes.push({ path, options }); return row(); }, idPath: id => String(id) });
  const dto = { name: 'Age', severity: 'HIGH', minimumAgeMonths: 60, requireWarrantyExpired: false, priority: 100, rowVersion: version };
  await api.reviseRule(1, dto);
  assert.equal(writes[0].options.method, 'PUT'); assert.equal(writes[0].options.body.rowVersion, version);
  assert.equal(Object.hasOwn(writes[0].options.body, 'estimatedUnitCost'), false);
  await assert.rejects(api.createRule({ ...dto, code: 'chosen' }), e => e.status === 400);
  assert.throws(() => api.evaluate([1, 1]), e => e.status === 400);
  assert.throws(() => api.status(1, true, 'bad'), e => e.status === 400);
  assert.equal(writes.length, 1);
});
test('rule validation requires configured severity and warranty/failure pair', () => {
  const dto = { name: 'Age', minimumAgeMonths: 60, requireWarrantyExpired: false, priority: 100 };
  assert.ok(validateRule(dto).severity);
  assert.ok(validateRule({ ...dto, severity: 'HIGH', requireWarrantyExpired: true }).minimumFailureCount);
  assert.deepEqual(validateRule({ ...dto, severity: 'HIGH' }), {});
});
test('closed replacement page makes no API request', async () => {
  let calls = 0; const svc = services(['replacement-rules.read'], { rules: async () => { calls++; return page([]); } });
  svc.replacementEnabled = false;
  await assert.rejects(replacementPage(svc, true), e => e.status === 404); assert.equal(calls, 0);
});
test('manager reads rules without administrator actions', async () => {
  const dom = installDom('/replacement/rules');
  try {
    const svc = services(['replacement-rules.read']); svc.auth.session.roles = ['SYSTEM_MANAGER'];
    const root = await replacementPage(svc, true); dom.body.append(root);
    assert.equal(button(root, 'Tạo quy tắc'), undefined); assert.equal(button(root, 'Tạo phiên bản mới'), undefined);
    assert.ok(button(root, 'Chi tiết')); assert.ok(root.textContent.includes('<script>Rule</script>'));
  } finally { dom.restore(); }
});
test('rule revision preserves hidden cost by omission and rejects duplicate submit', async () => {
  const dom = installDom('/replacement/rules'); let writes = 0, payload, release;
  try {
    const svc = services(['replacement-rules.read', 'replacement-rules.update'], {
      reviseRule: async (_id, dto) => { writes++; payload = dto; await new Promise(resolve => { release = resolve; }); return { ...row(), id: 2, version: 2 }; }
    });
    const root = await replacementPage(svc, true); dom.body.append(root);
    button(root, 'Tạo phiên bản mới').fire('click'); await until(() => Boolean(dialog(dom.body)));
    assert.equal(document.getElementById('field-replacement-estimatedUnitCost'), undefined);
    document.getElementById('field-replacement-name').value = 'New rule revision';
    for (const node of descendants(dom.body)) if (node.getAttribute('id')?.startsWith('field-replacement-') && typeof node.value !== 'string') node.value = String(node.value);
    const form = dialog(dom.body).querySelector('form'); form.fire('submit'); form.fire('submit');
    await until(() => writes === 1); assert.equal(Object.hasOwn(payload, 'estimatedUnitCost'), false);
    assert.equal(payload.rowVersion, version); release(); await until(() => !dialog(dom.body));
  } finally { dom.restore(); }
});
test('session change during initial response does not mount stale rule data', async () => {
  const dom = installDom('/replacement/rules'); let release;
  try {
    const svc = services(['replacement-rules.read'], { rules: () => new Promise(resolve => { release = resolve; }) });
    const pending = replacementPage(svc, true); svc.auth.session = null; release(page([row()]));
    await assert.rejects(pending, e => e.status === 401);
  } finally { dom.restore(); }
});
test('unknown write result blocks retries until reload', async () => {
  const dom = installDom('/replacement/rules'); let writes = 0;
  try {
    const svc = services(['replacement-rules.read', 'replacement-rules.update'], { reviseRule: async () => { writes++; throw Object.assign(new Error('Unknown commit status'), { status: 500 }); } });
    const root = await replacementPage(svc, true); dom.body.append(root);
    button(root, 'Tạo phiên bản mới').fire('click'); await until(() => Boolean(dialog(dom.body)));
    for (const node of descendants(dom.body)) if (node.getAttribute('id')?.startsWith('field-replacement-') && typeof node.value !== 'string') node.value = String(node.value);
    const form = dialog(dom.body).querySelector('form'); form.fire('submit');
    await until(() => Boolean(button(dom.body, 'Đóng và tải lại'))); form.fire('submit');
    assert.equal(writes, 1); assert.equal(button(dom.body, 'Xác nhận').disabled, true);
    button(dom.body, 'Hủy').fire('click');
  } finally { dom.restore(); }
});
test('evaluation picker submits only explicitly selected assets and shows summary', async () => {
  const dom = installDom('/replacement'); const writes = [];
  try {
    const svc = services(['replacement-recommendations.read', 'replacement-recommendations.evaluate', 'assets.read', 'assets.cost.read'], {
      list: async () => page([]), evaluate: async ids => { writes.push(ids); return { evaluated: 1, created: 1, results: [{ assetId: 2, outcome: 'MATCHED' }] }; }
    });
    svc.assets = { list: async () => ({ items: [{ id: 1, assetCode: 'AST-1', name: 'First' }, { id: 2, assetCode: 'AST-2', name: 'Chosen' }] }) };
    const root = await replacementPage(svc); dom.body.append(root);
    button(root, 'Đánh giá tài sản').fire('click');
    await until(() => descendants(dom.body).filter(n => n.tagName === 'INPUT' && n.getAttribute('type') === 'checkbox').length === 2);
    const boxes = descendants(dom.body).filter(n => n.tagName === 'INPUT' && n.getAttribute('type') === 'checkbox');
    assert.ok(boxes.every(n => !n.checked)); boxes[1].checked = true; boxes[1].fire('change');
    dialog(dom.body).querySelector('form').fire('submit');
    await until(() => writes.length === 1 && !dialog(dom.body)); assert.deepEqual(writes[0], [2]);
    assert.ok(dom.body.textContent.includes('tạo 1 đề xuất mới'));
  } finally { dom.restore(); }
});
