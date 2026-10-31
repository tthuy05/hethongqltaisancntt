import test from 'node:test';
import assert from 'node:assert/strict';
import { services } from '../src/ItAssetManagement.Api/wwwroot/js/services/production-index.js';

test('approved production software/license entry retains existing modules and includes reporting', () => {
  assert.equal(services.mode, 'api');
  for (const feature of ['assignmentEnabled', 'maintenanceEnabled', 'importExportEnabled', 'softwareEnabled', 'licenseEnabled', 'licenseAllocationEnabled', 'replacementEnabled', 'reportingEnabled'])
    assert.equal(services[feature], true, feature);
  assert.ok(services.software && services.licenses && services.licenseAssignments);
  assert.equal(services.auth.session, null);
});
