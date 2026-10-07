import { copyFile, mkdir, readdir, readFile, lstat, rm, stat } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawn } from 'node:child_process';
import { buildSwaggerUi } from './swagger-build.mjs';

export const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
export const frontendSource = path.join(projectRoot, 'src', 'ItAssetManagement.Api', 'wwwroot');
export const frontendOutput = path.join(projectRoot, 'artifacts', 'frontend');
export const frontendProductionOutput = path.join(projectRoot, 'artifacts', 'frontend-production');
const allowedExtensions = new Set(['.html', '.css', '.js', '.mjs', '.json', '.svg', '.png', '.jpg', '.jpeg', '.webp', '.ico', '.woff2', '.txt']);

function assertChild(parent, candidate) {
  const relative = path.relative(parent, candidate);
  if (!relative || relative === '..' || relative.startsWith(`..${path.sep}`) || path.isAbsolute(relative)) {
    throw new Error(`Build output must remain below its declared root: ${candidate}`);
  }
}

async function copyPublicDirectory(source, destination) {
  await assertWritableOutput(destination);
  await mkdir(destination, { recursive: true });
  for (const entry of await readdir(source, { withFileTypes: true })) {
    if (entry.name.startsWith('.') || entry.isSymbolicLink()) {
      throw new Error(`Hidden files and symbolic links are not public build inputs: ${entry.name}`);
    }
    const input = path.join(source, entry.name);
    const output = path.join(destination, entry.name);
    assertChild(frontendSource, input);
    assertChild(frontendOutput, output);
    if (entry.isDirectory()) {
      await copyPublicDirectory(input, output);
    } else if (entry.isFile()) {
      if (path.relative(frontendSource, input).replaceAll(path.sep, '/') === 'css/input.css') continue;
      if (!allowedExtensions.has(path.extname(entry.name).toLowerCase())) {
        throw new Error(`Unexpected public file extension: ${entry.name}`);
      }
      await assertWritableOutput(output);
      await copyFile(input, output);
    }
  }
}

function run(command, args) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, { cwd: projectRoot, stdio: 'inherit', windowsHide: true });
    child.on('error', reject);
    child.on('exit', (code) => code === 0 ? resolve() : reject(new Error(`Build command exited with code ${code}`)));
  });
}

export async function assertWritableOutput(candidate, outputRoot = frontendOutput) {
  if (![frontendOutput, frontendProductionOutput].includes(outputRoot)) throw new Error('Unknown frontend output root.');
  assertChild(projectRoot, candidate);
  const relative = path.relative(outputRoot, candidate);
  if (relative === '..' || relative.startsWith(`..${path.sep}`) || path.isAbsolute(relative)) throw new Error('Outside frontend output.');
  let current = projectRoot;
  for (const segment of path.relative(projectRoot, candidate).split(path.sep)) {
    current = path.join(current, segment);
    try { if ((await lstat(current)).isSymbolicLink()) throw new Error('Build destination cannot contain symbolic links or junctions.'); }
    catch (error) { if (error.code === 'ENOENT') break; throw error; }
  }
}

function localReference(directory, reference) {
  if (!reference.startsWith('.')) throw new Error(`Production assets must use local relative references: ${reference}`);
  const filename = path.resolve(directory, reference);
  assertChild(frontendSource, filename);
  return filename;
}

async function assertProductionSource(candidate) {
  assertChild(frontendSource, candidate);
  let current = frontendSource;
  for (const segment of path.relative(frontendSource, candidate).split(path.sep)) {
    if (segment.startsWith('.')) throw new Error('Hidden files are not production inputs.');
    current = path.join(current, segment);
    if ((await lstat(current)).isSymbolicLink()) throw new Error('Production inputs cannot contain symbolic links or junctions.');
  }
  if (!(await stat(candidate)).isFile() || !allowedExtensions.has(path.extname(candidate).toLowerCase())) throw new Error('Unexpected production input file.');
}

// Follow the real module graph rather than copy every public source. The only
// substitution is the explicit API-only factory; its imports remain resolvable.
async function copyProductionDirectory() {
  const copied = new Set();
  async function copySource(requested) {
    if (copied.has(requested)) return;
    copied.add(requested);
    assertChild(frontendSource, requested);
    const relative = path.relative(frontendSource, requested);
    const source = relative.replaceAll(path.sep, '/') === 'js/services/index.js'
      ? path.join(frontendSource, 'js', 'services', 'production-index.js')
      : requested;
    await assertProductionSource(source);
    const output = path.join(frontendProductionOutput, relative);
    await assertWritableOutput(output, frontendProductionOutput);
    await mkdir(path.dirname(output), { recursive: true });
    await copyFile(source, output);
    if (path.extname(source) === '.js') {
      const content = await readFile(source, 'utf8');
      if (/\bimport\s*\(/u.test(content)) throw new Error('Dynamic imports need an explicit production packaging review.');
      for (const [, reference] of content.matchAll(/\b(?:import|export)\s+(?:[^;]*?\s+from\s+)?["']([^"']+)["']/gu)) {
        await copySource(localReference(path.dirname(source), reference));
      }
    }
  }
  const html = await readFile(path.join(frontendSource, 'index.html'), 'utf8');
  await copySource(path.join(frontendSource, 'index.html'));
  for (const [, reference] of html.matchAll(/\b(?:src|href)=["']([^"']+)["']/gu)) {
    if (reference.startsWith('#') || reference === './css/app.css') continue;
    await copySource(localReference(frontendSource, reference));
  }
}

export async function verifyProductionFrontend() {
  const files = [];
  async function inspect(directory) {
    await assertWritableOutput(directory, frontendProductionOutput);
    for (const entry of await readdir(directory, { withFileTypes: true })) {
      if (entry.isSymbolicLink() || entry.name.startsWith('.')) throw new Error('Hidden files and links are not production assets.');
      const file = path.join(directory, entry.name);
      if (entry.isDirectory()) { await inspect(file); continue; }
      const relative = path.relative(frontendProductionOutput, file).replaceAll(path.sep, '/');
      if (!entry.isFile() || !allowedExtensions.has(path.extname(entry.name).toLowerCase()) || /(?:^|\/)(?:mock|swagger|private|credentials)(?:\/|$)|(?:mock-services|production-index)\.js$/iu.test(relative)) {
        throw new Error(`Unexpected production asset: ${relative}`);
      }
      files.push(relative);
      if (!/\.(?:html|js|css|json|txt)$/u.test(file)) continue;
      const content = await readFile(file, 'utf8');
      if (/postgres(?:ql)?:\/\/|\bnpg_[a-z0-9]+\b|ConnectionStrings(?::|__)DefaultConnection/iu.test(content)) throw new Error('Database credentials/configuration cannot be frontend assets.');
      if (file.endsWith('.js')) {
        for (const [, reference] of content.matchAll(/\b(?:import|export)\s+(?:[^;]*?\s+from\s+)?["']([^"']+)["']/gu)) {
          if (!reference.startsWith('.')) throw new Error('Production modules must be self-hosted.');
          const dependency = path.resolve(path.dirname(file), reference);
          await assertWritableOutput(dependency, frontendProductionOutput);
          if (!(await stat(dependency)).isFile()) throw new Error('A production module dependency is missing.');
        }
      }
    }
  }
  await inspect(frontendProductionOutput);
  const entry = await readFile(path.join(frontendProductionOutput, 'js', 'services', 'index.js'), 'utf8');
  if (!entry.includes("mode !== 'api'") || /createMockServices|demoRequested/u.test(entry)) throw new Error('Production service factory must be API-only.');
  return files.sort();
}

export async function buildFrontend({ production = false } = {}) {
  const outputRoot = production ? frontendProductionOutput : frontendOutput;
  assertChild(projectRoot, outputRoot);
  await assertWritableOutput(outputRoot, outputRoot);
  await readFile(path.join(frontendSource, 'index.html'));
  // The exact, validated production-only directory is disposable build output.
  // Cleaning it prevents removed/old development files surviving a later build.
  if (production) await rm(outputRoot, { recursive: true, force: true });
  await mkdir(outputRoot, { recursive: true });
  if (production) await copyProductionDirectory();
  else await copyPublicDirectory(frontendSource, outputRoot);
  const fontDirectory = path.join(outputRoot, 'assets', 'fonts');
  await assertWritableOutput(fontDirectory, outputRoot);
  await mkdir(fontDirectory, { recursive: true });
  const interRoot = path.join(projectRoot, 'node_modules', '@fontsource', 'inter');
  for (const subset of ['latin', 'vietnamese']) {
    for (const weight of [400, 500, 600]) {
      const filename = `inter-${subset}-${weight}-normal.woff2`;
      await assertWritableOutput(path.join(fontDirectory, filename), outputRoot);
      await copyFile(path.join(interRoot, 'files', filename), path.join(fontDirectory, filename));
    }
  }
  await assertWritableOutput(path.join(fontDirectory, 'OFL.txt'), outputRoot);
  await copyFile(path.join(interRoot, 'LICENSE'), path.join(fontDirectory, 'OFL.txt'));
  await assertWritableOutput(path.join(outputRoot, 'css', 'app.css'), outputRoot);
  await mkdir(path.join(outputRoot, 'css'), { recursive: true });
  await run(process.execPath, [
    path.join(projectRoot, 'node_modules', 'tailwindcss', 'lib', 'cli.js'),
    '--config', path.join(projectRoot, 'frontend', 'tailwind.config.cjs'),
    '--input', path.join(frontendSource, 'css', 'input.css'),
    '--output', path.join(outputRoot, 'css', 'app.css'),
    '--minify',
  ]);
  console.log(`Frontend build ready: ${outputRoot}`);
  if (production) {
    const files = await verifyProductionFrontend();
    console.log(`Production API-only package: ${files.length} local assets; no mock modules or Swagger. Build does not contact a database.`);
  } else {
    console.log('Local CSS and Inter fonts; API mode by default, explicit localhost demo retained. Build does not contact a database.');
    await buildSwaggerUi();
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const args = process.argv.slice(2);
  if (args.some(argument => argument !== '--production')) throw new Error('Usage: node scripts/frontend-build.mjs [--production]');
  buildFrontend({ production: args.includes('--production') }).catch((error) => {
    console.error(error.message);
    process.exitCode = 1;
  });
}
