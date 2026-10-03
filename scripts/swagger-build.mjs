import { copyFile, mkdir, lstat } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
export const swaggerOutput = path.join(root, 'artifacts', 'swagger');

export async function buildSwaggerUi() {
  // Explicit output directory/files, reject junctions instead of following them.
  let current = root;
  for (const segment of ['artifacts', 'swagger']) {
    current = path.join(current, segment);
    try { if ((await lstat(current)).isSymbolicLink()) throw new Error('Swagger output cannot contain a junction.'); }
    catch (error) { if (error.code !== 'ENOENT') throw error; }
  }
  await mkdir(swaggerOutput, { recursive: true });
  const distribution = path.join(root, 'node_modules', 'swagger-ui-dist');
  const files = [
    ['index.html', path.join(root, 'frontend', 'swagger', 'index.html')],
    ['init.js', path.join(root, 'frontend', 'swagger', 'init.js')],
    ['site.css', path.join(root, 'frontend', 'swagger', 'site.css')],
    ...['swagger-ui-bundle.js', 'swagger-ui.css', 'LICENSE'].map(name => [name, path.join(distribution, name)]),
  ];
  for (const [name, source] of files) {
    const destination = path.join(swaggerOutput, name);
    try { if ((await lstat(destination)).isSymbolicLink()) throw new Error('Swagger output file cannot be a symbolic link.'); }
    catch (error) { if (error.code !== 'ENOENT') throw error; }
    await copyFile(source, destination);
  }
  console.log('Development Swagger UI built from pinned local swagger-ui-dist; no CDN.');
}
