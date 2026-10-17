import test from 'node:test';
import assert from 'node:assert/strict';
import { licenseTargetPicker } from '../src/ItAssetManagement.Api/wwwroot/js/components/license-target-picker.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

const grants = ['licenses.read', 'license-assignments.assign', 'users.lookup', 'assets.read'];
const services = () => ({ mode: 'api', auth: { session: { id: 7, permissions: grants } },
  users: { lookup: async () => ({ items: [{ id: 9, displayName: 'Synthetic user', email: 'not-retained@example.test' }] }) },
  assets: { list: async () => ({ items: [{ id: 4, assetCode: 'AST-FIXTURE', name: 'Synthetic asset', status: 'InStock' }] }) } });
function controls(root) {
  const nodes = descendants(root); return { type: nodes.find(n => n.tagName === 'SELECT' && n.getAttribute('id')?.endsWith('-type')),
    selection: nodes.find(n => n.tagName === 'SELECT' && n.getAttribute('id')?.endsWith('-selection')),
    feedback: nodes.find(n => n.getAttribute('role') === 'status'), search: nodes.find(n => n.tagName === 'BUTTON') };
}
test('default closed/no contract/no grants never searches or posts', () => {
  const dom = installDom(); try {
    assert.match(licenseTargetPicker(services()).element.textContent, /PLANNED/);
    assert.match(licenseTargetPicker(services(), { enabled: true, licenseType: 'VOLUME' }).element.textContent, /chốt loại target/);
    const source = services(); source.auth.session.permissions = [];
    assert.throws(() => licenseTargetPicker(source, { enabled: true, licenseType: 'PER_USER' }), error => error.status === 403);
  } finally { dom.restore(); }
});
test('PER_USER uses existing active user lookup and emits only user XOR asset IDs', async () => {
  const dom = installDom(); try {
    const source = services(); const calls = []; const selected = [];
    source.users.lookup = async query => { calls.push(query); return { items: [{ id: 9, displayName: 'Synthetic user', password: 'not-retained' }] }; };
    source.assets.list = async () => { throw new Error('wrong target'); };
    const picker = licenseTargetPicker(source, { enabled: true, licenseType: 'PER_USER', onChange: value => selected.push(value) });
    dom.body.append(picker.element); const ui = controls(picker.element); ui.search.fire('click');
    await until(() => !ui.search.disabled && !ui.selection.disabled);
    ui.selection.value = '9'; ui.selection.fire('change');
    assert.deepEqual(calls, [{ page: 1, pageSize: 20 }]); assert.deepEqual(selected.at(-1), { userId: 9, assetId: null });
    assert.doesNotMatch(picker.element.textContent, /not-retained/); assert.equal(Object.isFrozen(selected.at(-1)), true);
    assert.deepEqual(descendants(ui.type).filter(n => n.tagName === 'OPTION').map(n => n.value), ['user']); picker.dispose();
  } finally { dom.restore(); }
});
test('PER_DEVICE filters retired/archived assets and never allows user target', async () => {
  const dom = installDom(); try {
    const source = services(); const selected = [];
    source.assets.list = async () => ({ items: [{ id: 4, assetCode: 'A4', name: 'Ready', status: 'InStock' },
      { id: 5, assetCode: 'A5', name: 'Retired', status: 'Retired' }, { id: 6, assetCode: 'A6', name: 'Archived', status: 'InStock', isArchived: true }] });
    const picker = licenseTargetPicker(source, { enabled: true, licenseType: 'PER_DEVICE', onChange: value => selected.push(value) });
    dom.body.append(picker.element); const ui = controls(picker.element); ui.search.fire('click');
    await until(() => !ui.search.disabled && !ui.selection.disabled);
    assert.match(ui.selection.textContent, /A4/); assert.doesNotMatch(ui.selection.textContent, /A5|A6/);
    ui.selection.value = '4'; ui.selection.fire('change'); assert.deepEqual(selected.at(-1), { userId: null, assetId: 4 });
    ui.selection.value = '5'; ui.selection.fire('change'); assert.equal(selected.at(-1), null); picker.dispose();
  } finally { dom.restore(); }
});
test('changing target type rejects stale lookup response and clears previous IDs', async () => {
  const dom = installDom(); try {
    const source = services(); let release;
    source.users.lookup = () => new Promise(resolve => { release = resolve; });
    const selected = []; const picker = licenseTargetPicker(source, { enabled: true, licenseType: 'VOLUME', allowedTargets: ['user', 'asset'], onChange: value => selected.push(value) });
    dom.body.append(picker.element); const ui = controls(picker.element); ui.search.fire('click');
    ui.type.value = 'asset'; ui.type.fire('change'); release({ items: [{ id: 9, displayName: 'Old user response' }] });
    await until(() => !ui.search.disabled); assert.equal(ui.selection.disabled, true); assert.doesNotMatch(ui.selection.textContent, /Old user/);
    assert.equal(selected.at(-1), null); picker.dispose();
  } finally { dom.restore(); }
});
test('disposed component and changed session cannot consume delayed results', async () => {
  for (const scenario of ['dispose', 'session']) {
    const dom = installDom(); try {
      const source = services(); let release; source.users.lookup = () => new Promise(resolve => { release = resolve; });
      const picker = licenseTargetPicker(source, { enabled: true, licenseType: 'PER_USER' }); dom.body.append(picker.element);
      const ui = controls(picker.element); ui.search.fire('click');
      if (scenario === 'dispose') picker.dispose(); else source.auth.session = { id: 8, permissions: grants };
      release({ items: [{ id: 9, displayName: 'Delayed secret-free result' }] }); await until(() => ui.search.textContent === 'Tìm target');
      assert.equal(ui.selection.disabled, true); assert.doesNotMatch(ui.selection.textContent, /Delayed/);
    } finally { dom.restore(); }
  }
});
test('invalid/error lookup and missing lookup grant produce explicit feedback without fake results', async () => {
  const dom = installDom(); try {
    for (const lookup of [async () => { throw Object.assign(new Error('private backend details'), { status: 403 }); },
      async () => ({ items: [{ id: '9', displayName: 'Malformed' }] }), async () => ({ items: [{ id: 9, displayName: 'One' }, { id: 9, displayName: 'Two' }] })]) {
      const source = services(); source.users.lookup = lookup;
      const picker = licenseTargetPicker(source, { enabled: true, licenseType: 'PER_USER' }); dom.body.append(picker.element);
      const ui = controls(picker.element); ui.search.fire('click'); await until(() => !ui.search.disabled);
      assert.equal(ui.selection.disabled, true); assert.match(ui.feedback.textContent, /Không tải được target/);
      assert.doesNotMatch(picker.element.textContent, /private backend details|Malformed/); picker.dispose();
    }
    const source = services(); source.auth.session.permissions = grants.filter(g => g !== 'users.lookup');
    const picker = licenseTargetPicker(source, { enabled: true, licenseType: 'PER_USER' }); dom.body.append(picker.element);
    const ui = controls(picker.element); ui.search.fire('click'); assert.match(ui.feedback.textContent, /Không đủ quyền/); picker.dispose();
  } finally { dom.restore(); }
});
test('grant revocation during lookup cannot populate choices or emit an allocation payload', async () => {
  const dom = installDom(); try {
    const source = services(); let release; const selected = [];
    source.users.lookup = () => new Promise(resolve => { release = resolve; });
    const picker = licenseTargetPicker(source, { enabled: true, licenseType: 'PER_USER', onChange: value => selected.push(value) });
    dom.body.append(picker.element); const ui = controls(picker.element); ui.search.fire('click');
    source.auth.session.permissions = grants.filter(g => g !== 'license-assignments.assign');
    release({ items: [{ id: 9, displayName: 'No longer accessible' }] }); await until(() => !ui.search.disabled);
    assert.equal(ui.selection.disabled, true); assert.match(ui.feedback.textContent, /Quyền đã thay đổi/);
    assert.ok(selected.every(value => value === null)); picker.dispose();
  } finally { dom.restore(); }
});
test('lookup has no nested form, Enter is search-only and disposed picker cannot send another request', async () => {
  const dom = installDom(); try {
    const source = services(); let calls = 0;
    source.users.lookup = async () => { calls++; return { items: [] }; };
    const picker = licenseTargetPicker(source, { enabled: true, licenseType: 'PER_USER' }); dom.body.append(picker.element);
    const ui = controls(picker.element); const input = descendants(picker.element).find(n => n.tagName === 'INPUT');
    assert.equal(picker.element.tagName, 'DIV'); assert.equal(descendants(picker.element).filter(n => n.tagName === 'FORM').length, 0);
    assert.equal(ui.search.getAttribute('type'), 'button');
    let prevented = false, stopped = false;
    for (const handler of input.handlers.get('keydown')) handler({ key: 'Enter', preventDefault() { prevented = true; }, stopPropagation() { stopped = true; } });
    await until(() => !ui.search.disabled); assert.equal(calls, 1); assert.equal(prevented, true); assert.equal(stopped, true);
    picker.dispose(); ui.search.fire('click'); assert.equal(calls, 1); assert.equal(ui.search.disabled, true);
  } finally { dom.restore(); }
});
