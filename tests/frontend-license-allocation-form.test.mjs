import test from 'node:test';
import assert from 'node:assert/strict';
import { licenseAllocationForm } from '../src/ItAssetManagement.Api/wwwroot/js/components/license-allocation-form.js';
import { licenseAllocationDraft } from '../src/ItAssetManagement.Api/wwwroot/js/models/license-allocation-draft.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

const grants = ['licenses.read', 'license-assignments.assign', 'users.lookup'];
const license = () => ({ id: 4, licenseCode: 'LIC-FIXTURE', licenseType: 'PER_USER', totalQuantity: 2, isActive: true });
const services = () => ({ mode: 'api', auth: { session: { id: 7, permissions: grants } },
  users: { lookup: async () => ({ items: [{ id: 9, displayName: 'Synthetic recipient' }] }) } });
const control = root => { const nodes = descendants(root); return {
  search: nodes.find(n => n.tagName === 'BUTTON' && n.textContent === 'Tìm target'),
  selection: nodes.find(n => n.tagName === 'SELECT' && n.getAttribute('id')?.endsWith('-selection')),
  note: nodes.find(n => n.tagName === 'TEXTAREA'),
  prepare: nodes.find(n => n.tagName === 'BUTTON' && n.textContent === 'Chuẩn bị yêu cầu')
}; };
async function choose(root) {
  const ui = control(root); ui.search.fire('click'); await until(() => !ui.search.disabled && !ui.selection.disabled);
  ui.selection.value = '9'; ui.selection.fire('change'); return ui;
}
test('draft builder enforces one-seat XOR/positive IDs/strict fields and immutable normalized notes', () => {
  const draft = licenseAllocationDraft({ licenseId: 4, userId: 9, assetId: null, note: '  Fixture note  ' });
  assert.deepEqual(draft, { licenseId: 4, userId: 9, assetId: null, note: 'Fixture note' }); assert.ok(Object.isFrozen(draft));
  for (const invalid of [{ licenseId: 4 }, { licenseId: 4, userId: 9, assetId: 2 }, { licenseId: '4', userId: 9 },
    { licenseId: 4, userId: 0 }, { licenseId: 4, userId: 9, quantity: 2 }, { licenseId: 4, userId: 9, licenseKey: 'synthetic' },
    { licenseId: 4, userId: 9, note: 'a'.repeat(1001) }]) assert.throws(() => licenseAllocationDraft(invalid), error => error.status === 400);
});
test('closed/no callback/inactive license cannot prepare or submit a request', () => {
  const dom = installDom(); try {
    assert.match(licenseAllocationForm({}).element.textContent, /PLANNED/);
    const missing = licenseAllocationForm(services(), { enabled: true, license: license() });
    assert.equal(control(missing.element).prepare.disabled, true); missing.dispose();
    const inactive = licenseAllocationForm(services(), { enabled: true, license: { ...license(), isActive: false }, onPrepared: () => { throw new Error('must not run'); } });
    assert.equal(control(inactive.element).prepare.disabled, true); inactive.dispose();
    const denied = services(); denied.auth.session.permissions = [];
    assert.throws(() => licenseAllocationForm(denied, { enabled: true, license: license() }), error => error.status === 403);
  } finally { dom.restore(); }
});
test('one outer form prepares an immutable draft with selected target, never a quantity or network mutation', async () => {
  const dom = installDom(); try {
    const prepared = []; const form = licenseAllocationForm(services(), { enabled: true, license: license(), onPrepared: draft => prepared.push(draft) });
    dom.body.append(form.element); assert.equal(form.element.tagName, 'FORM');
    assert.equal(descendants(form.element).filter(n => n.tagName === 'FORM').length, 0);
    const ui = await choose(form.element); ui.note.value = '  Prepared note  '; form.element.fire('submit');
    await until(() => !ui.prepare.disabled); assert.deepEqual(prepared, [{ licenseId: 4, userId: 9, assetId: null, note: 'Prepared note' }]);
    assert.match(form.element.textContent, /chưa cấp seat hoặc ghi database/); assert.equal(ui.note.value, '  Prepared note  '); form.dispose();
  } finally { dom.restore(); }
});
test('missing target/overlong note block the local callback before preparation', async () => {
  const dom = installDom(); try {
    let calls = 0; const form = licenseAllocationForm(services(), { enabled: true, license: license(), onPrepared: () => { calls++; } });
    dom.body.append(form.element); form.element.fire('submit'); assert.match(form.element.textContent, /Tìm và chọn/); assert.equal(calls, 0);
    const ui = await choose(form.element); ui.note.value = 'x'.repeat(1001); form.element.fire('submit'); assert.equal(calls, 0);
    assert.match(form.element.textContent, /ghi chú vượt/); form.dispose();
  } finally { dom.restore(); }
});
test('double submit is single-flight and editing while pending requires preparing the new draft again', async () => {
  const dom = installDom(); try {
    let calls = 0, release; const form = licenseAllocationForm(services(), { enabled: true, license: license(), onPrepared: () => { calls++; return new Promise(resolve => { release = resolve; }); } });
    dom.body.append(form.element); const ui = await choose(form.element); form.element.fire('submit'); form.element.fire('submit'); assert.equal(calls, 1);
    ui.note.value = 'Edited while pending'; ui.note.fire('input'); release(); await until(() => !ui.prepare.disabled);
    assert.match(form.element.textContent, /Bản nhập đã thay đổi/); assert.equal(calls, 1); form.dispose();
  } finally { dom.restore(); }
});
test('callback failure preserves selection/note and does not retry or leak details', async () => {
  const dom = installDom(); try {
    let calls = 0; const form = licenseAllocationForm(services(), { enabled: true, license: license(), onPrepared: async () => { calls++; throw new Error('synthetic-private-detail'); } });
    dom.body.append(form.element); const ui = await choose(form.element); ui.note.value = 'Keep note'; form.element.fire('submit'); await until(() => !ui.prepare.disabled);
    assert.equal(calls, 1); assert.equal(ui.note.value, 'Keep note'); assert.equal(ui.selection.value, '9');
    assert.match(form.element.textContent, /Dữ liệu đang nhập vẫn được giữ/); assert.doesNotMatch(form.element.textContent, /synthetic-private-detail/); form.dispose();
  } finally { dom.restore(); }
});
test('revoked grants prevent preparation and dispose suppresses completion of a pending callback', async () => {
  const dom = installDom(); try {
    const source = services(); let calls = 0, release;
    const form = licenseAllocationForm(source, { enabled: true, license: license(), onPrepared: () => { calls++; return new Promise(resolve => { release = resolve; }); } });
    dom.body.append(form.element); await choose(form.element);
    source.auth.session.permissions = []; form.element.fire('submit'); assert.equal(calls, 0); assert.match(form.element.textContent, /Quyền đã thay đổi/);
    source.auth.session.permissions = grants; form.element.fire('submit'); assert.equal(calls, 1);
    form.dispose(); release(); await new Promise(resolve => setImmediate(resolve));
    assert.equal(control(form.element).prepare.disabled, true); assert.doesNotMatch(form.element.textContent, /đã được chuyển/);
  } finally { dom.restore(); }
});
