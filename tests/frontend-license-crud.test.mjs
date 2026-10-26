import test from 'node:test';
import assert from 'node:assert/strict';
import { createLicenseService } from '../src/ItAssetManagement.Api/wwwroot/js/services/license-service.js';
import { licensePage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/licenses.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

const rowVersion = Buffer.alloc(16, 8).toString('base64');
const sampleLicense = () => ({
  id: 4,
  licenseCode: 'BQ000004',
  software: { id: 2, name: '<script>SQL Server</script>' },
  licenseType: 'VOLUME',
  totalQuantity: 10,
  usedQuantity: 0,
  availableQuantity: 10,
  isActive: true,
  maskedKey: '****-WXYZ',
  notes: 'Production DB license',
  startsAt: '2026-01-01T00:00:00Z',
  expiresAt: '2027-01-01T00:00:00Z',
  purchaseCost: 5000000,
  rowVersion
});
const licensePageResult = (p = 1, ps = 20) => ({
  items: [sampleLicense()],
  page: p,
  pageSize: ps,
  totalItems: p === 2 ? 11 : 1,
  totalPages: p === 2 ? 2 : 1
});
const softwareOptionsResult = () => [
  { id: 2, name: 'SQL Server', publisher: 'Microsoft', version: '2022' }
];

const findBtn = (root, text) => descendants(root).find(n => n.tagName === 'BUTTON' && n.textContent?.trim() === text);
const findDialog = root => descendants(root).find(n => n.tagName === 'DIALOG');
const findForm = root => findDialog(root)?.querySelector('form');

function createFixture(permissions = ['licenses.read', 'licenses.create', 'licenses.update', 'licenses.archive', 'licenses.key.manage', 'licenses.key.reveal', 'assets.cost.read'], roles = ['ADMIN_IT']) {
  const calls = [];
  const currentItem = sampleLicense();
  const services = {
    mode: 'api',
    licenseEnabled: true,
    auth: {
      session: {
        id: 7,
        roles,
        permissions
      }
    },
    licenses: {
      list: async q => { calls.push(['list', q]); return licensePageResult(q?.page ?? 1, q?.pageSize ?? 20); },
      get: async id => { calls.push(['get', id]); return currentItem; },
      create: async dto => { calls.push(['create', dto]); return { ...currentItem, ...dto, id: 5, licenseCode: 'BQ000005' }; },
      update: async (id, dto) => { calls.push(['update', id, dto]); return { ...currentItem, ...dto }; },
      archive: async (id, ver) => { calls.push(['archive', id, ver]); },
      revealKey: async (id, reason) => { calls.push(['revealKey', id, reason]); return { licenseKey: 'REAL-KEY-1234-WXYZ', expiresAt: new Date(Date.now() + 60000).toISOString() }; },
      softwareOptions: async () => { calls.push(['softwareOptions']); return softwareOptionsResult(); }
    }
  };
  return { services, calls, currentItem };
}

test('license adapter preserves routes, query params, headers, and If-Match', async () => {
  const calls = [];
  const service = createLicenseService({
    request: async (...args) => {
      calls.push(args);
      if (args[0].startsWith('/software-licenses?')) return licensePageResult(2, 10);
      return sampleLicense();
    },
    idPath: id => String(id)
  });

  await service.list({ softwareId: 2, status: 'ACTIVE', vendor: 'Microsoft', page: 2, pageSize: 10, keyword: 'SQL' });
  await service.create({ softwareId: 2, licenseType: 'VOLUME', totalQuantity: 10, notes: 'Test' });
  await service.update(4, { softwareId: 2, licenseType: 'VOLUME', totalQuantity: 15, rowVersion, notes: 'Updated' });
  await service.archive(4, rowVersion);
  await service.revealKey(4, 'Audit verification for IT audit');
  await service.softwareOptions();

  assert.equal(calls[0][0], '/software-licenses?page=2&pageSize=10&softwareId=2&status=ACTIVE&vendor=Microsoft&keyword=SQL');
  assert.equal(calls[1][0], '/software-licenses');
  assert.equal(calls[1][1].method, 'POST');
  assert.equal(calls[2][0], '/software-licenses/4');
  assert.equal(calls[2][1].method, 'PUT');
  assert.equal(calls[2][1].headers['If-Match'], '"' + rowVersion + '"');
  assert.equal(calls[3][0], '/software-licenses/4');
  assert.equal(calls[3][1].method, 'DELETE');
  assert.equal(calls[3][1].headers['If-Match'], '"' + rowVersion + '"');
  assert.equal(calls[4][0], '/software-licenses/4/reveal-key');
  assert.equal(calls[4][1].method, 'POST');
  assert.deepEqual(calls[4][1].body, { reason: 'Audit verification for IT audit' });
  assert.equal(calls[5][0], '/software-licenses/software-options');
});

test('license adapter validates inputs and rejects invalid contracts before network', async () => {
  let writes = 0;
  const service = createLicenseService({
    request: async () => { writes++; return sampleLicense(); },
    idPath: id => String(id)
  });

  // Cannot send client licenseCode
  await assert.rejects(() => service.create({ softwareId: 2, licenseType: 'VOLUME', totalQuantity: 5, licenseCode: 'BQ999999' }), { status: 400 });
  // Cannot send totalQuantity <= 0
  await assert.rejects(() => service.create({ softwareId: 2, licenseType: 'VOLUME', totalQuantity: 0 }), { status: 400 });
  // Cannot send invalid licenseType
  await assert.rejects(() => service.create({ softwareId: 2, licenseType: 'INVALID_TYPE', totalQuantity: 5 }), { status: 400 });
  // Cannot send invalid date
  await assert.rejects(() => service.create({ softwareId: 2, licenseType: 'VOLUME', totalQuantity: 5, startsAt: 'invalid-date' }), { status: 400 });
  // Cannot send startsAt >= expiresAt
  await assert.rejects(() => service.create({ softwareId: 2, licenseType: 'VOLUME', totalQuantity: 5, startsAt: '2026-10-15T00:00:00Z', expiresAt: '2026-10-10T00:00:00Z' }), { status: 400 });
  // Cannot send negative cost
  await assert.rejects(() => service.create({ softwareId: 2, licenseType: 'VOLUME', totalQuantity: 5, purchaseCost: -100 }), { status: 400 });
  // Cannot send unexpected extra field
  await assert.rejects(() => service.create({ softwareId: 2, licenseType: 'VOLUME', totalQuantity: 5, unexpected: 'bad' }), { status: 400 });
  // Cannot archive without valid rowVersion
  await assert.rejects(() => service.archive(4, 'invalid-version!'), { status: 400 });
  // Cannot revealKey with empty or blank reason
  await assert.rejects(() => service.revealKey(4, ''), { status: 400 });
  await assert.rejects(() => service.revealKey(4, '   '), { status: 400 });

  assert.equal(writes, 0);
});

test('licensePage checks rollout flag and permissions before network requests', async () => {
  const dom = installDom('/licenses');
  try {
    const { services, calls } = createFixture();
    services.licenseEnabled = false;
    await assert.rejects(licensePage(services), { status: 404 });

    services.licenseEnabled = true;
    services.auth.session.permissions = [];
    await assert.rejects(licensePage(services), { status: 403 });
    assert.equal(calls.length, 0);
  } finally {
    dom.restore();
  }
});

test('license reader sees masked key, escaped HTML, and no unauthorized action buttons', async () => {
  const dom = installDom('/licenses');
  try {
    const { services } = createFixture(['licenses.read'], ['TECHNICAL_SUPPORT']);
    const root = await licensePage(services);
    dom.body.append(root);

    assert.ok(root.textContent.includes('BQ000004'));
    assert.ok(root.textContent.includes('****-WXYZ'));
    // No raw unescaped script tag
    assert.equal(descendants(root).some(n => n.tagName === 'SCRIPT'), false);

    // Reader has no mutate controls
    assert.equal(findBtn(root, 'Thêm bản quyền'), undefined);
    assert.equal(findBtn(root, 'Sửa'), undefined);
    assert.equal(findBtn(root, 'Ngừng dùng'), undefined);

    // Detail button is available
    const detailBtn = findBtn(root, 'Chi tiết');
    assert.ok(detailBtn);
    detailBtn.fire('click');
    await until(() => Boolean(findDialog(dom.body)));
    assert.ok(dom.body.textContent.includes('Chi tiết bản quyền'));
    assert.ok(dom.body.textContent.includes('BQ000004'));
  } finally {
    dom.restore();
  }
});

test('license create modal validates software, quantity and submits once', async () => {
  const dom = installDom('/licenses');
  try {
    const { services, calls } = createFixture();
    let refreshed = 0;
    window.dispatchEvent = e => { if (e.type === 'frontend-refresh') refreshed++; };

    const root = await licensePage(services);
    dom.body.append(root);

    findBtn(root, 'Thêm bản quyền').fire('click');
    await until(() => Boolean(findDialog(dom.body)));

    const formEl = findForm(dom.body);

    // Initial submit with empty software should fail client validation
    formEl.fire('submit');
    assert.equal(calls.filter(c => c[0] === 'create').length, 0);

    // Fill valid data
    const softwareSelect = document.getElementById('field-license-software');
    assert.ok(softwareSelect);
    softwareSelect.value = '2';

    const qtyInput = document.getElementById('field-license-totalQuantity');
    assert.ok(qtyInput);
    qtyInput.value = '20';

    formEl.fire('submit');
    // Double submit attempt should be blocked by pending flag
    formEl.fire('submit');

    await until(() => refreshed === 1);
    const createCalls = calls.filter(c => c[0] === 'create');
    assert.equal(createCalls.length, 1);
    assert.equal(createCalls[0][1].softwareId, 2);
    assert.equal(createCalls[0][1].totalQuantity, 20);
    assert.equal(Object.hasOwn(createCalls[0][1], 'licenseCode'), false);
  } finally {
    dom.restore();
  }
});

test('license update modal refreshes version and surfaces 409 conflict safely', async () => {
  const dom = installDom('/licenses');
  try {
    const { services, calls } = createFixture();
    let updateAttempts = 0;
    services.licenses.update = async (_id, dto) => {
      updateAttempts++;
      assert.equal(dto.rowVersion, rowVersion);
      throw Object.assign(new Error('Dữ liệu đã bị thay đổi bởi người dùng khác.'), { status: 409, code: 'CONCURRENCY_CONFLICT' });
    };

    const root = await licensePage(services);
    dom.body.append(root);

    findBtn(root, 'Sửa').fire('click');
    await until(() => Boolean(findDialog(dom.body)));

    const formEl = findForm(dom.body);
    formEl.fire('submit');

    await until(() => Boolean(findBtn(dom.body, 'Đóng và tải lại')));
    // Submit again should be blocked while conflict dialog is open
    formEl.fire('submit');
    assert.equal(updateAttempts, 1);
    assert.ok(calls.some(c => c[0] === 'get'));
  } finally {
    dom.restore();
  }
});

test('license archive respects active allocations conflict', async () => {
  const dom = installDom('/licenses');
  try {
    const { services } = createFixture();
    let archiveAttempts = 0;
    services.licenses.archive = async (id, ver) => {
      archiveAttempts++;
      assert.equal(id, 4);
      assert.equal(ver, rowVersion);
      throw Object.assign(new Error('Không thể ngừng sử dụng bản quyền còn phân bổ hoạt động.'), { status: 409, code: 'LICENSE_HAS_ACTIVE_ALLOCATIONS' });
    };

    const root = await licensePage(services);
    dom.body.append(root);

    findBtn(root, 'Ngừng dùng').fire('click');
    await until(() => Boolean(findDialog(dom.body)));

    const formEl = findForm(dom.body);
    formEl.fire('submit');

    await until(() => dom.body.textContent.includes('còn phân bổ hoạt động'));
    assert.equal(archiveAttempts, 1);
  } finally {
    dom.restore();
  }
});

test('license reveal key validates reason length, displays revealed key and expires', async () => {
  const dom = installDom('/licenses/4');
  try {
    const { services, calls } = createFixture();
    const root = await licensePage(services, 4);
    dom.body.append(root);

    findBtn(root, 'Xem khóa').fire('click');
    await until(() => Boolean(findDialog(dom.body)));

    const dialog = findDialog(dom.body);
    const formEl = findForm(dom.body);
    const reasonInput = document.getElementById('field-reveal-reason');
    assert.ok(reasonInput);

    // Empty reason should fail
    reasonInput.value = '   ';
    formEl.fire('submit');
    assert.equal(calls.filter(c => c[0] === 'revealKey').length, 0);

    // Valid reason
    reasonInput.value = 'Kiem tra ban quyen cho phong ke toan';
    formEl.fire('submit');

    await until(() => Boolean(findDialog(dom.body)));
    assert.equal(calls.filter(c => c[0] === 'revealKey').length, 1);
  } finally {
    dom.restore();
  }
});
