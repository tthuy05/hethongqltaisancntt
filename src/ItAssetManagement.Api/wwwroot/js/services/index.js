import { createMockServices } from './mock-services.js';
import { createApiServices } from './api-services.js';
export { ServiceError } from './service-error.js';

/**
 * Factory isolates sessions/data for tests. Explicit mock is still localhost-only.
 * Browser default additionally requires ?demo=1; API mode never falls back to mock.
 */
export function createServices({ mode = 'api', hostname = globalThis.location?.hostname ?? '', ...options } = {}) {
  if (mode === 'mock') return createMockServices({ hostname, ...options });
  if (mode === 'api') return createApiServices(options);
  throw new TypeError(`Unsupported service mode: ${mode}`);
}

const hostname = globalThis.location?.hostname ?? '';
const localHost = ['localhost', '127.0.0.1'].includes(hostname);
const demoRequested = new URLSearchParams(globalThis.location?.search ?? '').get('demo') === '1';
const softwarePreview = localHost && !demoRequested && new URLSearchParams(globalThis.location?.search ?? '').get('software') === '1';
const licensePreview = localHost && !demoRequested && (new URLSearchParams(globalThis.location?.search ?? '').get('license') === '1' || softwarePreview);
export const services = createServices({ mode: localHost && demoRequested ? 'mock' : 'api', hostname, reportingEnabled: localHost && !demoRequested && new URLSearchParams(globalThis.location?.search ?? '').get('reports') === '1', replacementEnabled: localHost && !demoRequested && new URLSearchParams(globalThis.location?.search ?? '').get('replacement') === '1', assignmentEnabled: true, maintenanceEnabled: true, importExportEnabled: true, softwareEnabled: softwarePreview, licenseEnabled: licensePreview, licenseAllocationEnabled: licensePreview && new URLSearchParams(globalThis.location?.search ?? '').get('allocation') === '1' });
export const { auth, assets, departments, assetTypes, dashboard, importExport } = services;
export default services;
