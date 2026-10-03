#!/usr/bin/env node
/*
 * Fails when the frontend uses a translate('Key') literal that has no entry in
 * src/NzbDrone.Core/Localization/Core/en.json (the key itself would be shown
 * to the user). Regression check for issue #43.
 *
 *   node qa/check-translation-keys.cjs
 */
const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '..');
const SRC = path.join(ROOT, 'frontend', 'src');
const EN = path.join(ROOT, 'src', 'NzbDrone.Core', 'Localization', 'Core', 'en.json');

const en = JSON.parse(fs.readFileSync(EN, 'utf8'));
const used = new Map();

function walk(dir) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const file = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      walk(file);
    } else if (/\.(js|jsx|ts|tsx)$/.test(entry.name)) {
      const source = fs.readFileSync(file, 'utf8');
      const pattern = /translate\(\s*['"]([A-Za-z0-9_]+)['"]/g;
      let match;

      while ((match = pattern.exec(source))) {
        if (!used.has(match[1])) {
          used.set(match[1], new Set());
        }

        used.get(match[1]).add(path.relative(SRC, file).split(path.sep).join('/'));
      }
    }
  }
}

walk(SRC);

const missing = [...used.keys()].filter((key) => !(key in en)).sort();

console.log(`translate() keys used: ${used.size}, missing from en.json: ${missing.length}`);

for (const key of missing) {
  console.log(`  ${key} <- ${[...used.get(key)].slice(0, 3).join(', ')}`);
}

process.exit(missing.length ? 1 : 0);
