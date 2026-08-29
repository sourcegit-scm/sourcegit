import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const scriptUrl = new URL('../scripts/devspaces-screenshots.ps1', import.meta.url);
const workflowUrl = new URL('../.github/workflows/devspaces-screenshots.yml', import.meta.url);

test('DevSpaces screenshot CI launches and captures the real DevBoard app', async () => {
  const [script, workflow] = await Promise.all([
    readFile(scriptUrl, 'utf8'),
    readFile(workflowUrl, 'utf8'),
  ]);

  assert.match(workflow, /runs-on: windows-latest/);
  assert.match(script, /dotnet publish src\/DevBoard\.csproj/);
  assert.match(script, /DevBoard\.exe/);
  assert.match(script, /Start-Process/);
  assert.match(script, /MainWindowHandle/);
  assert.match(script, /CopyFromScreen/);
  assert.doesNotMatch(script, /dotnet test .*DevSpacesScreenshot/);
});
