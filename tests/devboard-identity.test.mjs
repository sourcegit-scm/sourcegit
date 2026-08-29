import assert from 'node:assert/strict';
import fs from 'node:fs';
import test from 'node:test';
import { scanText } from '../scripts/check-devboard-identity.mjs';

test('rejects stale current-product identity', () => {
  const hits = scanText('src/Foo.cs', 'namespace SourceGit.Models;\nvar exe = "SourceGit.exe";');
  assert.equal(hits.length, 2);
});

test('rejects generic SourceGit and spaced Dev Board current-product copy', () => {
  const hits = scanText('README.md', 'Run SourceGit to open your workspace.\n# Dev Board');
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

test('uses DevBoard Linux package resource filenames', () => {
  const required = [
    'build/resources/_common/applications/devboard.desktop',
    'build/resources/_common/icons/devboard.png',
    'build/resources/appimage/devboard.appdata.xml',
    'build/resources/flatpak/devboard.desktop',
  ];

  for (const path of required) assert.equal(fs.existsSync(path), true, `${path} should exist`);
});

test('package metadata uses DevBoard identity', () => {
  const files = [
    'build/resources/_common/applications/devboard.desktop',
    'build/resources/appimage/devboard.appdata.xml',
    'build/resources/flatpak/devboard.desktop',
    'build/resources/deb/DEBIAN/control',
    'build/resources/app/App.plist',
  ];

  for (const path of files) {
    const content = fs.readFileSync(path, 'utf8');
    assert.equal(/\bSourceGit\b|\bsourcegit\b/.test(content), false, `${path} should use DevBoard/devboard identity`);
  }
});
