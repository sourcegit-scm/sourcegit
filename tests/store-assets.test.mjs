import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const assets = [
  ['Square44x44Logo.png', 44, 44],
  ['Square150x150Logo.png', 150, 150],
  ['StoreLogo.png', 50, 50],
];

for (const [name, width, height] of assets) {
  test(`${name} is a valid ${width}x${height} PNG`, async () => {
    const bytes = await readFile(new URL(`../store/assets/${name}`, import.meta.url));
    assert.deepEqual([...bytes.subarray(0, 8)], [0x89,0x50,0x4e,0x47,0x0d,0x0a,0x1a,0x0a]);
    assert.equal(bytes.readUInt32BE(16), width);
    assert.equal(bytes.readUInt32BE(20), height);
  });
}
