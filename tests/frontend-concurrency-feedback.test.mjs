import test from 'node:test';
import assert from 'node:assert/strict';
import { concurrencyFeedback } from '../src/ItAssetManagement.Api/wwwroot/js/components/concurrency-feedback.js';
import { assetFormPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/asset-form.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

test('conflict feedback never refreshes or retries before explicit discard click', async () => {
  const dom = installDom(); try {
    let calls = 0; const root = concurrencyFeedback({ onRefresh: async () => { calls++; } }); dom.body.append(root);
    assert.equal(calls, 0); assert.match(root.textContent, /Bản bạn đang nhập vẫn được giữ/);
    const control = descendants(root).find(n => n.tagName === 'BUTTON'); assert.equal(control.getAttribute('type'), 'button');
    control.fire('click'); await until(() => !control.disabled); assert.equal(calls, 1);
    assert.throws(() => concurrencyFeedback({}), TypeError);
  } finally { dom.restore(); }
});
test('double click sends one read refresh; detached feedback cannot start another', async () => {
  const dom = installDom(); try {
    let calls = 0, release; const root = concurrencyFeedback({ onRefresh: () => { calls++; return new Promise(resolve => { release = resolve; }); } });
    dom.body.append(root); const control = descendants(root).find(n => n.tagName === 'BUTTON');
    control.fire('click'); control.fire('click'); assert.equal(calls, 1); assert.equal(control.disabled, true);
    release(); await until(() => !control.disabled); root.remove(); control.fire('click'); assert.equal(calls, 1);
  } finally { dom.restore(); }
});
test('read failures show safe feedback without leaking backend error or automatically retrying', async () => {
  const dom = installDom(); try {
    for (const status of [401, 403, 503]) {
      let calls = 0; const root = concurrencyFeedback({ onRefresh: async () => { calls++; throw Object.assign(new Error('synthetic-private-detail'), { status }); } });
      dom.body.append(root); const control = descendants(root).find(n => n.tagName === 'BUTTON');
      control.fire('click'); await until(() => !control.disabled); assert.equal(calls, 1);
      assert.doesNotMatch(root.textContent, /synthetic-private-detail/); assert.match(root.textContent, /Phiên đăng nhập|Không đủ quyền|Không tải được/);
    }
  } finally { dom.restore(); }
});
test('real Asset edit component retains draft after 409 and offers explicit discard/read refresh only', async () => {
  const dom = installDom('/assets/4/edit'); try {
    let writes = 0, reloads = 0; window.dispatchEvent = () => { reloads++; };
    const catalog = async () => ({ items: [{ id: 1, name: 'Fixture reference', isActive: true }], page: 1, pageSize: 100, totalItems: 1, totalPages: 1 });
    const services = { mode: 'api', assetTypes: { list: catalog }, departments: { list: catalog }, assets: {
      get: async () => ({ id: 4, name: 'Original', assetCode: 'AST-FIXTURE', assetTypeId: 1, owningDepartmentId: 1,
        assetType: { id: 1, name: 'Fixture reference' }, owningDepartment: { id: 1, name: 'Fixture reference' }, status: 'InStock', rowVersion: 'AAAAAAAAAAAAAAAAAAAAAA==' }),
      update: async () => { writes++; throw Object.assign(new Error('Data changed'), { status: 409, code: 'CONCURRENCY_CONFLICT' }); }
    } };
    const root = await assetFormPage(services, 4); dom.body.append(root);
    const nodes = descendants(root); const name = nodes.find(n => n.getAttribute('name') === 'name'); name.value = 'Draft must remain';
    nodes.find(n => n.tagName === 'FORM').fire('submit'); await until(() => root.textContent.includes('Bỏ bản đang nhập và tải mới'));
    assert.equal(writes, 1); assert.equal(reloads, 0); assert.equal(name.value, 'Draft must remain');
    descendants(root).find(n => n.tagName === 'BUTTON' && n.textContent === 'Bỏ bản đang nhập và tải mới').fire('click');
    await until(() => reloads === 1); assert.equal(writes, 1);
  } finally { dom.restore(); }
});
