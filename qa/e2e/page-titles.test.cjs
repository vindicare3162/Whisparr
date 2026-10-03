#!/usr/bin/env node
/*
 * Regression test for issue #44: every page sets a document title of the form
 * "<Page> - <instance name>", and the Add New Performer / Studio pages do not
 * reuse the "Add New Scene" title.
 *
 *   npm i -g playwright && npx playwright install chromium   # once
 *   BASE=http://localhost:6969 node qa/e2e/page-titles.test.cjs
 *   UI_DIR=_output/UI ...   overlays a locally built bundle on the live backend
 */
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const BASE = (process.env.BASE || 'http://localhost:6969').replace(/\/$/, '');
const UI_DIR = process.env.UI_DIR ? path.resolve(process.env.UI_DIR) : null;
const PAGES = [
  ['/', /^Scenes - /],
  ['/scenes', /^Scenes - /],
  ['/movies', /^Movies - /],
  ['/performers', /^Performers - /],
  ['/studios', /^Studios - /],
  ['/add/new/performer', /^Add New Performer - /],
  ['/add/new/studio', /^Add New Studio - /],
  ['/add/new/scene', /^Add New Scene - /],
];

const localEntry = UI_DIR
  ? fs.readdirSync(UI_DIR).find((f) => /^index-[0-9a-f]+\.js$/.test(f))
  : null;

async function overlayRoute(route) {
  const request = route.request();

  if (request.resourceType() === 'document' && request.url().startsWith(BASE)) {
    const response = await route.fetch();
    const html = (await response.text()).replace(/index-[0-9a-f]+\.js/g, localEntry);

    return route.fulfill({ response, body: html, headers: { ...response.headers(), 'content-length': String(Buffer.byteLength(html)) } });
  }

  const pathname = decodeURIComponent(new URL(request.url()).pathname);
  const file = path.join(UI_DIR, pathname);

  return fs.existsSync(file) && fs.statSync(file).isFile() ? route.fulfill({ path: file }) : route.continue();
}

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });

  if (UI_DIR) {
    if (!localEntry) {
      throw new Error(`UI_DIR=${UI_DIR} has no index-<hash>.js; run "yarn build --env production" first`);
    }

    await page.route('**/*', overlayRoute);
  }

  let failed = 0;

  for (const [route, expected] of PAGES) {
    await page.goto(BASE + route, { waitUntil: 'load', timeout: 60000 });
    await page.waitForFunction((re) => new RegExp(re).test(document.title), expected.source, { timeout: 10000 }).catch(() => {});

    const title = await page.title();
    const ok = expected.test(title);

    if (!ok) {
      failed += 1;
    }

    console.log(`${ok ? 'ok' : 'FAIL'}  ${route}  title="${title}" expected ${expected}`);
  }

  await browser.close();
  process.exit(failed ? 1 : 0);
})().catch((e) => {
  console.error(e);
  process.exit(1);
});
