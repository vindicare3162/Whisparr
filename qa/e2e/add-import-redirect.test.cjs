#!/usr/bin/env node
/*
 * Regression test for issue #52: /add/import must not render an empty page; it
 * redirects to /add/import/movies, which shows the root-folder picker.
 *
 *   npm i -g playwright && npx playwright install chromium   # once
 *   BASE=http://localhost:6969 node qa/e2e/add-import-redirect.test.cjs
 *   UI_DIR=_output/UI ...   overlays a locally built bundle on the live backend
 */
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const BASE = (process.env.BASE || 'http://localhost:6969').replace(/\/$/, '');
const UI_DIR = process.env.UI_DIR ? path.resolve(process.env.UI_DIR) : null;

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

  await page.goto(`${BASE}/add/import`, { waitUntil: 'load', timeout: 60000 });
  await page.waitForFunction(() => /\/add\/import\/movies$/.test(location.pathname), null, { timeout: 10000 }).catch(() => {});
  await page.waitForTimeout(1500);

  const state = await page.evaluate(() => ({
    pathname: location.pathname,
    bodyText: (document.querySelector('[class*="PageContentBody"]') || document.body).innerText.replace(/\s+/g, ' ').trim().slice(0, 120),
  }));

  await browser.close();

  const failures = [];

  if (!/\/add\/import\/movies$/.test(state.pathname)) {
    failures.push(`expected redirect to /add/import/movies, got ${state.pathname}`);
  }

  if (!state.bodyText) {
    failures.push('page content is empty');
  }

  console.log(`${failures.length ? 'FAIL' : 'ok'}  /add/import -> ${state.pathname}  content="${state.bodyText}"`);

  for (const f of failures) {
    console.log(`      - ${f}`);
  }

  process.exit(failures.length ? 1 : 0);
})().catch((e) => {
  console.error(e);
  process.exit(1);
});
