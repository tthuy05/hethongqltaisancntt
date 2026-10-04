import test from 'node:test';
import assert from 'node:assert/strict';
import { createApiServices } from '../src/ItAssetManagement.Api/wwwroot/js/services/api-services.js';
import { validateUser, validateUserStatus, validateUserRoles } from '../src/ItAssetManagement.Api/wwwroot/js/utils/validation.js';

const version = Buffer.alloc(16).toString('base64');
const profile = { username: 'staff.test', email: 'staff@example.test', displayName: 'Nhân viên thử', employeeCode: null, departmentId: null, phone: null };
const login = { email: 'admin@example.test', password: 'synthetic-test-password' };
async function client(handler = () => ({})) {
  const calls = [];
  const services = createApiServices({ fetchImpl: async (url, options) => {
    calls.push({ url, ...options });
    const result = url.endsWith('/auth/login')
      ? { accessToken: 'synthetic-test-token', tokenType: 'Bearer', expiresAt: new Date(Date.now() + 60000).toISOString(), user: { id: 9, roles: ['ADMIN_IT'], permissions: ['users.read'] } }
      : await handler(url, options);
    return result instanceof Response ? result : new Response(JSON.stringify(result), { headers: { 'Content-Type': 'application/json' } });
  } });
  await services.auth.login(login);
  return { services, calls };
}
test('user profile validation matches nullable IDs and blocks security mass assignment', () => {
  assert.deepEqual(validateUser({ ...profile, password: ' 0123456789  ' }), {});
  assert.deepEqual(validateUser({ ...profile, rowVersion: version }, { isUpdate: true }), {});
  for (const extra of [{ isActive: true }, { roles: [] }, { tokenVersion: 1 }, { isAdminLocked: false }, { normalizedEmail: 'X' }])
    assert.ok(Object.keys(validateUser({ ...profile, password: 'valid-test-password', ...extra })).length);
  for (const bad of [{ email: 'bad' }, { departmentId: -1 }, { username: '' }, { displayName: 'a'.repeat(201) }, { password: ' '.repeat(12) }, { password: 'short' }, { phone: 'x'.repeat(31) }])
    assert.ok(Object.keys(validateUser({ ...profile, password: 'valid-test-password', ...bad })).length);
  assert.ok(validateUser({ ...profile, password: 'not-an-update', rowVersion: version }, { isUpdate: true }).password);
});
test('status commands are explicit independent axes with required reason and version', () => {
  for (const status of ['Active', 'Inactive', 'Locked', 'Unlocked']) assert.deepEqual(validateUserStatus({ status, reason: 'test reason', rowVersion: version }), {});
  for (const dto of [{ status: 'active' }, { reason: ' ' }, { rowVersion: 'stale' }, { isActive: false }])
    assert.ok(Object.keys(validateUserStatus({ status: 'Locked', reason: 'test reason', rowVersion: version, ...dto })).length);
});
test('role replacement requires explicitly present bounded unique numeric IDs, empty array is allowed', () => {
  assert.deepEqual(validateUserRoles({ roleIds: [], rowVersion: version }), {});
  assert.deepEqual(validateUserRoles({ roleIds: [31, 7], rowVersion: version }), {});
  for (const roleIds of [undefined, null, [1, 1], [0], [-1], ['7'], [1, 2, 3, 4]]) assert.ok(validateUserRoles({ roleIds, rowVersion: version }).roleIds);
  assert.ok(validateUserRoles({ roleIds: [], rowVersion: 'bad' }).rowVersion);
});
test('user and catalog reads preserve real IDs, query names, paging and same-origin paths', async () => {
  const { services, calls } = await client(() => ({ items: [{ id: 731, name: 'Actual role' }], totalPages: 1 }));
  await services.users.list({ roleId: 731, departmentId: 9, keyword: 'Thủy %_', page: 2, pageSize: 10 });
  await services.users.get(41); await services.users.account(41); await services.roles.list({ status: 'Active' });
  await services.roles.get(731); await services.roles.permissions({ module: 'users', keyword: 'read' });
  assert.equal(new URL(calls[1].url, 'http://localhost').searchParams.get('roleId'), '731');
  assert.equal(new URL(calls[1].url, 'http://localhost').searchParams.get('keyword'), 'Thủy %_');
  assert.deepEqual(calls.slice(2).map(c => c.url), ['/api/v1/users/41', '/api/v1/users/41/account', '/api/v1/roles?status=Active', '/api/v1/roles/731', '/api/v1/roles/permissions?module=users&keyword=read']);
  for (const call of calls.slice(1)) { assert.equal(call.headers.Authorization, 'Bearer synthetic-test-token'); assert.equal(call.method, 'GET'); assert.equal(call.cache, 'no-store'); }
});
test('create sends untouched password whitespace and never includes default roles/status', async () => {
  const { services, calls } = await client(() => ({ id: 41 }));
  await services.users.create({ ...profile, password: ' 0123456789  ' });
  const call = calls.at(-1); assert.equal(call.method, 'POST'); assert.equal(call.url, '/api/v1/users');
  const dto = JSON.parse(call.body); assert.equal(dto.password, ' 0123456789  ');
  assert.equal(Object.hasOwn(dto, 'roleIds'), false); assert.equal(Object.hasOwn(dto, 'isActive'), false);
  assert.equal(services.auth.session.id, 9);
});
test('profile status and role mutations use exact methods and rowVersion without extra fields', async () => {
  const { services, calls } = await client(() => ({ user: { id: 41 }, roleIds: [731], warnings: [] }));
  await services.users.update(41, { ...profile, rowVersion: version });
  await services.users.setStatus(41, { status: 'Locked', reason: 'test reason', rowVersion: version });
  await services.users.replaceRoles(41, { roleIds: [731], rowVersion: version });
  assert.deepEqual(calls.slice(1).map(c => [c.method, c.url]), [['PUT', '/api/v1/users/41'], ['PATCH', '/api/v1/users/41/status'], ['PUT', '/api/v1/users/41/roles']]);
  assert.deepEqual(JSON.parse(calls.at(-1).body), { roleIds: [731], rowVersion: version });
  assert.equal(services.auth.session.id, 9);
});
test('self account writes clear the in-memory session only after success', async () => {
  for (const operation of ['update', 'setStatus', 'replaceRoles']) {
    const { services } = await client(() => ({ user: { id: 9 }, warnings: [] }));
    const dto = operation === 'update' ? { ...profile, rowVersion: version } : operation === 'setStatus' ? { status: 'Active', reason: 'test', rowVersion: version } : { roleIds: [731], rowVersion: version };
    await services.users[operation](9, dto);
    assert.equal(services.auth.session, null);
    await assert.rejects(services.users.list(), error => error.status === 401);
  }
});
test('409 and 403 are surfaced once without blind retry or clearing a still-valid session', async () => {
  for (const [status, code] of [[409, 'LAST_ADMIN_PROTECTED'], [409, 'CONCURRENCY_CONFLICT'], [403, 'FORBIDDEN']]) {
    const { services, calls } = await client(() => new Response(JSON.stringify({ code, detail: 'safe failure' }), { status }));
    await assert.rejects(services.users.replaceRoles(9, { roleIds: [], rowVersion: version }), error => error.status === status && error.code === code);
    assert.equal(calls.length, 2); assert.equal(services.auth.session.id, 9);
  }
});
test('invalid write fields are rejected before fetch and IDs cannot inject URL paths', async () => {
  const { services, calls } = await client();
  await assert.rejects(services.users.create({ ...profile, password: 'valid-test-password', roleIds: [1] }), error => error.status === 400);
  await assert.rejects(services.users.setStatus(41, { status: 'Disable', reason: 'test', rowVersion: version }), error => error.status === 400);
  await assert.rejects(services.users.replaceRoles(41, { roleIds: null, rowVersion: version }), error => error.status === 400);
  assert.throws(() => services.users.account('../roles'), error => error.status === 400);
  assert.equal(calls.length, 1);
});
test('401 clears session and catalog reads never silently fall back to mock data', async () => {
  const { services } = await client(() => new Response(JSON.stringify({ code: 'UNAUTHORIZED', detail: 'login required' }), { status: 401 }));
  await assert.rejects(services.roles.list(), error => error.status === 401);
  assert.equal(services.auth.session, null); assert.equal(services.mode, 'api');
});
