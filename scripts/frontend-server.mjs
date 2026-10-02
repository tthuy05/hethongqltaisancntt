import http from 'node:http';
import { readFile, realpath, stat } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { buildFrontend, frontendOutput, projectRoot } from './frontend-build.mjs';

export const contentSecurityPolicy = [
  "default-src 'self'",
  "script-src 'self'",
  "style-src 'self'",
  "font-src 'self'",
  "img-src 'self' data:",
  "connect-src 'self'",
  "object-src 'none'",
  "base-uri 'none'",
  "frame-ancestors 'none'",
  "form-action 'self'",
].join('; ');

// An explicit, temporary comparison mode for the untrusted ORIGINAL Stitch export.
// It is forbidden for repository/source/build roots and never used by the final UI.
const originalExportPolicy = [
  "default-src 'self'",
  "script-src 'self' https://cdn.tailwindcss.com 'unsafe-inline' 'unsafe-eval'",
  "style-src 'self' https://fonts.googleapis.com 'unsafe-inline'",
  "font-src 'self' https://fonts.gstatic.com",
  "img-src 'self' data: https://lh3.googleusercontent.com",
  "connect-src 'none'",
  "object-src 'none'",
  "base-uri 'none'",
  "frame-ancestors 'none'",
  "form-action 'none'",
].join('; ');

const mimeTypes = new Map([
  ['.html', 'text/html; charset=utf-8'], ['.css', 'text/css; charset=utf-8'],
  ['.js', 'text/javascript; charset=utf-8'], ['.mjs', 'text/javascript; charset=utf-8'],
  ['.json', 'application/json; charset=utf-8'], ['.svg', 'image/svg+xml'],
  ['.png', 'image/png'], ['.jpg', 'image/jpeg'], ['.jpeg', 'image/jpeg'],
  ['.webp', 'image/webp'], ['.ico', 'image/x-icon'], ['.woff2', 'font/woff2'],
  ['.txt', 'text/plain; charset=utf-8'],
]);

function isChildOrRoot(root, candidate) {
  const relative = path.relative(root, candidate);
  return relative === '' || (relative !== '..' && !relative.startsWith(`..${path.sep}`) && !path.isAbsolute(relative));
}

function respond(response, status, message) {
  response.writeHead(status, { 'Content-Type': 'text/plain; charset=utf-8', 'Content-Length': Buffer.byteLength(message) });
  response.end(message);
}

/** Read-only static preview. No proxy, API implementation, database client or directory listing. */
export async function createStaticServer({ root = frontendOutput, previewOriginal = false } = {}) {
  const absoluteRoot = await realpath(path.resolve(root));
  if (!(await stat(absoluteRoot)).isDirectory()) throw new Error('Static root must be a directory.');
  if (previewOriginal && (isChildOrRoot(projectRoot, absoluteRoot) || isChildOrRoot(absoluteRoot, projectRoot))) {
    throw new Error('Original Stitch preview root must be outside the repository and must not contain it.');
  }
  return http.createServer(async (request, response) => {
    response.setHeader('Content-Security-Policy', previewOriginal ? originalExportPolicy : contentSecurityPolicy);
    response.setHeader('X-Content-Type-Options', 'nosniff');
    response.setHeader('Referrer-Policy', 'no-referrer');
    response.setHeader('X-Frame-Options', 'DENY');
    response.setHeader('Cache-Control', 'no-store');
    response.setHeader('Permissions-Policy', 'camera=(), microphone=(), geolocation=()');
    if (!['GET', 'HEAD'].includes(request.method)) {
      response.setHeader('Allow', 'GET, HEAD');
      respond(response, 405, 'Static frontend only. API backend is not implemented.');
      return;
    }
    let pathname;
    try {
      pathname = decodeURIComponent((request.url || '/').split('?')[0]);
    } catch {
      respond(response, 400, 'Invalid URL encoding.');
      return;
    }
    if (!pathname.startsWith('/') || pathname.includes('\\') || /[\x00-\x1f]/u.test(pathname)
      || pathname.split('/').some((segment) => segment === '..' || segment.startsWith('.'))
      || pathname.includes('%')) {
      respond(response, 400, 'Invalid static path.');
      return;
    }
    if (pathname === '/api' || pathname.startsWith('/api/')) {
      respond(response, 404, 'API backend is not implemented. This server only serves frontend files.');
      return;
    }
    const candidate = path.resolve(absoluteRoot, `.${pathname === '/' ? '/index.html' : pathname}`);
    if (!isChildOrRoot(absoluteRoot, candidate)) {
      respond(response, 403, 'Outside the static root.');
      return;
    }
    try {
      const actualPath = await realpath(candidate);
      if (!isChildOrRoot(absoluteRoot, actualPath)) {
        respond(response, 403, 'Outside the static root.');
        return;
      }
      if (!(await stat(actualPath)).isFile()) {
        respond(response, 404, 'File not found. Directory listing is disabled.');
        return;
      }
      const contentType = mimeTypes.get(path.extname(actualPath).toLowerCase());
      if (!contentType) {
        respond(response, 404, 'File type not served.');
        return;
      }
      const contents = await readFile(actualPath);
      response.writeHead(200, { 'Content-Type': contentType, 'Content-Length': contents.length });
      response.end(request.method === 'HEAD' ? undefined : contents);
    } catch (error) {
      if (['ENOENT', 'ENOTDIR'].includes(error.code)) respond(response, 404, 'File not found.');
      else respond(response, 500, 'Unable to serve this file.');
    }
  });
}

function argument(name, fallback) {
  const position = process.argv.indexOf(name);
  if (position < 0) return fallback;
  const value = process.argv[position + 1];
  if (!value || value.startsWith('--')) throw new Error(`${name} requires a value.`);
  return value;
}

async function main() {
  const previewOriginal = process.argv.includes('--stitch-preview');
  if (previewOriginal && (process.argv.includes('--build') || !process.argv.includes('--root'))) {
    throw new Error('Original Stitch preview requires an explicit --root outside the repository and cannot build the final frontend.');
  }
  if (process.argv.includes('--build')) await buildFrontend();
  const root = path.resolve(argument('--root', frontendOutput));
  const port = Number(argument('--port', previewOriginal ? '4174' : process.env.FRONTEND_PORT || '4173'));
  if (!Number.isInteger(port) || port < 1 || port > 65535) throw new Error('Port must be an integer from 1 to 65535.');
  if (previewOriginal && port !== 4174) throw new Error('Original Stitch comparison preview is restricted to localhost port 4174.');
  const server = await createStaticServer({ root, previewOriginal });
  server.on('error', (error) => {
    console.error(error.message);
    process.exitCode = 1;
  });
  server.listen(port, '127.0.0.1', () => {
    console.log(`Frontend preview: http://127.0.0.1:${port}/`);
    console.log(`Demo navigation: http://127.0.0.1:${port}/?demo=1#/login`);
    console.log(`Read-only static root: ${root}`);
    if (previewOriginal) console.log('ORIGINAL STITCH EXPORT ONLY: relaxed CDN/inline CSP for comparison; not the final frontend or an authentication environment.');
    console.log('API / authentication backend / database: NOT IMPLEMENTED or NOT CONNECTED.');
  });
  for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => server.close(() => process.exit(0)));
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch((error) => {
    console.error(error.message);
    process.exitCode = 1;
  });
}
