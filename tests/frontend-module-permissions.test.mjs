import test from 'node:test';
import assert from 'node:assert/strict';
import { modulePermissions } from '../src/ItAssetManagement.Api/wwwroot/js/models/module-permissions.js';

const grants = ['maintenance.read', 'maintenance.create', 'maintenance.assign', 'maintenance.cost.write',
  'licenses.read', 'licenses.key.manage', 'licenses.key.reveal', 'assets.cost.read'];
const user = (roles, permissions = grants) => ({ id: 7, roles, permissions });

test('Admin receives only explicitly granted key/cost hints', () => {
  const flags = modulePermissions(user(['ADMIN_IT']));
  assert.equal(flags.licenses.manageKey, true); assert.equal(flags.licenses.revealKey, true);
  assert.equal(flags.maintenance.assign, true); assert.equal(flags.maintenance.readCost, true);
  assert.equal(flags.readAssetCost, true);
  const noGrants = modulePermissions(user(['ADMIN_IT'], []));
  assert.equal(noGrants.licenses.revealKey, false); assert.equal(noGrants.maintenance.read, false);
});
test('Manager has global maintenance scope but never key manage/reveal on misgrant', () => {
  const flags = modulePermissions(user(['SYSTEM_MANAGER']));
  assert.equal(flags.maintenance.globalScope, true); assert.equal(flags.maintenance.writeCost, true);
  assert.equal(flags.licenses.read, true); assert.equal(flags.licenses.manageKey, false);
  assert.equal(flags.licenses.revealKey, false);
});
test('Support/custom groups remain scoped despite key/cost/assignment misgrants', () => {
  for (const roles of [['TECHNICAL_SUPPORT'], ['CUSTOM_GROUP']]) {
    const flags = modulePermissions(user(roles));
    assert.equal(flags.maintenance.read, true); assert.equal(flags.maintenance.globalScope, false);
    assert.equal(flags.maintenance.assign, false); assert.equal(flags.maintenance.readCost, false);
    assert.equal(flags.maintenance.writeCost, false); assert.equal(flags.licenses.manageKey, false);
    assert.equal(flags.licenses.revealKey, false);
  }
});
test('Missing read grant disables dependent privileged hints', () => {
  const flags = modulePermissions(user(['ADMIN_IT'], grants.filter(p => !p.endsWith('.read'))));
  assert.equal(flags.maintenance.create, false); assert.equal(flags.maintenance.assign, false);
  assert.equal(flags.maintenance.writeCost, false); assert.equal(flags.licenses.manageKey, false);
  assert.equal(flags.licenses.revealKey, false);
});
test('Malformed/anonymous sessions default deny without throwing', () => {
  for (const session of [null, {}, { ...user(['ADMIN_IT']), id: '7' },
    { ...user(['ADMIN_IT']), roles: 'ADMIN_IT' }, { ...user(['ADMIN_IT']), permissions: 'licenses.read' },
    { ...user(['ADMIN_IT']), permissions: [null] }]) {
    const flags = modulePermissions(session);
    assert.equal(flags.maintenance.read, false); assert.equal(flags.licenses.read, false);
    assert.equal(flags.readAssetCost, false);
  }
});
test('Refresh recomputes flags and results are immutable without retaining session/secrets', () => {
  const session = { ...user(['ADMIN_IT']), licenseKey: 'synthetic-secret' };
  const before = modulePermissions(session);
  session.permissions = []; const after = modulePermissions(session);
  assert.equal(before.licenses.revealKey, true); assert.equal(after.licenses.revealKey, false);
  assert.ok(Object.isFrozen(before)); assert.ok(Object.isFrozen(before.licenses));
  assert.ok(Object.isFrozen(before.maintenance)); assert.doesNotMatch(JSON.stringify(before), /synthetic-secret/);
  assert.throws(() => { before.licenses.revealKey = false; }, TypeError);
});
