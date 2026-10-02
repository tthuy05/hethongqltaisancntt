import assert from 'node:assert/strict';
import { readdir, readFile, stat } from 'node:fs/promises';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { frontendSource, projectRoot } from './frontend-build.mjs';

async function walk(directory) {
  const result = [];
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    assert(!entry.isSymbolicLink(), `Symbolic links are not allowed in frontend source: ${entry.name}`);
    const filename = path.join(directory, entry.name);
    if (entry.isDirectory()) result.push(...await walk(filename));
    else if (entry.isFile()) result.push(filename);
  }
  return result;
}

async function checkFrontend() {
  const sourceFiles = await walk(frontendSource);
  const toolingFiles = (await walk(path.join(projectRoot, 'scripts'))).filter((file) => file.endsWith('.mjs'));
  let syntaxChecks = 0;
  let contentChecks = 0;
  for (const file of [...sourceFiles, ...toolingFiles]) {
    if (/\.(?:js|mjs)$/u.test(file)) {
      const result = spawnSync(process.execPath, ['--check', file], { encoding: 'utf8', windowsHide: true });
      assert.equal(result.status, 0, `JavaScript syntax failed for ${path.relative(projectRoot, file)}:\n${result.stderr}`);
      syntaxChecks += 1;
    }
    if (!/\.(?:js|mjs|html|css|json)$/u.test(file)) continue;
    const content = await readFile(file, 'utf8');
    assert(!/postgres(?:ql)?:\/\//iu.test(content), `Frontend must not contain a database connection URI: ${file}`);
    if (file.endsWith('.html')) {
      assert(!/\son[a-z]+\s*=/iu.test(content), `Inline event handlers are not allowed by CSP: ${file}`);
      const scripts = content.match(/<script\b[^>]*>[\s\S]*?<\/script>/giu) || [];
      for (const script of scripts) {
        assert(/\bsrc\s*=\s*["'][^"']+["']/iu.test(script), `Inline scripts are not allowed by CSP: ${file}`);
        assert(!/\bsrc\s*=\s*["'](?:https?:)?\/\//iu.test(script), `Runtime scripts must be self-hosted: ${file}`);
        const source = script.match(/\bsrc\s*=\s*["']([^"']+)["']/iu)[1];
        assert((await stat(path.resolve(path.dirname(file), source))).isFile(), `Script entry point does not exist: ${source}`);
      }
      assert(!/<link\b[^>]*href\s*=\s*["'](?:https?:)?\/\//iu.test(content), `Fonts and CSS must be self-hosted: ${file}`);
    }
    if (/\.(?:js|mjs)$/u.test(file)) {
      const imports = content.matchAll(/\b(?:import|export)\s+(?:[^;]*?\s+from\s+)?["'](\.[^"']+)["']/gu);
      for (const [, specifier] of imports) {
        assert((await stat(path.resolve(path.dirname(file), specifier))).isFile(), `Local module import does not exist in ${file}: ${specifier}`);
      }
    }
    if (file.endsWith('.css')) assert(!/@import\s+(?:url\()?\s*["']?https?:/iu.test(content), `Runtime CSS must be self-hosted: ${file}`);
    contentChecks += 1;
  }
  assert(sourceFiles.some((file) => path.basename(file) === 'index.html'), 'Frontend entry point is missing.');
  console.log(`PASS: ${syntaxChecks} JavaScript syntax checks; ${contentChecks} CSP/database-boundary source checks.`);
  console.log('Source checks do not claim browser, accessibility, authentication, backend or database verification.');
}

checkFrontend().catch((error) => {
  console.error(error.message);
  process.exitCode = 1;
});
