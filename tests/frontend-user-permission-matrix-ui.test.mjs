import test from 'node:test';
import assert from 'node:assert/strict';
import { userPage } from '../src/ItAssetManagement.Api/wwwroot/js/pages/users.js';
import { installDom, descendants, until } from './support/test-dom.mjs';

const version = 'AAAAAAAAAAAAAAAAAAAAAA==';
const testUser = {
  id: 21,
  displayName: 'Nguyễn Văn Test',
  username: 'test.user',
  email: 'test@example.com',
  employeeCode: 'NV000021',
  departmentId: 1,
  departmentName: 'Phòng IT',
  isActive: true,
  isEmailConfirmed: true,
  rowVersion: version
};

const userAccountState = {
  id: 21,
  isActive: true,
  isAdminLocked: false,
  roleIds: [2],
  rowVersion: version,
  hasCustomPermissions: true,
  customPermissions: ['users.read'],
  effectivePermissions: ['users.read']
};

const roleAdmin = {
  id: 1,
  name: 'Admin IT',
  code: 'ADMIN_IT',
  isActive: true,
  permissions: [
    { code: 'users.read', module: 'users', name: 'Xem người dùng' },
    { code: 'users.create', module: 'users', name: 'Tạo người dùng' },
    { code: 'assets.read', module: 'assets', name: 'Xem tài sản' }
  ]
};

const roleManager = {
  id: 2,
  name: 'System Manager',
  code: 'MANAGER',
  isActive: true,
  permissions: [
    { code: 'users.read', module: 'users', name: 'Xem người dùng' },
    { code: 'assets.read', module: 'assets', name: 'Xem tài sản' }
  ]
};

const page = items => ({ items, totalPages: 1, page: 1, pageSize: 100, totalItems: items.length });
const grants = ['users.read', 'users.create', 'users.update', 'users.status.manage', 'roles.assign', 'roles.read', 'departments.read'];

function createMockServices() {
  const writes = [];
  return {
    mode: 'api',
    writes,
    auth: { session: { id: 7, permissions: [...grants] } },
    departments: { list: async () => page([{ id: 1, name: 'Phòng IT' }]) },
    roles: {
      list: async () => page([roleAdmin, roleManager]),
      get: async id => id === 1 ? roleAdmin : roleManager
    },
    users: {
      list: async () => page([testUser]),
      get: async () => testUser,
      account: async () => ({ ...userAccountState }),
      create: async dto => { writes.push({ type: 'create', dto }); return { ...testUser, employeeCode: 'NV000099' }; },
      update: async (id, dto) => { writes.push({ type: 'update', id, dto }); return testUser; },
      replaceRoles: async (id, dto) => { writes.push({ type: 'replaceRoles', id, dto }); return { user: testUser, warnings: [] }; }
    }
  };
}

const findButton = (root, name) => descendants(root).find(n => n.tagName === 'BUTTON' && n.textContent?.trim() === name);
const findDialogs = dom => descendants(dom.body).filter(n => n.tagName === 'DIALOG');

test('add user dialog uses wide layout with full-width permission matrix below account and role sections', async () => {
  const dom = installDom('/users');
  try {
    const services = createMockServices();
    const root = await userPage(services);
    dom.body.append(root);

    const addBtn = findButton(root, 'Thêm người dùng');
    assert.ok(addBtn, 'Button "Thêm người dùng" should exist');
    addBtn.fire('click');

    await until(() => findDialogs(dom).length === 1);
    const dialog = findDialogs(dom)[0];

    // Check dialog has wide class
    assert.ok(dialog.className.includes('dialog--wide'), 'Dialog should have dialog--wide class');

    // Check layout sections
    const profileSection = descendants(dialog).find(n => n.className === 'user-profile-section');
    assert.ok(profileSection, 'Profile section should exist');

    const roleSection = descendants(dialog).find(n => n.className === 'user-role-section');
    assert.ok(roleSection, 'Role section should exist');

    const roleBox = descendants(roleSection).find(n => n.className === 'role-choices-box');
    assert.ok(roleBox, 'Role choices box should exist');

    const radioCards = descendants(roleBox).filter(n => n.className === 'role-radio-card');
    assert.equal(radioCards.length, 2, 'Should display radio cards for each role');

    // Check permission matrix container
    const matrixPanel = descendants(dialog).find(n => n.className === 'user-permissions-panel');
    assert.ok(matrixPanel, 'Permission matrix panel should be mounted full width');

    // Matrix should be inside roleSection, after roleBox
    assert.ok(descendants(roleSection).includes(matrixPanel), 'Permission matrix should be inside role section');

    // Check module groups and live counters
    const moduleGroups = descendants(matrixPanel).filter(n => n.className === 'permission-module-group');
    assert.ok(moduleGroups.length >= 2, 'Should display module groups (users, assets)');

    const countBadge = descendants(matrixPanel).find(n => n.className.includes('permission-counter'));
    assert.ok(countBadge, 'Total counter badge should be present');
    assert.match(countBadge.textContent, /Đã chọn: \d+\/\d+ quyền/);

    // Verify reset button and preview button exist
    const resetBtn = descendants(matrixPanel).find(n => n.textContent === 'Khôi phục mặc định theo vai trò');
    assert.ok(resetBtn, 'Reset button should exist');

    const previewBtn = descendants(matrixPanel).find(n => n.textContent === 'Xem trước quyền hiệu lực');
    assert.ok(previewBtn, 'Preview button should exist');

    dialog.remove();
    root.remove();
  } finally {
    dom.restore();
  }
});

test('roles dialog uses wide layout with user summary bar, role cards and full-width matrix', async () => {
  const dom = installDom('/users');
  try {
    const services = createMockServices();
    const root = await userPage(services);
    dom.body.append(root);

    const rolesBtn = findButton(root, 'Vai trò');
    assert.ok(rolesBtn, 'Button "Vai trò" should exist');
    rolesBtn.fire('click');

    await until(() => findDialogs(dom).length === 1);
    const dialog = findDialogs(dom)[0];

    assert.ok(dialog.className.includes('dialog--wide'), 'Roles dialog should have dialog--wide class');

    // Summary bar
    const summaryBar = descendants(dialog).find(n => n.className === 'user-role-summary-bar');
    assert.ok(summaryBar, 'User summary bar should exist');
    assert.match(summaryBar.textContent, /Nguyễn Văn Test/);
    assert.match(summaryBar.textContent, /NV000021/);
    assert.match(summaryBar.textContent, /Phòng IT/);

    // Role radio cards
    const roleCards = descendants(dialog).filter(n => n.className === 'role-radio-card');
    assert.equal(roleCards.length, 2, 'Should have radio cards for roles');

    // Matrix
    const matrixPanel = descendants(dialog).find(n => n.className === 'user-permissions-panel');
    assert.ok(matrixPanel, 'Full-width matrix panel should exist');

    // Custom badge check (state has customPermissions)
    const customBadge = descendants(matrixPanel).find(n => n.className.includes('custom-badge'));
    assert.ok(customBadge, 'Custom permission badge should be active');

    // Test reset to role defaults button
    const resetBtn = descendants(matrixPanel).find(n => n.textContent === 'Khôi phục mặc định theo vai trò');
    assert.ok(resetBtn, 'Reset button should be active when custom permissions exist');
    resetBtn.fire('click');

    const defaultBadge = descendants(matrixPanel).find(n => n.className.includes('default-badge'));
    assert.ok(defaultBadge, 'After reset, default badge should appear');

    // Save and check payload
    const saveBtn = findButton(dialog, 'Lưu vai trò');
    saveBtn.fire('click');

    await until(() => services.writes.length === 1);
    assert.equal(services.writes[0].type, 'replaceRoles');
    assert.equal(services.writes[0].id, 21);
    assert.equal(services.writes[0].dto.resetToRoleDefaults, true);

    dialog.remove();
    root.remove();
  } finally {
    dom.restore();
  }
});
