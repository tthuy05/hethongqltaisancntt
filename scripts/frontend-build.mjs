import { copyFile, mkdir, readdir, readFile, lstat } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawn } from 'node:child_process';

export const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
export const frontendSource = path.join(projectRoot, 'src', 'ItAssetManagement.Api', 'wwwroot');
export const frontendOutput = path.join(projectRoot, 'artifacts', 'frontend');
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

export async function assertWritableOutput(candidate) {
  assertChild(projectRoot, candidate);
  const relative = path.relative(frontendOutput, candidate);
  if (relative === '..' || relative.startsWith(`..${path.sep}`) || path.isAbsolute(relative)) throw new Error('Outside frontend output.');
  let current = projectRoot;
  for (const segment of path.relative(projectRoot, candidate).split(path.sep)) {
    current = path.join(current, segment);
    try { if ((await lstat(current)).isSymbolicLink()) throw new Error('Build destination cannot contain symbolic links or junctions.'); }
    catch (error) { if (error.code === 'ENOENT') break; throw error; }
  }
}

export async function buildFrontend() {
  assertChild(projectRoot, frontendOutput);
  await assertWritableOutput(frontendOutput);
  await readFile(path.join(frontendSource, 'index.html'));
  await mkdir(frontendOutput, { recursive: true });
  await copyPublicDirectory(frontendSource, frontendOutput);
  const fontDirectory = path.join(frontendOutput, 'assets', 'fonts');
  await assertWritableOutput(fontDirectory);
  await mkdir(fontDirectory, { recursive: true });
  const interRoot = path.join(projectRoot, 'node_modules', '@fontsource', 'inter');
  for (const subset of ['latin', 'vietnamese']) {
    for (const weight of [400, 500, 600]) {
      const filename = `inter-${subset}-${weight}-normal.woff2`;
      await assertWritableOutput(path.join(fontDirectory, filename));
      await copyFile(path.join(interRoot, 'files', filename), path.join(fontDirectory, filename));
    }
  }
  await assertWritableOutput(path.join(fontDirectory, 'OFL.txt'));
  await copyFile(path.join(interRoot, 'LICENSE'), path.join(fontDirectory, 'OFL.txt'));
  await assertWritableOutput(path.join(frontendOutput, 'css', 'app.css'));
  await mkdir(path.join(frontendOutput, 'css'), { recursive: true });
  await run(process.execPath, [
    path.join(projectRoot, 'node_modules', 'tailwindcss', 'lib', 'cli.js'),
    '--config', path.join(projectRoot, 'frontend', 'tailwind.config.cjs'),
    '--input', path.join(frontendSource, 'css', 'input.css'),
    '--output', path.join(frontendOutput, 'css', 'app.css'),
    '--minify',
  ]);
  console.log(`Frontend build ready: ${frontendOutput}`);
  console.log('Local CSS and Inter fonts; MOCK development services only. No backend or database was contacted.');
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  buildFrontend().catch((error) => {
    console.error(error.message);
    process.exitCode = 1;
  });
}
