export class ServiceError extends Error {
  constructor(status, code, message, errors = {}, extra = {}) {
    super(message);
    this.name = 'ServiceError';
    this.status = status;
    this.code = code;
    this.errors = errors;
    this.traceId = extra.traceId ?? null;
    this.retryAfter = extra.retryAfter ?? null;
  }
}
