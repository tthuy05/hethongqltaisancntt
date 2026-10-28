import test from 'node:test';
import assert from 'node:assert/strict';
import { services } from '../src/ItAssetManagement.Api/wwwroot/js/services/production-index.js';

test('approved production software/license entry retains existing modules and excludes reporting', () => {
  assert.equal(services.mode, 'api');
  for (const feature of ['assignmentEnabled', 'maintenanceEnabled', 'importExportEnabled', 'softwareEnabled', 'licenseEnabled', 'licenseAllocationEnabled'])
    assert.equal(services[feature], true, feature);
  assert.equal(services.reportingEnabled, false);
  assert.ok(services.software && services.licenses && services.licenseAssignments);
  assert.equal(services.auth.session, null);
});
