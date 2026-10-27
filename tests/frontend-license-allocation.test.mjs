import test from 'node:test';
import assert from 'node:assert/strict';
import { createLicenseAssignmentService, validateAssign, validVersion } from '../src/ItAssetManagement.Api/wwwroot/js/services/license-assignment-service.js';
import { licenseDetailPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/licenses.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

const rowVersion = Buffer.alloc(16, 1).toString('base64');
const sampleLicense = () => ({
  id: 4,
  licenseCode: 'BQ000004',
  software: { id: 2, name: 'Microsoft Office 365' },
  licenseType: 'PER_USER',
  totalQuantity: 5,
  usedQuantity: 1,
  availableQuantity: 4,
  isActive: true,
  maskedKey: '****-9999',
  notes: 'Office licenses',
  startsAt: '2026-01-01T00:00:00Z',
  expiresAt: '2027-01-01T00:00:00Z',
  rowVersion
});

const sampleAssignment = (id = 10, isArchived = false) => ({
  id,
  softwareLicenseId: 4,
  licenseCode: 'BQ000004',
  softwareName: 'Microsoft Office 365',
  assignedUserId: 20,
  assignedUserFullName: 'Nguyen Van A',
  assignedUserEmail: 'nguyenvana@example.com',
  assignedAssetId: null,
  assignedAssetCode: null,
  assignedAssetName: null,
  assignedAtUtc: '2026-02-01T10:00:00Z',
  revokedAtUtc: isArchived ? '2026-03-01T10:00:00Z' : null,
  assignedByUserId: 7,
  assignedByUserName: 'Admin User',
  revokedByUserId: isArchived ? 7 : null,
  revokedByUserName: isArchived ? 'Admin User' : null,
  notes: isArchived ? 'Employee left company' : 'Standard issue',
  isArchived,
  createdAtUtc: '2026-02-01T10:00:00Z',
  rowVersion
});

function createFixture(permissions = ['licenses.read', 'license-assignments.read', 'license-assignments.assign', 'license-assignments.revoke', 'license-assignments.transfer', 'users.lookup', 'assets.read']) {
  const calls = [];
  if (globalThis.window) window.dispatchEvent = event => { calls.push(['refresh', event.type]); };
  const currentLicense = sampleLicense();
  const assignments = [sampleAssignment(10, false)];
  const services = {
    mode: 'api',
    licenseEnabled: true, licenseAllocationEnabled: true,
    auth: {
      session: {
        id: 7,
        roles: ['ADMIN_IT'],
        permissions
      }
    },
    licenses: {
      get: async id => { calls.push(['licenses.get', id]); return currentLicense; }
    },
    licenseAssignments: {
      get: async id => { calls.push(['get', id]); return structuredClone(assignments.find(a => a.id === id)); },
      listByLicense: async (lid, q) => {
        calls.push(['listByLicense', lid, q]);
        const items = q?.status === 'Active' ? assignments.filter(a => !a.isArchived) :
                      q?.status === 'Revoked' ? assignments.filter(a => a.isArchived) : assignments;
        return { items, page: q?.page ?? 1, pageSize: q?.pageSize ?? 20, totalItems: items.length, totalPages: 1 };
      },
      assign: async dto => {
        calls.push(['assign', dto]);
        const newA = { ...sampleAssignment(11, false), assignedUserId: dto.userId, assignedAssetId: dto.assetId, notes: dto.notes };
        assignments.push(newA);
        currentLicense.usedQuantity++;
        currentLicense.availableQuantity--;
        return newA;
      },
      revoke: async (id, dto) => {
        calls.push(['revoke', id, dto]);
        const target = assignments.find(a => a.id === id);
        if (target) {
          target.isArchived = true;
          target.revokedAtUtc = new Date().toISOString();
          target.notes = dto.reason;
        }
        currentLicense.usedQuantity--;
        currentLicense.availableQuantity++;
        return target;
      },
      transfer: async (id, dto) => {
        calls.push(['transfer', id, dto]);
        const target = assignments.find(a => a.id === id);
        if (target) {
          target.isArchived = true;
          target.revokedAtUtc = new Date().toISOString();
        }
        const newA = { ...sampleAssignment(12, false), assignedUserId: dto.userId, assignedAssetId: dto.assetId, notes: dto.reason };
        assignments.push(newA);
        return { previous: target, current: newA };
      }
    },
    users: {
      lookup: async q => {
        calls.push(['users.lookup', q]);
        return { items: [{ id: 25, displayName: 'Tran Thi B' }] };
      }
    },
    assets: {
      list: async q => {
        calls.push(['assets.list', q]);
        return { items: [{ id: 30, assetCode: 'AST-000030', name: 'Laptop Dell XPS', status: 'InStock' }] };
      }
    }
  };
  return { services, calls, currentLicense, assignments };
}

test('license assignment client validates requests and preserves query and If-Match headers', async () => {
  const calls = [];
  const client = createLicenseAssignmentService({
    request: async (url, opts) => {
      calls.push({ url, opts });
      return { ok: true };
    },
    idPath: id => String(id)
  });

  assert.equal(validVersion(rowVersion), true);
  assert.equal(validVersion('invalid-base64'), false);

  const errorsMissing = validateAssign({});
  assert.ok(errorsMissing.softwareLicenseId);
  assert.ok(errorsMissing.target);

  const errorsBoth = validateAssign({ softwareLicenseId: 4, userId: 1, assetId: 2 });
  assert.ok(errorsBoth.target);

  await client.list({ page: 2, pageSize: 15, licenseId: 4, status: 'Active' });
  assert.equal(calls[0].url, '/license-assignments?page=2&pageSize=15&licenseId=4&status=Active');

  await client.listByLicense(4, { page: 1, pageSize: 20, status: 'Active' });
  assert.equal(calls[1].url, '/software-licenses/4/assignments?page=1&pageSize=20&status=Active');

  await client.assign({ softwareLicenseId: 4, userId: 20, notes: 'Seat assignment' });
  assert.equal(calls[2].url, '/license-assignments');
  assert.equal(calls[2].opts.method, 'POST');

  await client.revoke(10, { reason: 'Retirement', rowVersion });
  assert.equal(calls[3].url, '/license-assignments/10/revoke');
  assert.equal(calls[3].opts.headers['If-Match'], `"${rowVersion}"`);

  await client.transfer(10, { userId: 25, reason: 'Transferred', rowVersion });
  assert.equal(calls[4].url, '/license-assignments/10/transfer');
  assert.equal(calls[4].opts.headers['If-Match'], `"${rowVersion}"`);
});

test('license detail renders allocation section and displays active seats', async () => {
  const dom = installDom();
  try {
    const { services } = createFixture();
    const page = await licenseDetailPage(services, 4, { enabled: true });
    dom.body.append(page);

    await until(() => descendants(page).some(n => n.textContent?.includes('Danh sách phân bổ')));

    assert.match(page.textContent, /Danh sách phân bổ/);
    assert.match(page.textContent, /Nguyen Van A/);
    assert.match(page.textContent, /nguyenvana@example.com/);
    assert.match(page.textContent, /Đang hoạt động/);

    const nodes = descendants(page);
    const assignBtn = nodes.find(n => n.tagName === 'BUTTON' && n.textContent?.includes('Phân bổ seat'));
    assert.ok(assignBtn);
    assert.equal(assignBtn.disabled, false);

    page.dispose();
  } finally {
    dom.restore();
  }
});

test('license detail handles assign workflow with target picker', async () => {
  const dom = installDom();
  try {
    const { services, calls } = createFixture();
    const page = await licenseDetailPage(services, 4, { enabled: true });
    dom.body.append(page);

    await until(() => descendants(page).some(n => n.textContent?.includes('Danh sách phân bổ')));

    const assignBtn = descendants(page).find(n => n.tagName === 'BUTTON' && n.textContent?.includes('Phân bổ seat'));
    assignBtn.fire('click');

    await until(() => descendants(dom.body).some(n => n.tagName === 'DIALOG'));
    const dialog = descendants(dom.body).find(n => n.tagName === 'DIALOG');
    assert.ok(dialog);

    const searchBtn = descendants(dialog).find(n => n.tagName === 'BUTTON' && n.textContent === 'Tìm target');
    assert.ok(searchBtn);
    searchBtn.fire('click');

    const select = descendants(dialog).find(n => n.tagName === 'SELECT' && n.getAttribute('id')?.endsWith('-selection'));
    await until(() => !select.disabled);

    select.value = '25';
    select.fire('change');

    const form = dialog.querySelector('form');
    form.fire('submit');

    await until(() => calls.some(c => c[0] === 'assign'));
    await until(() => !dialog.isConnected);
    await until(() => calls.some(c => c[0] === 'refresh' && c[1] === 'frontend-refresh'));
    const assignCall = calls.find(c => c[0] === 'assign');
    assert.equal(assignCall[1].softwareLicenseId, 4);
    assert.equal(assignCall[1].userId, 25);

    page.dispose();
  } finally {
    dom.restore();
  }
});

test('license detail handles revoke workflow with confirmation', async () => {
  const dom = installDom();
  try {
    const { services, calls } = createFixture();
    const page = await licenseDetailPage(services, 4, { enabled: true });
    dom.body.append(page);

    await until(() => descendants(page).some(n => n.textContent?.includes('Nguyen Van A')));

    const revokeBtn = descendants(page).find(n => n.tagName === 'BUTTON' && n.textContent === 'Thu hồi');
    assert.ok(revokeBtn);
    revokeBtn.fire('click');

    await until(() => descendants(dom.body).some(n => n.tagName === 'DIALOG'));
    const dialog = descendants(dom.body).find(n => n.tagName === 'DIALOG');
    assert.ok(dialog);
    assert.match(dialog.textContent, /Nguyen Van A/i);

    const form = dialog.querySelector('form');
    form.fire('submit');

    await until(() => calls.some(c => c[0] === 'revoke'));
    await until(() => !dialog.isConnected);
    await until(() => calls.some(c => c[0] === 'refresh' && c[1] === 'frontend-refresh'));
    const revokeCall = calls.find(c => c[0] === 'revoke');
    assert.equal(revokeCall[1], 10);
    assert.equal(revokeCall[2].rowVersion, rowVersion);

    page.dispose();
  } finally {
    dom.restore();
  }
});

test('license detail handles transfer workflow with target picker', async () => {
  const dom = installDom();
  try {
    const { services, calls } = createFixture();
    const page = await licenseDetailPage(services, 4, { enabled: true });
    dom.body.append(page);

    await until(() => descendants(page).some(n => n.textContent?.includes('Nguyen Van A')));

    const transferBtn = descendants(page).find(n => n.tagName === 'BUTTON' && n.textContent === 'Điều chuyển');
    assert.ok(transferBtn);
    transferBtn.fire('click');

    await until(() => descendants(dom.body).some(n => n.tagName === 'DIALOG'));
    const dialog = descendants(dom.body).find(n => n.tagName === 'DIALOG');
    assert.ok(dialog);
    assert.match(dialog.textContent, /Nguyen Van A/);

    const searchBtn = descendants(dialog).find(n => n.tagName === 'BUTTON' && n.textContent === 'Tìm target');
    assert.ok(searchBtn);
    searchBtn.fire('click');

    const select = descendants(dialog).find(n => n.tagName === 'SELECT' && n.getAttribute('id')?.endsWith('-selection'));
    await until(() => !select.disabled);

    select.value = '25';
    select.fire('change');

    const form = dialog.querySelector('form');
    form.fire('submit');

    await until(() => calls.some(c => c[0] === 'transfer'));
    await until(() => !dialog.isConnected);
    await until(() => calls.some(c => c[0] === 'refresh' && c[1] === 'frontend-refresh'));
    const transferCall = calls.find(c => c[0] === 'transfer');
    assert.equal(transferCall[1], 10);
    assert.equal(transferCall[2].userId, 25);
    assert.equal(transferCall[2].rowVersion, rowVersion);

    page.dispose();
  } finally {
    dom.restore();
  }
});

test('license detail disables allocate button when license is exhausted or inactive', async () => {
  const dom = installDom();
  try {
    const { services, currentLicense } = createFixture();
    currentLicense.availableQuantity = 0;
    currentLicense.usedQuantity = 5;

    const page = await licenseDetailPage(services, 4, { enabled: true });
    dom.body.append(page);

    await until(() => descendants(page).some(n => n.textContent?.includes('Danh sách phân bổ')));
    const assignBtn = descendants(page).find(n => n.tagName === 'BUTTON' && n.textContent?.includes('Phân bổ seat'));
    assert.ok(assignBtn);
    assert.equal(assignBtn.disabled, true);

    page.dispose();
  } finally {
    dom.restore();
  }
});

test('allocation feature stays closed despite grants and never fetches allocation data', async () => {
  const dom = installDom(); try {
    const { services, calls } = createFixture(); services.licenseAllocationEnabled = false;
    const page = await licenseDetailPage(services, 4, { enabled: true }); dom.body.append(page);
    assert.equal(calls.some(c => c[0] === 'listByLicense'), false); assert.doesNotMatch(page.textContent, /Danh sách phân bổ/); page.dispose();
  } finally { dom.restore(); }
});
test('revoked rows are read only even when isArchived remains false', async () => {
  const dom = installDom(); try {
    const { services, assignments } = createFixture(); assignments[0].revokedAtUtc = '2026-03-01T10:00:00Z'; assignments[0].isArchived = false;
    const page = await licenseDetailPage(services, 4, { enabled: true }); dom.body.append(page);
    assert.match(page.textContent, /Đã thu hồi/);
    for (const label of ['Thu hồi','Điều chuyển']) assert.equal(descendants(page).some(n => n.tagName === 'BUTTON' && n.textContent === label), false);
    page.dispose();
  } finally { dom.restore(); }
});
test('network failure blocks replay until refresh and changed session blocks writes', async () => {
  const dom = installDom(); try {
    const { services, calls } = createFixture(); let writes = 0;
    services.licenseAssignments.revoke = async () => { writes++; throw Object.assign(new Error('Unknown result'), {status:0}); };
    const page = await licenseDetailPage(services, 4, {enabled:true}); dom.body.append(page);
    descendants(page).find(n => n.tagName === 'BUTTON' && n.textContent === 'Thu hồi').fire('click');
    await until(() => descendants(dom.body).some(n => n.tagName === 'DIALOG'));
    const dialog = descendants(dom.body).find(n => n.tagName === 'DIALOG'); const form = dialog.querySelector('form');
    form.fire('submit'); await until(() => dialog.textContent.includes('Đóng và tải lại')); form.fire('submit'); assert.equal(writes,1);
    services.auth.session = null; form.fire('submit'); assert.equal(writes,1); assert.equal(calls.some(c => c[0] === 'refresh'),false); page.dispose();
  } finally { dom.restore(); }
});
test('assignment adapter rejects invalid IDs, unknown generated fields and invalid versions before transport', async () => {
  let writes=0; const service=createLicenseAssignmentService({request:async()=>{writes++;},idPath:String});
  for (const operation of [()=>service.assign({softwareLicenseId:4,userId:20,assetId:-1}),()=>service.assign({softwareLicenseId:4,userId:20,licenseCode:'BQ999999'}),
    ()=>service.assign({softwareLicenseId:4,userId:20,notes:3}),()=>service.assign({softwareLicenseId:4,userId:20,assignedAt:'not-a-date'}),
    ()=>service.revoke(4,{rowVersion:'bad'}),()=>service.revoke(4,{rowVersion,reason:'x'.repeat(1001)}),
    ()=>service.transfer(4,{rowVersion,userId:20,assetId:0}),()=>service.listByLicense(4,{page:2147483647,pageSize:100}),()=>service.listByLicense(4,{secret:true})]) await assert.rejects(operation,{status:400});
  assert.equal(writes,0);
});
