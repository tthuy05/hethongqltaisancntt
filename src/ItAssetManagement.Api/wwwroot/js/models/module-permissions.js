// Presentation hints only. Never replaces server grants, object scope or state
// validation. Call again after auth/me refresh; don't persist the result.
export function modulePermissions(user) {
  const valid = Number.isSafeInteger(user?.id) && user.id > 0 &&
    Array.isArray(user.roles) && user.roles.every(role => typeof role === 'string') &&
    Array.isArray(user.permissions) && user.permissions.every(permission => typeof permission === 'string');
  const has = permission => valid && user.permissions.includes(permission);
  const admin = valid && user.roles.includes('ADMIN_IT');
  const globalMaintenanceScope = valid && (admin || user.roles.includes('SYSTEM_MANAGER'));
  const readMaintenance = has('maintenance.read');
  const readLicenses = has('licenses.read');
  return Object.freeze({
    maintenance: Object.freeze({
      read: readMaintenance,
      create: readMaintenance && has('maintenance.create'),
      globalScope: readMaintenance && globalMaintenanceScope,
      assign: readMaintenance && globalMaintenanceScope && has('maintenance.assign'),
      readCost: readMaintenance && globalMaintenanceScope && has('maintenance.cost.write'),
      writeCost: readMaintenance && globalMaintenanceScope && has('maintenance.cost.write')
    }),
    licenses: Object.freeze({
      read: readLicenses,
      manageKey: readLicenses && admin && has('licenses.key.manage'),
      revealKey: readLicenses && admin && has('licenses.key.reveal')
    }),
    // Existing asset cost grant, not an invented license-cost permission.
    readAssetCost: has('assets.cost.read')
  });
}
