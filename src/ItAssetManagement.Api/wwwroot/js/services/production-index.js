import { createApiServices } from './api-services.js';
export { ServiceError } from './service-error.js';

/** Production entry: never select demo services, including on localhost. */
export function createServices({ mode = 'api', ...options } = {}) {
  if (mode !== 'api') throw new TypeError(`Unsupported production service mode: ${mode}`);
  return createApiServices(options);
}

export const services = createServices();
export const { auth, assets, departments, assetTypes, dashboard } = services;
export default services;
