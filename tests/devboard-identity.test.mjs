import assert from 'node:assert/strict';
import test from 'node:test';
import { scanText } from '../scripts/check-devboard-identity.mjs';

test('rejects stale current-product identity', () => {
  const hits = scanText('src/Foo.cs', 'namespace SourceGit.Models;\nvar exe = "SourceGit.exe";');
  assert.equal(hits.length, 2);
});

test('rejects old fork repository and spaced product name', () => {
  const hits = scanText('README.md', 'https://github.com/dhhieu113pro/sourcegit\n<Product>Dev Board</Product>');
  assert.equal(hits.length, 2);
});

test('allows upstream attribution', () => {
  assert.deepEqual(
    scanText('README.md', 'DevBoard is based on SourceGit (https://github.com/sourcegit-scm/sourcegit).'),
    [],
  );
});

test('allows explicit legacy migration references', () => {
  assert.deepEqual(
    scanText('src/Native/DataDirectoryResolver.cs', 'const string LegacyProductName = "SourceGit"; // legacy-migration'),
    [],
  );
});
