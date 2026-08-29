import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const forbidden = [
  /\bnamespace\s+SourceGit(?:\.|\b)/g,
  /\bSourceGit\.exe\b/g,
  /\bsrc\/SourceGit\.csproj\b/g,
  /\bSourceGit\.Tests\b/g,
  /github\.com\/dhhieu113pro\/sourcegit\b/g,
  /<Product>Dev Board<\/Product>/g,
];

const skippedDirectories = new Set(['.git', 'bin', 'obj', 'artifacts', 'node_modules']);
const scannedExtensions = new Set(['.cs', '.axaml', '.csproj', '.slnx', '.md', '.yml', '.yaml', '.ps1', '.mjs', '.json', '.xml', '.plist', '.desktop']);

function isAllowed(pathname, line) {
  if (pathname === 'LICENSE' || pathname === 'THIRD-PARTY-LICENSES.md') return true;
  if (pathname.includes('/superpowers/specs/') || pathname.includes('/superpowers/plans/')) return true;
  if (line.includes('sourcegit-scm/sourcegit')) return true;
  if (line.includes('legacy-migration')) return true;
  return false;
}

export function scanText(pathname, text) {
  const hits = [];
  const lines = text.split(/\r?\n/);
  for (let index = 0; index < lines.length; index++) {
    const line = lines[index];
    if (isAllowed(pathname, line)) continue;
    for (const pattern of forbidden) {
      pattern.lastIndex = 0;
      while (pattern.exec(line)) {
        hits.push({ path: pathname, line: index + 1, text: line.trim() });
        if (!pattern.global) break;
      }
    }
  }
  return hits;
}

export function scanIdentity(rootDir) {
  const hits = [];
  function walk(current) {
    for (const entry of fs.readdirSync(current, { withFileTypes: true })) {
      if (entry.isDirectory() && skippedDirectories.has(entry.name)) continue;
      const full = path.join(current, entry.name);
      if (entry.isDirectory()) {
        walk(full);
        continue;
      }
      const relative = path.relative(rootDir, full).split(path.sep).join('/');
      const extension = path.extname(entry.name);
      if (!scannedExtensions.has(extension) && !['LICENSE', 'README.md', 'TRANSLATION.md'].includes(entry.name)) continue;
      hits.push(...scanText(relative, fs.readFileSync(full, 'utf8')));
    }
  }
  walk(rootDir);
  return hits;
}

const invoked = process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (invoked) {
  const root = process.cwd();
  const hits = scanIdentity(root);
  for (const hit of hits) console.error(`${hit.path}:${hit.line}: ${hit.text}`);
  if (hits.length) process.exitCode = 1;
}
