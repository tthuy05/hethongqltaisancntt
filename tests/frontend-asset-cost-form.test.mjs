import test from 'node:test';
import assert from 'node:assert/strict';
import { assetFormPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/asset-form.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

function source(permissions = ['assets.read', 'assets.update']) {
  const writes = [];
  const catalog = async () => ({ items: [{ id: 1, name: 'Fixture reference', isActive: true }], page: 1, pageSize: 100, totalItems: 1, totalPages: 1 });
  return { mode: 'api', writes, auth: { session: { id: 7, permissions } }, assetTypes: { list: catalog }, departments: { list: catalog }, assets: {
    get: async () => ({ id: 5, name: 'Synthetic asset', assetCode: 'AST-FIXTURE', assetTypeId: 1, owningDepartmentId: 1,
      assetType: { id: 1, name: 'Fixture reference' }, owningDepartment: { id: 1, name: 'Fixture reference' },
      status: 'InStock', purchasePrice: 1200.25, rowVersion: 'AQEBAQEBAQEBAQEBAQEBAQ==' }),
    update: async (id, dto) => { writes.push({ id, dto }); return { id }; },
    create: async dto => { writes.push({ dto }); return { id: 5 }; }
  } };
}
const field = (root, name) => descendants(root).find(n => n.getAttribute('name') === name);
const submit = root => descendants(root).find(n => n.tagName === 'BUTTON' && n.getAttribute('type') === 'submit');
test('nonfinancial editor never mounts or submits price and can still edit metadata', async () => {
  const dom = installDom('/assets/5/edit'); try {
    const services = source(); const root = await assetFormPage(services, 5); dom.body.append(root);
    assert.ok(!field(root, 'purchasePrice')); assert.doesNotMatch(root.textContent, /1200\.25/);
    assert.match(root.textContent, /Giá mua hiện tại được giữ nguyên/);
    field(root, 'name').value = 'Only metadata changed'; descendants(root).find(n => n.tagName === 'FORM').fire('submit');
    await until(() => !submit(root).disabled);
    assert.equal(services.writes.length, 1); assert.equal(services.writes[0].dto.name, 'Only metadata changed');
    assert.equal(Object.hasOwn(services.writes[0].dto, 'purchasePrice'), false);
  } finally { dom.restore(); }
});
test('nonfinancial create omits price rather than inventing zero or requesting a financial grant', async () => {
  const dom = installDom('/assets/new'); try {
    const services = source(['assets.read', 'assets.create']); const root = await assetFormPage(services); dom.body.append(root);
    assert.ok(!field(root, 'purchasePrice')); assert.match(root.textContent, /Giá mua để chưa biết/);
    field(root, 'name').value = 'Metadata-only new asset'; field(root, 'assetTypeId').value = '1'; field(root, 'owningDepartmentId').value = '1';
    descendants(root).find(n => n.tagName === 'FORM').fire('submit'); await until(() => !submit(root).disabled);
    assert.equal(services.writes.length, 1); assert.equal(Object.hasOwn(services.writes[0].dto, 'purchasePrice'), false);
  } finally { dom.restore(); }
});
test('financial editor retains price validation and can explicitly clear unknown cost', async () => {
  const dom = installDom('/assets/5/edit'); try {
    const services = source(['assets.read', 'assets.update', 'assets.cost.read']); const root = await assetFormPage(services, 5); dom.body.append(root);
    assert.equal(field(root, 'purchasePrice').value, 1200.25);
    field(root, 'purchasePrice').value = '1200.251'; descendants(root).find(n => n.tagName === 'FORM').fire('submit');
    assert.equal(services.writes.length, 0); assert.match(root.textContent, /tối đa 2 chữ số/);
    field(root, 'purchasePrice').value = ''; descendants(root).find(n => n.tagName === 'FORM').fire('submit');
    await until(() => !submit(root).disabled); assert.equal(services.writes.length, 1); assert.equal(services.writes[0].dto.purchasePrice, null);
  } finally { dom.restore(); }
});
