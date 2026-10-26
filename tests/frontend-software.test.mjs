import test from 'node:test';
import assert from 'node:assert/strict';
import { createSoftwareService } from '../src/ItAssetManagement.Api/wwwroot/js/services/software-service.js';
import { softwarePage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/software.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

const rowVersion = Buffer.alloc(16, 7).toString('base64');
const item = { id: 4, code: 'PM000004', name: '<script>Office</script>', publisher: 'Acme', version: '1.0', description: 'Catalog', isActive: true, rowVersion };
const page = { items: [item], page: 1, pageSize: 20, totalItems: 1, totalPages: 1 };
const button = (root, text) => descendants(root).find(n => n.tagName === 'BUTTON' && n.textContent === text);
const form = root => descendants(root).find(n => n.tagName === 'DIALOG')?.querySelector('form');
function fixture() {
  const calls = [];
  const services = { softwareEnabled: true, auth: { session: { id: 7, roles: ['SYSTEM_MANAGER'], permissions: ['software.read', 'software.create', 'software.update', 'software.archive'] } },
    software: { list: async q => { calls.push(['list', q]); return page; }, get: async id => { calls.push(['get', id]); return item; },
      create: async dto => { calls.push(['create', dto]); return { ...item, ...dto }; }, update: async (id, dto) => { calls.push(['update', id, dto]); return item; }, archive: async (id, version) => { calls.push(['archive', id, version]); } } };
  return { services, calls };
}
test('software adapter preserves routes filters DTO and If-Match', async () => {
  const calls = []; const service = createSoftwareService({ request: async (...args) => { calls.push(args); }, idPath: String });
  await service.list({ vendor: 'A&B', status: 'Active', page: 2 }); await service.create({ name: 'Office' });
  await service.update(4, { name: 'Office 2', rowVersion }); await service.archive(4, rowVersion);
  assert.equal(calls[0][0], '/software?vendor=A%26B&status=Active&page=2');
  assert.equal(calls[1][1].method, 'POST'); assert.equal(calls[2][1].method, 'PUT');
  assert.equal(calls[3][1].headers['If-Match'], '"' + rowVersion + '"');
});
test('software adapter refuses client codes unknown fields malformed versions and overlong values', async () => {
  let writes = 0; const service = createSoftwareService({ request: async () => { writes++; }, idPath: String });
  for (const work of [() => service.create({ name: 'Office', code: 'PM999999' }), () => service.create({ name: ' ' }),
    () => service.create({ name: 'Office', publisher: 'x'.repeat(251) }), () => service.create({ name: 'Office', rowVersion }),
    () => service.update(4, { name: 'Office' }), () => service.archive(4, 'bad'), () => service.list({ purchaseCost: 1 })])
    await assert.rejects(work, { status: 400 });
  assert.equal(writes, 0);
});
test('software page checks flag and permission before transport', async () => {
  const dom = installDom('/software'); try {
    const { services, calls } = fixture(); services.softwareEnabled = false; await assert.rejects(softwarePage(services), { status: 404 });
    services.softwareEnabled = true; services.auth.session.permissions = []; await assert.rejects(softwarePage(services), { status: 403 }); assert.equal(calls.length, 0);
  } finally { dom.restore(); }
});
test('software reader sees escaped content and no mutation controls', async () => {
  const dom = installDom('/software'); try {
    const { services } = fixture(); services.auth.session.permissions = ['software.read'];
    const root = await softwarePage(services); dom.body.append(root);
    assert.ok(root.textContent.includes(item.name)); assert.equal(descendants(root).some(n => n.tagName === 'SCRIPT'), false);
    for (const label of ['Thêm phần mềm', 'Sửa', 'Ngừng sử dụng']) assert.equal(button(root, label), undefined);
    button(root, 'Chi tiết').fire('click'); await until(() => dom.body.textContent.includes('Chi tiết phần mềm'));
  } finally { dom.restore(); }
});
test('software create validates required name sends no code submits once and refreshes', async () => {
  const dom = installDom('/software'); try {
    const { services, calls } = fixture(); let refresh = 0; window.dispatchEvent = e => { if (e.type === 'frontend-refresh') refresh++; };
    const root = await softwarePage(services); dom.body.append(root); button(root, 'Thêm phần mềm').fire('click');
    form(dom.body).fire('submit'); assert.equal(calls.filter(c => c[0] === 'create').length, 0);
    document.getElementById('field-software-name').value = 'Office'; form(dom.body).fire('submit'); form(dom.body).fire('submit');
    await until(() => refresh === 1); const writes = calls.filter(c => c[0] === 'create'); assert.equal(writes.length, 1); assert.equal(Object.hasOwn(writes[0][1], 'code'), false);
  } finally { dom.restore(); }
});
test('software update refreshes record version and prevents blind retry after conflict', async () => {
  const dom = installDom('/software'); try {
    const { services, calls } = fixture(); let writes = 0;
    services.software.update = async (_id, dto) => { writes++; assert.equal(dto.rowVersion, rowVersion); throw Object.assign(new Error('Stale'), { status: 409 }); };
    const root = await softwarePage(services); dom.body.append(root); button(root, 'Sửa').fire('click'); await until(() => Boolean(form(dom.body)));
    form(dom.body).fire('submit'); await until(() => Boolean(button(dom.body, 'Đóng và tải lại'))); form(dom.body).fire('submit');
    assert.equal(writes, 1); assert.ok(calls.some(c => c[0] === 'get'));
  } finally { dom.restore(); }
});
test('software archive reviews fresh record and respects active license conflict', async () => {
  const dom = installDom('/software'); try {
    const { services } = fixture(); let writes = 0;
    services.software.archive = async (id, version) => { writes++; assert.equal(id, 4); assert.equal(version, rowVersion); throw Object.assign(new Error('Active'), { status: 409, code: 'SOFTWARE_ACTIVE_LICENSES' }); };
    const root = await softwarePage(services); dom.body.append(root); button(root, 'Ngừng sử dụng').fire('click'); await until(() => Boolean(form(dom.body)));
    form(dom.body).fire('submit'); await until(() => dom.body.textContent.includes('còn bản quyền hoạt động')); form(dom.body).fire('submit'); assert.equal(writes, 1);
  } finally { dom.restore(); }
});
test('software dialog cancels stale session and ignores late page response', async () => {
  const dom = installDom('/software'); try {
    const { services, calls } = fixture(); const root = await softwarePage(services); dom.body.append(root); button(root, 'Thêm phần mềm').fire('click');
    document.getElementById('field-software-name').value = 'Office'; services.auth.session = null; form(dom.body).fire('submit');
    assert.equal(calls.filter(c => c[0] === 'create').length, 0);
    services.auth.session = { id: 7, permissions: ['software.read'], roles: [] };
    services.software.list = async () => { services.auth.session = null; return page; }; await assert.rejects(softwarePage(services), { status: 401 });
  } finally { dom.restore(); }
});
