#!/usr/bin/env node
/*
 * Regression test for issue #57: the studio detail page must list the
 * studio's scenes.
 *
 * Picks the first studio with scenes from the API, opens its page, expands
 * every year group and checks that scene rows (links to /movie/...) render.
 *
 *   npm i -g playwright && npx playwright install chromium   # once
 *   BASE=http://localhost:6969 API_KEY=<key> node qa/e2e/studio-details.test.cjs
 *   UI_DIR=_output/UI ...   overlays a locally built bundle on the live backend
 *
 * playwright is resolved through Node's normal lookup (a scratch folder can be
 * exposed with NODE_PATH=<folder>/node_modules).
 */
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const BASE = (process.env.BASE || 'http://localhost:6969').replace(/\/$/, '');
const API_KEY = process.env.API_KEY;
const UI_DIR = process.env.UI_DIR ? path.resolve(process.env.UI_DIR) : null;

if (!API_KEY) {
  console.error('API_KEY is required (Settings > General > API Key) to look up a studio with scenes');
  process.exit(2);
}

// With UI_DIR set, the live server's index.html is rewritten to load the local
// build's entry bundle and every asset that exists in UI_DIR is served from
// disk, so a rebuilt frontend can be tested without redeploying the backend.
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

async function findStudioWithScenes() {
  const response = await fetch(`${BASE}/api/v3/studio`, { headers: { 'X-Api-Key': API_KEY } });

  if (!response.ok) {
    throw new Error(`GET /api/v3/studio failed: ${response.status}`);
  }

  const studios = await response.json();
  // totalSceneCount counts catalogued scenes; sceneCount only counts ones with a file on disk.
  const studio = studios.filter((s) => s.totalSceneCount > 0).sort((a, b) => a.totalSceneCount - b.totalSceneCount)[0];

  if (!studio) {
    throw new Error('no studio with scenes on this instance');
  }

  return studio;
}

(async () => {
  const studio = await findStudioWithScenes();
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  const pageErrors = [];

  page.on('pageerror', (e) => pageErrors.push(String(e.message || e).split('\n')[0]));

  if (UI_DIR) {
    if (!localEntry) {
      throw new Error(`UI_DIR=${UI_DIR} has no index-<hash>.js; run "yarn build --env production" first`);
    }

    await page.route('**/*', overlayRoute);
  }

  await page.goto(`${BASE}/studio/${studio.foreignId}`, { waitUntil: 'load', timeout: 60000 });
  await page.waitForSelector('a[title="Expand All"], button[title="Expand All"], a[title="Collapse All"], button[title="Collapse All"]', { timeout: 30000 });

  // The year groups appear once the studio's scenes have been fetched; expanding
  // before that would toggle an empty list.
  // Report a plain failure (no year groups) instead of throwing when they never appear.
  await page.waitForSelector('[class*="StudioDetailsYear-"]', { state: 'attached', timeout: 30000 }).catch(() => {});

  const expandAll = page.locator('a[title="Expand All"], button[title="Expand All"]');

  if (await expandAll.count()) {
    await expandAll.first().click();
  }

  await page.waitForFunction(() => document.querySelectorAll('a[href*="/movie/"]').length > 0, null, { timeout: 20000 }).catch(() => {});

  const state = await page.evaluate(() => ({
    yearGroups: document.querySelectorAll('[class*="StudioDetailsYear-"]').length,
    sceneLinks: document.querySelectorAll('a[href*="/movie/"]').length,
  }));

  await browser.close();

  const failures = [];

  if (!state.yearGroups) {
    failures.push('no year groups rendered');
  } else if (!state.sceneLinks) {
    failures.push('no scene rows rendered after Expand All');
  }

  if (pageErrors.length) {
    failures.push(`page errors: ${pageErrors.slice(0, 3).join(' | ')}`);
  }

  console.log(`${failures.length ? 'FAIL' : 'ok'}  /studio/${studio.foreignId}  "${studio.title}" totalSceneCount=${studio.totalSceneCount} yearGroups=${state.yearGroups} sceneLinks=${state.sceneLinks}`);

  for (const f of failures) {
    console.log(`      - ${f}`);
  }

  process.exit(failures.length ? 1 : 0);
})().catch((e) => {
  console.error(e);
  process.exit(1);
});
