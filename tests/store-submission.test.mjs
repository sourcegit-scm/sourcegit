import test from 'node:test';
import assert from 'node:assert/strict';
import {
  storeVersionFromTag,
  buildSubmissionUpdate,
  runStoreSubmission,
  selectStorePackages,
} from '../scripts/store-submission.mjs';

test('storeVersionFromTag accepts strict Store tags', () => {
  assert.equal(storeVersionFromTag('v1.2.3-store'), '1.2.3');
  assert.throws(() => storeVersionFromTag('v1.2-store'), /vX\.Y\.Z-store/);
  assert.throws(() => storeVersionFromTag('1.2.3-store'), /vX\.Y\.Z-store/);
});

test('selectStorePackages requires x64 and arm64 Dev Board packages', () => {
  assert.deepEqual(
    selectStorePackages(['notes.txt','DevBoard_1.2.3_arm64.msix','DevBoard_1.2.3_x64.msix'], '1.2.3'),
    ['DevBoard_1.2.3_x64.msix','DevBoard_1.2.3_arm64.msix'],
  );
  assert.throws(() => selectStorePackages(['DevBoard_1.2.3_x64.msix'], '1.2.3'), /arm64/);
});

test('buildSubmissionUpdate deletes copied packages and adds pending uploads', () => {
  const payload = buildSubmissionUpdate({
    applicationCategory: 'DeveloperTools',
    applicationPackages: [{ fileName: 'old.msix', fileStatus: 'Uploaded' }],
  }, ['DevBoard_1.2.3_x64.msix','DevBoard_1.2.3_arm64.msix']);
  assert.equal(payload.applicationPackages[0].fileStatus, 'PendingDelete');
  assert.equal(payload.applicationPackages[1].fileStatus, 'PendingUpload');
  assert.equal(payload.applicationPackages[2].fileStatus, 'PendingUpload');
});

test('runStoreSubmission creates, updates, uploads, commits and polls', async () => {
  const calls = [];
  const responses = [
    { access_token: 'token' },
    { id: 'submission-123', fileUploadUrl: 'https://blob.example/upload', applicationPackages: [] },
    {}, {}, {}, { status: 'Certification' },
  ];
  const fakeFetch = async (url, init = {}) => {
    calls.push({ url: String(url), method: init.method ?? 'GET' });
    return new Response(JSON.stringify(responses.shift()), { status: 200, headers: { 'content-type': 'application/json' } });
  };
  const result = await runStoreSubmission({
    tenantId: 'tenant', clientId: 'client', clientSecret: 'secret', applicationId: 'app',
    packageNames: ['DevBoard_1.2.3_x64.msix','DevBoard_1.2.3_arm64.msix'],
    zipBytes: Buffer.from('zip'), fetchImpl: fakeFetch, sleep: async () => {}, maxPollAttempts: 1,
  });
  assert.deepEqual(result, { submissionId: 'submission-123', status: 'Certification' });
  assert.deepEqual(calls.map((call) => call.method), ['POST','POST','PUT','PUT','POST','GET']);
});

test('failure status does not leak client secret', async () => {
  const secret = 'never-print-me';
  const responses = [
    { access_token: 'token' },
    { id: 'submission-123', fileUploadUrl: 'https://blob.example/upload', applicationPackages: [] },
    {}, {}, {}, { status: 'CertificationFailed', statusDetails: { errors: ['bad package'] } },
  ];
  const fakeFetch = async () => new Response(JSON.stringify(responses.shift()), { status: 200 });
  await assert.rejects(
    () => runStoreSubmission({
      tenantId: 'tenant', clientId: 'client', clientSecret: secret, applicationId: 'app',
      packageNames: ['DevBoard_1.2.3_x64.msix','DevBoard_1.2.3_arm64.msix'],
      zipBytes: Buffer.from('zip'), fetchImpl: fakeFetch, sleep: async () => {}, maxPollAttempts: 1,
    }),
    (error) => error.message.includes('CertificationFailed') && !error.message.includes(secret),
  );
});
