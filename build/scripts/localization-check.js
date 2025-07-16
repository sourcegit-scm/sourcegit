const fs = require('fs-extra');
const path = require('path');
const xml2js = require('xml2js');

const repoRoot = path.join(__dirname, '../../');
const localesDir = path.join(repoRoot, 'src/Resources/Locales');
const enUSFile = path.join(localesDir, 'en_US.axaml');
const outputFile = path.join(repoRoot, 'TRANSLATION.md');

const parser = new xml2js.Parser();

async function parseXml(filePath) {
    const data = await fs.readFile(filePath);
    return parser.parseStringPromise(data);
}

async function filterAndSortTranslations(localeData, enUSKeys, enUSData) {
    const strings = localeData.ResourceDictionary['x:String'];
    // Remove keys that don't exist in English file
    const filtered = strings.filter(item => enUSKeys.has(item.$['x:Key']));

    // Sort based on the key order in English file
    const enUSKeysArray = enUSData.ResourceDictionary['x:String'].map(item => item.$['x:Key']);
    filtered.sort((a, b) => {
        const aIndex = enUSKeysArray.indexOf(a.$['x:Key']);
        const bIndex = enUSKeysArray.indexOf(b.$['x:Key']);
        return aIndex - bIndex;
    });

    return filtered;
}

async function calculateTranslationRate() {
    const enUSData = await parseXml(enUSFile);
    const enUSKeys = new Set(enUSData.ResourceDictionary['x:String'].map(item => item.$['x:Key']));
    const files = (await fs.readdir(localesDir)).filter(file => file !== 'en_US.axaml' && file.endsWith('.axaml'));

    const lines = [];

    lines.push('# Translation Status');
    lines.push('This document shows the translation status of each locale file in the repository.');
    lines.push(`## Details`);
    lines.push(`### ![en_US](https://img.shields.io/badge/en__US-%E2%88%9A-brightgreen)`);

    for (const file of files) {
        const locale = file.replace('.axaml', '').replace('_', '__');
        const filePath = path.join(localesDir, file);
        const localeData = await parseXml(filePath);
        const localeKeys = new Set(localeData.ResourceDictionary['x:String'].map(item => item.$['x:Key']));
        const missingKeys = [...enUSKeys].filter(key => !localeKeys.has(key));

        // Sort and clean up extra translations
        const sortedAndCleaned = await filterAndSortTranslations(localeData, enUSKeys, enUSData);
        localeData.ResourceDictionary['x:String'] = sortedAndCleaned;

        // Save the updated file
        const builder = new xml2js.Builder({
            headless: true,
            renderOpts: { pretty: true, indent: '  ' }
        });
        let xmlStr = builder.buildObject(localeData);

        // Add an empty line before the first x:String
        xmlStr = xmlStr.replace('  <x:String', '\n  <x:String');
        await fs.writeFile(filePath, xmlStr + '\n', 'utf8');

        if (missingKeys.length > 0) {
            const progress = ((enUSKeys.size - missingKeys.length) / enUSKeys.size) * 100;
            const badgeColor = progress >= 75 ? 'yellow' : 'red';

            lines.push(`### ![${locale}](https://img.shields.io/badge/${locale}-${progress.toFixed(2)}%25-${badgeColor})`);
            lines.push(`<details>\n<summary>Missing keys in ${file}</summary>\n\n${missingKeys.map(key => `- \`${key}\``).join('\n')}\n\n</details>`)
        } else {
            lines.push(`### ![${locale}](https://img.shields.io/badge/${locale}-%E2%88%9A-brightgreen)`);
        }
    }

    const content = lines.join('\n\n');
    console.log(content);
    await fs.writeFile(outputFile, content, 'utf8');
}

calculateTranslationRate().catch(err => console.error(err));
