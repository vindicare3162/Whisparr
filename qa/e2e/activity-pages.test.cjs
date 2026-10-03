#!/usr/bin/env node
/*
 * Regression test for Activity > Queue / History / Blocklist (issue #42).
 *
 * Before the fix: Queue and History render only the toolbar (the table is
 * gated on the full movie catalog, which is never fetched any more) and
 * Blocklist crashes to the error boundary with
 * "TypeError: Cannot read properties of undefined (reading '<movieId>')".
 * After the fix: each page renders its table rows (with a movie title link)
 * or the "nothing here" alert, with no page errors.
 *
 * Runs against a live instance; see qa/e2e/README.md.
 *
 *   BASE=http://localhost:6969 node qa/e2e/activity-pages.test.cjs
 *   UI_DIR=_output/UI ... overlays a locally built bundle on the live backend.
 */
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const BASE = (process.env.BASE || 'http://localhost:6969').replace(/\/$/, '');
const UI_DIR = process.env.UI_DIR ? path.resolve(process.env.UI_DIR) : null;
const PAGES = ['/activity/queue', '/activity/history', '/activity/blocklist'];

// With UI_DIR set, the live server's index.html is rewritten to load the local
// build's entry bundle, and every asset that exists in UI_DIR is served from
// disk. The webpack runtime inside the local entry bundle then requests the
// local chunk names, so a rebuilt frontend can be tested without redeploying
// the backend container.
const localEntry = UI_DIR
  ? fs.readdirSync(UI_DIR).find((f) => /^index-[0-9a-f]+\.js$/.test(f))
  : null;

function localAssetFor(url) {
  if (!UI_DIR) {
    return null;
  }

  const pathname = decodeURIComponent(new URL(url).pathname);
  const file = path.join(UI_DIR, pathname);

  return fs.existsSync(file) && fs.statSync(file).isFile() ? file : null;
}

async function overlayRoute(route) {
  const request = route.request();

  if (request.resourceType() === 'document' && request.url().startsWith(BASE)) {
    const response = await route.fetch();
    const html = (await response.text()).replace(/index-[0-9a-f]+\.js/g, localEntry);

    return route.fulfill({ response, body: html, headers: { ...response.headers(), 'content-length': String(Buffer.byteLength(html)) } });
  }

  const file = localAssetFor(request.url());

  return file ? route.fulfill({ path: file }) : route.continue();
}

async function checkPage(browser, route) {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();
  const pageErrors = [];
  const consoleErrors = [];

  page.on('pageerror', (e) => pageErrors.push(String(e.message || e).split('\n')[0]));
  page.on('console', (m) => {
    if (m.type() === 'error' && /TypeError|ReferenceError/.test(m.text())) {
      consoleErrors.push(m.text().split('\n')[0]);
    }
  });

  if (UI_DIR) {
    if (!localEntry) {
      throw new Error(`UI_DIR=${UI_DIR} has no index-<hash>.js; run "yarn build --env production" first`);
    }

    await page.route('**/*', overlayRoute);
  }

  await page.goto(BASE + route, { waitUntil: 'load', timeout: 60000 });

  // Wait until the page has either rendered rows, an informational alert, or crashed.
  await page
    .waitForFunction(
      () =>
        document.querySelector('tbody tr') ||
        document.querySelector('[class*="Alert-alert"]') ||
        document.querySelector('[class*="ErrorBoundaryError"]'),
      null,
      { timeout: 20000 }
    )
    .catch(() => {});

  const state = await page.evaluate(() => ({
    rows: document.querySelectorAll('tbody tr').length,
    titleLinks: document.querySelectorAll('tbody tr a[href*="/movie/"]').length,
    alerts: Array.from(document.querySelectorAll('[class*="Alert-alert"]')).map((a) => a.textContent.trim()),
    crashed: !!document.querySelector('[class*="ErrorBoundaryError"]'),
    pager: !!document.querySelector('[class*="TablePager"]'),
  }));

  await context.close();

  const failures = [];

  if (state.crashed) {
    failures.push('error boundary rendered');
  }

  if (pageErrors.length || consoleErrors.length) {
    failures.push(`page errors: ${[...pageErrors, ...consoleErrors].slice(0, 3).join(' | ')}`);
  }

  if (!state.rows && !state.alerts.length) {
    failures.push('neither table rows nor an alert rendered (page stuck blank)');
  }

  if (state.rows && !state.titleLinks) {
    failures.push('rows rendered but no movie title links (movie not resolved)');
  }

  return { route, state, failures };
}

(async () => {
  const browser = await chromium.launch();
  let failed = 0;

  for (const route of PAGES) {
    const { state, failures } = await checkPage(browser, route);
    const status = failures.length ? 'FAIL' : 'ok';

    if (failures.length) {
      failed += 1;
    }

    console.log(`${status}  ${route}  rows=${state.rows} titleLinks=${state.titleLinks} pager=${state.pager} alerts=${state.alerts.length} crashed=${state.crashed}`);

    for (const f of failures) {
      console.log(`      - ${f}`);
    }
  }

  await browser.close();
  process.exit(failed ? 1 : 0);
})().catch((e) => {
  console.error(e);
  process.exit(1);
});
