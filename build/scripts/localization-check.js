const fs = require('fs-extra');
const path = require('path');
const xml2js = require('xml2js');

const repoRoot = path.join(__dirname, '../../');
const localesDir = path.join(repoRoot, 'src/Resources/Locales');
const enUSFile = path.join(localesDir, 'en_US.axaml');
const outputFile = path.join(repoRoot, 'TRANSLATION.md');
const readmeFile = path.join(repoRoot, 'README.md');

const parser = new xml2js.Parser();

async function parseXml(filePath) {
    const data = await fs.readFile(filePath);
    return parser.parseStringPromise(data);
}

async function calculateTranslationRate() {
    const enUSData = await parseXml(enUSFile);
    const enUSKeys = new Set(enUSData.ResourceDictionary['x:String'].map(item => item.$['x:Key']));

    const translationRates = [];
    const badges = [];

    const files = (await fs.readdir(localesDir)).filter(file => file !== 'en_US.axaml' && file.endsWith('.axaml'));

    // Add en_US badge first
    badges.push(`[![en_US](https://img.shields.io/badge/en__US-100%25-brightgreen)](TRANSLATION.md)`);

    for (const file of files) {
        const filePath = path.join(localesDir, file);
        const localeData = await parseXml(filePath);
        const localeKeys = new Set(localeData.ResourceDictionary['x:String'].map(item => item.$['x:Key']));

        const missingKeys = [...enUSKeys].filter(key => !localeKeys.has(key));
        const translationRate = ((enUSKeys.size - missingKeys.length) / enUSKeys.size) * 100;

        translationRates.push(`### ${file}: ${translationRate.toFixed(2)}%\n`);
        translationRates.push(`<details>\n<summary>Missing Keys</summary>\n\n${missingKeys.map(key => `- ${key}`).join('\n')}\n\n</details>`);

        // Add badges
        const locale = file.replace('.axaml', '').replace('_', '__');
        const badgeColor = translationRate === 100 ? 'brightgreen' : translationRate >= 75 ? 'yellow' : 'red';
        badges.push(`[![${locale}](https://img.shields.io/badge/${locale}-${translationRate.toFixed(2)}%25-${badgeColor})](TRANSLATION.md)`);
    }

    console.log(translationRates.join('\n\n'));

    await fs.writeFile(outputFile, translationRates.join('\n\n') + '\n', 'utf8');

    // Update README.md
    let readmeContent = await fs.readFile(readmeFile, 'utf8');
    const badgeSection = `## Translation Status\n\n${badges.join(' ')}`;
    console.log(badgeSection);
    readmeContent = readmeContent.replace(/## Translation Status\n\n.*\n\n/, badgeSection + '\n\n');
    await fs.writeFile(readmeFile, readmeContent, 'utf8');
}

calculateTranslationRate().catch(err => console.error(err));
