#!/usr/bin/env node
/*
 * Regression test for issue #46: images must have alt text and icon-only
 * links/buttons must have an accessible name.
 *
 * Runs axe-core (rules image-alt, link-name, button-name) on the pages where
 * the automated QA pass found violations and fails if any remain.
 *
 *   npm i -g playwright axe-core && npx playwright install chromium   # once
 *   BASE=http://localhost:6969 node qa/e2e/a11y-names.test.cjs
 *   UI_DIR=_output/UI ...   overlays a locally built bundle on the live backend
 *
 * playwright and axe-core are resolved through Node's normal lookup (a scratch
 * folder can be exposed with NODE_PATH=<folder>/node_modules).
 */
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const BASE = (process.env.BASE || 'http://localhost:6969').replace(/\/$/, '');
const UI_DIR = process.env.UI_DIR ? path.resolve(process.env.UI_DIR) : null;
const AXE = fs.readFileSync(require.resolve('axe-core/axe.min.js'), 'utf8');
const RULES = ['image-alt', 'link-name', 'button-name'];
const PAGES = [
  '/',
  '/performers',
  '/studios',
  '/calendar',
  '/system/status',
  '/system/events',
  '/add/new/scene',
  '/settings/indexers',
  '/settings/downloadclients',
  '/settings/importlists',
  '/settings/connect',
  '/settings/customformats',
  '/settings/profiles',
  '/settings/metadata',
  '/settings/tags',
  '/this-route-does-not-exist',
];

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

async function checkPage(browser, route) {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();

  if (UI_DIR) {
    if (!localEntry) {
      throw new Error(`UI_DIR=${UI_DIR} has no index-<hash>.js; run "yarn build --env production" first`);
    }

    await page.route('**/*', overlayRoute);
  }

  await page.goto(BASE + route, { waitUntil: 'load', timeout: 60000 });
  await page.waitForTimeout(4000);
  await page.addScriptTag({ content: AXE });

  const violations = await page.evaluate(async (rules) => {
    const result = await window.axe.run(document, { runOnly: { type: 'rule', values: rules }, resultTypes: ['violations'] });

    return result.violations.map((v) => ({ id: v.id, nodes: v.nodes.length, sample: v.nodes[0] && v.nodes[0].target.join(' ') }));
  }, RULES);

  await context.close();

  return violations;
}

(async () => {
  const browser = await chromium.launch();
  let failed = 0;

  for (const route of PAGES) {
    const violations = await checkPage(browser, route);
    const summary = violations.map((v) => `${v.id}(${v.nodes})`).join(' ');

    if (violations.length) {
      failed += 1;
    }

    console.log(`${violations.length ? 'FAIL' : 'ok'}  ${route}  ${summary || 'no violations'}`);

    for (const v of violations) {
      console.log(`      - ${v.id}: e.g. ${v.sample}`);
    }
  }

  await browser.close();
  process.exit(failed ? 1 : 0);
})().catch((e) => {
  console.error(e);
  process.exit(1);
});
