import assert from 'node:assert/strict';
import test from 'node:test';
import { readFile } from 'node:fs/promises';

test('MSIX packager contains Store safety contracts', async () => {
  const script = await readFile(new URL('../scripts/build-store-msix.ps1', import.meta.url), 'utf8');
  assert.match(script, /ValidateSet\('x64', 'arm64'\)/);
  assert.match(script, /PublishDir/);
  assert.match(script, /Dev Board/);
  assert.match(script, /runFullTrust/);
  assert.match(script, /10\.0\.19041\.0/);
  assert.match(script, /Square44x44Logo\.png/);
  assert.match(script, /Square150x150Logo\.png/);
  assert.match(script, /StoreLogo\.png/);
  assert.match(script, /makeappx\.exe/i);
  assert.match(script, /Copy-Item .*PublishDir.*\*.*-Recurse -Force/);
});
