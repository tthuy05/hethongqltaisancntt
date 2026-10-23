import test from 'node:test';
import assert from 'node:assert/strict';
import { assetExcelActions, assetImportDialog, excelDownloadButton } from '../src/ItAssetManagement.Api/wwwroot/js/components/asset-excel.js';
import { reportExcelActions } from '../src/ItAssetManagement.Api/wwwroot/js/components/report-excel.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

const previewResult = { totalRows: 2, validRows: 2, errorCount: 0, dryRun: true, committed: false, errors: [] };
const controls = dom => ({
  file: descendants(dom.body).find(n => n.tagName === 'INPUT'),
  preview: descendants(dom.body).find(n => n.tagName === 'BUTTON' && n.textContent === 'Kiểm tra / Xem trước'),
  confirm: descendants(dom.body).find(n => n.tagName === 'BUTTON' && n.textContent === 'Xác nhận nhập')
});
function source(handler = async (_file, { dryRun }) => ({ ...previewResult, dryRun, committed: !dryRun })) {
  const calls = [];
  return { mode: 'api', importExportEnabled: true, auth: { session: { id: 7, permissions: ['assets.read', 'import.assets.execute', 'export.assets.execute'] } }, calls,
    importExport: { importAssets: async (...args) => { calls.push(args); return handler(...args); }, template: async () => ({ blob: new Blob(['fixture']), filename: 'template.xlsx' }), exportAssets: async () => ({}) } };
}
function choose(dom, name = 'fixture.xlsx') {
  const { file } = controls(dom); const selected = new File(['synthetic'], name); file.files = [selected]; file.fire('change'); return selected;
}

test('Excel controls are opt-in, API-only and separately permission guarded', () => {
  const dom = installDom('/assets'); try {
    const services = source();
    assert.equal(assetExcelActions(services, { sortBy: 'name' }, () => true).length, 2);
    services.importExportEnabled = false; assert.equal(assetImportDialog(services), null); assert.deepEqual(assetExcelActions(services, {}, () => true), []);
    services.importExportEnabled = true; services.mode = 'mock'; assert.equal(assetImportDialog(services), null);
    services.mode = 'api'; services.auth.session.permissions = ['assets.read']; assert.equal(assetImportDialog(services), null);
    assert.deepEqual(assetExcelActions(services, {}, () => true), []);
    services.auth.session.permissions.push('import.assets.execute'); services.auth.session.id = 0; assert.equal(assetImportDialog(services), null);
  } finally { dom.restore(); }
});

test('import requires preview, captures the exact file, single-flights and refreshes after commit only', async () => {
  const dom = installDom('/assets'); try {
    let release, done = 0;
    const services = source(async (_file, { dryRun }) => dryRun ? previewResult : new Promise(resolve => { release = resolve; }));
    assetImportDialog(services, () => done++); const selected = choose(dom); const c = controls(dom);
    c.confirm.fire('click'); assert.equal(services.calls.length, 0);
    c.preview.fire('click'); await until(() => !c.confirm.disabled);
    assert.match(dom.body.textContent, /2 hợp lệ, 0 lỗi/); assert.equal(done, 0);
    c.confirm.fire('click'); c.confirm.fire('click'); assert.equal(services.calls.length, 2); assert.equal(c.file.disabled, true);
    assert.equal(services.calls[1][0], selected); assert.equal(services.calls[1][1].dryRun, false);
    release({ ...previewResult, dryRun: false, committed: true }); await until(() => done === 1);
    assert.equal(descendants(dom.body).filter(n => n.tagName === 'DIALOG').length, 0);
  } finally { dom.restore(); }
});

test('new file invalidates preview and row/field errors render as inert text', async () => {
  const dom = installDom('/assets'); try {
    let invalid = false;
    const services = source(async () => invalid ? { ...previewResult, validRows: 1, errorCount: 1,
      errors: [{ rowNumber: 3, field: 'name', errorCode: 'REQUIRED', errorMessage: '<img src=x onerror=alert(1)>' }] } : previewResult);
    assetImportDialog(services); choose(dom); const c = controls(dom); c.preview.fire('click'); await until(() => !c.confirm.disabled);
    choose(dom, 'other.xlsx'); assert.equal(c.confirm.disabled, true); invalid = true;
    c.preview.fire('click'); await until(() => !c.preview.disabled);
    assert.match(dom.body.textContent, /REQUIRED/); assert.match(dom.body.textContent, /<img src=x/);
    assert.equal(descendants(dom.body).filter(n => n.tagName === 'IMG').length, 0); assert.equal(c.confirm.disabled, true);
  } finally { dom.restore(); }
});

test('unknown commit outcome blocks all write retry and offers read refresh', async () => {
  const dom = installDom('/assets'); try {
    const services = source(async (_file, { dryRun }) => { if (dryRun) return previewResult; throw Error('lost response'); });
    assetImportDialog(services); choose(dom); const c = controls(dom); c.preview.fire('click'); await until(() => !c.confirm.disabled);
    c.confirm.fire('click'); await until(() => dom.body.textContent.includes('Chưa xác nhận được kết quả'));
    c.preview.fire('click'); c.confirm.fire('click'); assert.equal(services.calls.length, 2);
    assert.equal(c.preview.disabled, true); assert.equal(c.file.disabled, true);
  } finally { dom.restore(); }
});

test('commit validation failure never looks successful or retains an import-ready preview', async () => {
  const dom = installDom('/assets'); try {
    let done = 0;
    const services = source(async (_file, { dryRun }) => dryRun ? previewResult : { ...previewResult, dryRun: false, committed: false, validRows: 1,
      errorCount: 1, errors: [{ rowNumber: 2, field: 'serialNumber', errorCode: 'DUPLICATE_DB', errorMessage: 'Serial đã tồn tại' }] });
    assetImportDialog(services, () => done++); choose(dom); const c = controls(dom); c.preview.fire('click'); await until(() => !c.confirm.disabled);
    c.confirm.fire('click'); await until(() => !c.preview.disabled);
    assert.equal(done, 0); assert.equal(c.confirm.disabled, true); assert.match(dom.body.textContent, /Không có tài sản nào được nhập/);
  } finally { dom.restore(); }
});

test('stale page/session/permission cannot confirm and late preview cannot enable writes', async () => {
  for (const change of [s => { s.auth.session.id++; }, s => { s.auth.session.permissions = []; }, () => { window.location.hash = '#/users'; }]) {
    const dom = installDom('/assets'); try {
      let release; const services = source(() => new Promise(resolve => { release = resolve; }));
      assetImportDialog(services); choose(dom); const c = controls(dom); c.preview.fire('click'); change(services); release(previewResult);
      await until(() => !c.preview.disabled); c.confirm.fire('click'); assert.equal(services.calls.length, 1); assert.equal(c.confirm.disabled, true);
    } finally { dom.restore(); }
  }
});

test('downloads are single-flight and financial permission revocation discards response', async () => {
  const dom = installDom('/assets'); try {
    let release, saved = 0, requests = 0; const services = source(); services.auth.session.permissions.push('assets.cost.read');
    services.importExport.exportAssets = () => { requests++; return new Promise(resolve => { release = resolve; }); };
    const control = excelDownloadButton(services, { permission: 'export.assets.execute', method: 'exportAssets', save: () => saved++ }); dom.body.append(control);
    control.fire('click'); control.fire('click'); assert.equal(requests, 1);
    services.auth.session.permissions = services.auth.session.permissions.filter(p => p !== 'assets.cost.read'); release({});
    await until(() => !control.getAttribute('aria-busy')); assert.equal(saved, 0); assert.equal(control.disabled, true);
  } finally { dom.restore(); }
});

test('asset export preserves supported filters without paging and disables unsupported sort', async () => {
  const dom = installDom('/assets'); try {
    const services = source(); let query;
    services.importExport.exportAssets = async q => { query = q; throw Error('synthetic stop before save'); };
    const nodes = assetExcelActions(services, { page: 4, pageSize: 10, keyword: 'laptop', departmentId: '2', status: 'InStock', sortBy: 'name', sortDirection: 'desc' }, () => true);
    dom.body.append(...nodes); nodes[1].fire('click'); await until(() => !nodes[1].getAttribute('aria-busy'));
    assert.equal(query.keyword, 'laptop'); assert.equal(query.departmentId, '2'); assert.equal(query.status, 'InStock'); assert.equal(query.page, undefined);
    const unsupported = assetExcelActions(services, { sortBy: 'updatedAt' }, () => true); assert.equal(unsupported[1].disabled, true);
  } finally { dom.restore(); }
});

test('report exports use only real endpoints and map warranty dates to UTC', async () => {
  const dom = installDom('/reports'); try {
    const services = source(); let query;
    services.importExport.exportWarranty = async q => { query = q; throw Error('synthetic stop'); };
    const root = reportExcelActions(services, 'expiry', { warrantyFrom: '2026-10-01', warrantyTo: '2026-10-31', departmentId: '2' }, () => true);
    dom.body.append(root); descendants(root).find(n => n.tagName === 'BUTTON').fire('click'); await until(() => query);
    assert.equal(query.fromUtc, '2026-10-01T00:00:00.000Z'); assert.equal(query.toUtc, '2026-10-31T00:00:00.000Z');
    assert.match(root.textContent, /API xuất thời hạn License chưa có/);
    assert.equal(reportExcelActions(services, 'cost', {}, () => true), null);
    const unavailable = reportExcelActions(services, 'inventory', { purchasedFrom: '2026-10-01' }, () => true);
    assert.equal(descendants(unavailable).filter(n => n.tagName === 'BUTTON').length, 0);
  } finally { dom.restore(); }
});
