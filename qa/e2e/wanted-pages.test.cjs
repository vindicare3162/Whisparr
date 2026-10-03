#!/usr/bin/env node
/*
 * Regression test for issue #40: Wanted > Missing and Wanted > Cutoff Unmet
 * must render their table (or the "no items" alert) without crashing to the
 * error boundary.
 *
 *   npm i -g playwright && npx playwright install chromium   # once
 *   BASE=http://localhost:6969 node qa/e2e/wanted-pages.test.cjs
 *   UI_DIR=_output/UI ...   overlays a locally built bundle on the live backend
 */
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const BASE = (process.env.BASE || 'http://localhost:6969').replace(/\/$/, '');
const UI_DIR = process.env.UI_DIR ? path.resolve(process.env.UI_DIR) : null;
const PAGES = ['/wanted/missing', '/wanted/cutoffunmet'];

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

async function checkPage(browser, route) {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();
  const pageErrors = [];

  page.on('pageerror', (e) => pageErrors.push(String(e.message || e).split('\n')[0]));
  page.on('console', (m) => {
    if (m.type() === 'error' && /TypeError|ReferenceError/.test(m.text())) {
      pageErrors.push(m.text().split('\n')[0]);
    }
  });

  if (UI_DIR) {
    if (!localEntry) {
      throw new Error(`UI_DIR=${UI_DIR} has no index-<hash>.js; run "yarn build --env production" first`);
    }

    await page.route('**/*', overlayRoute);
  }

  await page.goto(BASE + route, { waitUntil: 'load', timeout: 60000 });
  await page
    .waitForFunction(
      () =>
        document.querySelector('tbody tr') ||
        document.querySelector('[class*="Alert-alert"]') ||
        document.querySelector('[class*="ErrorBoundaryError"]'),
      null,
      { timeout: 30000 }
    )
    .catch(() => {});

  const state = await page.evaluate(() => ({
    rows: document.querySelectorAll('tbody tr').length,
    titleLinks: document.querySelectorAll('tbody tr a[href*="/movie/"]').length,
    statusCells: document.querySelectorAll('tbody tr [class*="MovieStatus"], tbody tr [class*="status"]').length,
    alerts: Array.from(document.querySelectorAll('[class*="Alert-alert"]')).map((a) => a.textContent.trim()),
    crashed: !!document.querySelector('[class*="ErrorBoundaryError"]'),
  }));

  await context.close();

  const failures = [];

  if (state.crashed) {
    failures.push('error boundary rendered');
  }

  if (pageErrors.length) {
    failures.push(`page errors: ${[...new Set(pageErrors)].slice(0, 2).join(' | ')}`);
  }

  if (!state.rows && !state.alerts.length) {
    failures.push('neither table rows nor an alert rendered');
  }

  if (state.rows && !state.titleLinks) {
    failures.push('rows rendered but no movie title links');
  }

  return { state, failures };
}

(async () => {
  const browser = await chromium.launch();
  let failed = 0;

  for (const route of PAGES) {
    const { state, failures } = await checkPage(browser, route);

    if (failures.length) {
      failed += 1;
    }

    console.log(`${failures.length ? 'FAIL' : 'ok'}  ${route}  rows=${state.rows} titleLinks=${state.titleLinks} alerts=${state.alerts.length} crashed=${state.crashed}`);

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
