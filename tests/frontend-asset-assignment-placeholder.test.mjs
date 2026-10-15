import test from 'node:test';
import assert from 'node:assert/strict';
import { assetListPage, assetDetailPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/assets.js';
import { installDom } from './support/test-dom.mjs';

function services(mode = 'api', assignment = null) {
  const asset = { id: 5, assetCode: 'AST-FIXTURE', name: 'Synthetic asset', status: 'InStock',
    assetType: { id: 1, name: 'Fixture type' }, owningDepartment: { id: 1, name: 'Fixture department' }, currentAssignment: assignment };
  const catalog = { list: async () => ({ items: [], page: 1, pageSize: 100, totalItems: 0, totalPages: 0 }) };
  return { mode, auth: { session: { id: 7, permissions: ['assets.read'] } }, departments: catalog, assetTypes: catalog,
    assignments: { list: () => { throw Error('Assignment rollout must stay closed'); }, get: () => { throw Error('Assignment rollout must stay closed'); } },
    assets: { get: async () => asset, list: async () => ({ items: [asset], page: 1, pageSize: 10, totalItems: 1, totalPages: 1 }) } };
}
test('API detail missing current-assignment projection is unknown, not a claim that asset is unassigned', async () => {
  const dom = installDom('/assets/5'); try {
    const root = await assetDetailPage(services(), 5); assert.match(root.textContent, /Chưa có dữ liệu cấp phát/); assert.doesNotMatch(root.textContent, /Chưa phân bổ/);
    assert.match(root.textContent, /Assignment History.*PLANNED/);
  } finally { dom.restore(); }
});
test('API list also distinguishes unavailable assignment data and detail-request failure from unassigned state', async () => {
  const dom = installDom('/assets'); try {
    for (const failed of [false, true]) {
      const source = services(); if (failed) source.assets.get = async () => { throw Error('Fixture unavailable'); };
      const root = await assetListPage(source); assert.match(root.textContent, /Chưa có dữ liệu cấp phát/); assert.doesNotMatch(root.textContent, /Chưa phân bổ/);
    }
  } finally { dom.restore(); }
});
test('an existing explicit display name is rendered without making any Assignment API request', async () => {
  const dom = installDom('/assets'); try {
    const source = services('api', { user: { id: 9, displayName: 'Người nhận synthetic', email: 'not-for-display' } });
    for (const root of [await assetListPage(source), await assetDetailPage(source, 5)]) {
      assert.match(root.textContent, /Người nhận synthetic/); assert.doesNotMatch(root.textContent, /Chưa có dữ liệu cấp phát|not-for-display/);
    }
  } finally { dom.restore(); }
});
test('explicit demo mode preserves its known synthetic unassigned state', async () => {
  const dom = installDom('/assets'); try {
    for (const root of [await assetListPage(services('mock')), await assetDetailPage(services('mock'), 5)]) {
      assert.match(root.textContent, /Chưa phân bổ/); assert.doesNotMatch(root.textContent, /Chưa có dữ liệu cấp phát/);
    }
  } finally { dom.restore(); }
});
