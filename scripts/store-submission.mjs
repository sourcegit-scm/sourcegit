const EDITABLE_SUBMISSION_FIELDS = [
  'applicationCategory','pricing','visibility','targetPublishMode','targetPublishDate','listings',
  'hardwarePreferences','automaticBackupEnabled','canInstallOnRemovableMedia','isGameDvrEnabled',
  'gamingOptions','hasExternalInAppProducts','meetAccessibilityGuidelines','notesForCertification',
  'packageDeliveryOptions','enterpriseLicensing','allowMicrosoftDecideAppAvailabilityToFutureDeviceFamilies',
  'allowTargetFutureDeviceFamilies','trailers',
];

const FAILURE_STATUSES = new Set(['CommitFailed','PreProcessingFailed','CertificationFailed','PublishFailed','ReleaseFailed','Canceled']);
const ACCEPTED_STATUSES = new Set(['PreProcessing','Certification','PendingPublication','Publishing','Published','Release']);

export function storeVersionFromTag(tag) {
  const match = /^v(\d+\.\d+\.\d+)-store$/.exec(tag ?? '');
  if (!match) throw new Error(`Store tag '${tag ?? ''}' must match vX.Y.Z-store`);
  return match[1];
}

export function selectStorePackages(fileNames, version) {
  const expected = [`DevBoard_${version}_x64.msix`, `DevBoard_${version}_arm64.msix`];
  const files = new Set(fileNames);
  if (!expected.every((name) => files.has(name))) throw new Error(`Expected Store MSIX artifacts: ${expected.join(', ')}`);
  return expected;
}

export function buildSubmissionUpdate(createdSubmission, packageNames) {
  const update = {};
  for (const field of EDITABLE_SUBMISSION_FIELDS) {
    if (Object.prototype.hasOwnProperty.call(createdSubmission, field)) update[field] = createdSubmission[field];
  }
  const existingPackages = (createdSubmission.applicationPackages ?? []).map((pkg) => ({
    fileName: pkg.fileName,
    fileStatus: 'PendingDelete',
    minimumDirectXVersion: pkg.minimumDirectXVersion ?? 'None',
    minimumSystemRam: pkg.minimumSystemRam ?? 'None',
  }));
  const newPackages = packageNames.map((fileName) => ({
    fileName,
    fileStatus: 'PendingUpload',
    minimumDirectXVersion: 'None',
    minimumSystemRam: 'None',
  }));
  update.applicationPackages = [...existingPackages, ...newPackages];
  return update;
}

async function readJson(response) {
  const text = await response.text();
  const body = text ? JSON.parse(text) : {};
  if (!response.ok) throw new Error(`HTTP ${response.status}: ${text || response.statusText}`);
  return body;
}

export async function runStoreSubmission({
  tenantId, clientId, clientSecret, applicationId, packageNames, zipBytes,
  fetchImpl = fetch,
  sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms)),
  maxPollAttempts = 40,
  pollIntervalMs = 15000,
}) {
  const tokenResponse = await fetchImpl(`https://login.microsoftonline.com/${encodeURIComponent(tenantId)}/oauth2/token`, {
    method: 'POST',
    headers: { 'content-type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'client_credentials', client_id: clientId, client_secret: clientSecret,
      resource: 'https://manage.devcenter.microsoft.com',
    }),
  });
  const tokenBody = await readJson(tokenResponse);
  if (!tokenBody.access_token) throw new Error('Microsoft Entra token response did not contain access_token');

  const apiBase = `https://manage.devcenter.microsoft.com/v1.0/my/applications/${encodeURIComponent(applicationId)}/submissions`;
  const authHeaders = { authorization: `Bearer ${tokenBody.access_token}`, 'content-type': 'application/json' };
  const created = await readJson(await fetchImpl(apiBase, { method: 'POST', headers: authHeaders }));
  if (!created.id || !created.fileUploadUrl) throw new Error('Store create-submission response is missing id or fileUploadUrl');

  const submissionUrl = `${apiBase}/${encodeURIComponent(created.id)}`;
  await readJson(await fetchImpl(submissionUrl, {
    method: 'PUT', headers: authHeaders, body: JSON.stringify(buildSubmissionUpdate(created, packageNames)),
  }));
  await readJson(await fetchImpl(created.fileUploadUrl, {
    method: 'PUT', headers: { 'x-ms-blob-type': 'BlockBlob', 'content-type': 'application/zip' }, body: zipBytes,
  }));
  await readJson(await fetchImpl(`${submissionUrl}/commit`, {
    method: 'POST', headers: { authorization: `Bearer ${tokenBody.access_token}` },
  }));

  for (let attempt = 0; attempt < maxPollAttempts; attempt += 1) {
    const statusBody = await readJson(await fetchImpl(`${submissionUrl}/status`, {
      method: 'GET', headers: { authorization: `Bearer ${tokenBody.access_token}` },
    }));
    const status = statusBody.status;
    if (FAILURE_STATUSES.has(status)) {
      throw new Error(`Microsoft Store submission ${created.id} failed with status ${status}: ${JSON.stringify(statusBody.statusDetails ?? {})}`);
    }
    if (ACCEPTED_STATUSES.has(status)) return { submissionId: created.id, status };
    if (attempt < maxPollAttempts - 1) await sleep(pollIntervalMs);
  }
  throw new Error(`Microsoft Store submission ${created.id} did not leave CommitStarted/PendingCommit within the polling window`);
}

async function main() {
  const requiredNames = ['STORE_TENANT_ID','STORE_CLIENT_ID','STORE_CLIENT_SECRET','STORE_APPLICATION_ID','STORE_TAG'];
  const missing = requiredNames.filter((name) => !process.env[name]?.trim());
  if (missing.length) throw new Error(`Missing required Store environment values: ${missing.join(', ')}`);

  const { readFile, readdir } = await import('node:fs/promises');
  const packageDir = process.env.STORE_PACKAGE_DIR || 'store-packages';
  const zipPath = process.env.STORE_UPLOAD_ZIP || 'store-upload.zip';
  const version = storeVersionFromTag(process.env.STORE_TAG);
  const packageNames = selectStorePackages(await readdir(packageDir), version);
  const zipBytes = await readFile(zipPath);
  const result = await runStoreSubmission({
    tenantId: process.env.STORE_TENANT_ID,
    clientId: process.env.STORE_CLIENT_ID,
    clientSecret: process.env.STORE_CLIENT_SECRET,
    applicationId: process.env.STORE_APPLICATION_ID,
    packageNames,
    zipBytes,
  });
  console.log(`Microsoft Store accepted submission ${result.submissionId}; current status: ${result.status}`);
}

const { pathToFileURL } = await import('node:url');
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => {
    console.error(error instanceof Error ? error.message : error);
    process.exitCode = 1;
  });
}
