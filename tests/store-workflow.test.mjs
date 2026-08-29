import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const workflowUrl = new URL('../.github/workflows/store-msix.yml', import.meta.url);

test('Store workflow contains required build and submission contracts', async () => {
  const workflow = await readFile(workflowUrl, 'utf8');
  for (const expected of [
    'workflow_dispatch', 'v*-store', 'pull_request', 'win-x64', 'win-arm64',
    'STORE_PACKAGE_NAME', 'STORE_PUBLISHER', 'STORE_PUBLISHER_DISPLAY_NAME',
    'STORE_TENANT_ID', 'STORE_CLIENT_ID', 'STORE_CLIENT_SECRET', 'STORE_APPLICATION_ID',
    'node --test tests/store-submission.test.mjs',
    'node --test tests/store-msix-script.test.mjs',
    'node --test tests/store-assets.test.mjs',
    'node --test tests/store-workflow.test.mjs',
    'dotnet publish src/DevBoard.csproj', 'tests/DevBoard.Tests/DevBoard.Tests.csproj',
    'actions/upload-artifact@v4', 'actions/download-artifact@v4',
  ]) assert.ok(workflow.includes(expected), `missing ${expected}`);
  assert.equal(workflow.includes('src/SourceGit.csproj'), false);
  assert.equal(workflow.includes('tests/SourceGit.Tests'), false);
  assert.match(workflow, /github\.ref_type == 'tag'/);
  assert.match(workflow, /endsWith\(github\.ref_name, '-store'\)/);
  assert.match(workflow, /windows-11-arm/);
  assert.match(workflow, /DevBoard_\$\{\{ needs\.preflight\.outputs\.version \}\}_x64\.msix/);
  assert.match(workflow, /DevBoard_\$\{\{ needs\.preflight\.outputs\.version \}\}_arm64\.msix/);
});

test('Store build checkout includes the required AvaloniaEdit submodule', async () => {
  const workflow = await readFile(workflowUrl, 'utf8');
  const buildJob = workflow.split('  build-msix:')[1]?.split('  verify-store-packages:')[0] ?? '';
  assert.match(buildJob, /uses: actions\/checkout@v4\s+with:\s+submodules: (?:true|recursive)/m);
});

test('Store workflow runs unit tests once and reuses RID restore for publish', async () => {
  const workflow = await readFile(workflowUrl, 'utf8');
  const unitTestJob = workflow.split('  unit-tests:')[1]?.split('  build-msix:')[0] ?? '';
  const buildJob = workflow.split('  build-msix:')[1]?.split('  verify-store-packages:')[0] ?? '';

  assert.match(unitTestJob, /dotnet test tests\/DevBoard\.Tests\/DevBoard\.Tests\.csproj --configuration Release/);
  assert.match(buildJob, /needs: \[preflight, unit-tests\]/);
  assert.doesNotMatch(buildJob, /dotnet test /);
  assert.match(buildJob, /dotnet publish src\/DevBoard\.csproj .*--no-restore/);
});

test('Store unit tests expose hangs quickly without rebuilding during test execution', async () => {
  const workflow = await readFile(workflowUrl, 'utf8');
  const unitTestJob = workflow.split('  unit-tests:')[1]?.split('  build-msix:')[0] ?? '';

  assert.match(unitTestJob, /timeout-minutes: 10/);
  assert.match(unitTestJob, /dotnet restore tests\/DevBoard\.Tests\/DevBoard\.Tests\.csproj/);
  assert.match(unitTestJob, /dotnet build tests\/DevBoard\.Tests\/DevBoard\.Tests\.csproj --configuration Release --no-restore/);
  assert.match(unitTestJob, /dotnet test tests\/DevBoard\.Tests\/DevBoard\.Tests\.csproj --configuration Release --no-build --no-restore --blame-hang --blame-hang-timeout 2m --logger "console;verbosity=normal"/);
});

test('Store workflow cancels superseded runs and caps long jobs', async () => {
  const workflow = await readFile(workflowUrl, 'utf8');
  assert.match(workflow, /concurrency:\s+group: store-msix-/m);
  assert.match(workflow, /cancel-in-progress: true/);
  const unitTestJob = workflow.split('  unit-tests:')[1]?.split('  build-msix:')[0] ?? '';
  const buildJob = workflow.split('  build-msix:')[1]?.split('  verify-store-packages:')[0] ?? '';
  assert.match(unitTestJob, /timeout-minutes: 10/);
  assert.match(buildJob, /timeout-minutes: 30/);
});
