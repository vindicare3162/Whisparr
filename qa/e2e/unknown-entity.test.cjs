#!/usr/bin/env node
/*
 * Regression test for issue #45: an unknown performer or studio id must show
 * the not-found page instead of silently redirecting to the Scenes index.
 *
 *   npm i -g playwright && npx playwright install chromium   # once
 *   BASE=http://localhost:6969 node qa/e2e/unknown-entity.test.cjs
 *   UI_DIR=_output/UI ...   overlays a locally built bundle on the live backend
 */
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const BASE = (process.env.BASE || 'http://localhost:6969').replace(/\/$/, '');
const UI_DIR = process.env.UI_DIR ? path.resolve(process.env.UI_DIR) : null;
const PAGES = [
  ['/performer/00000000-0000-0000-0000-000000000000', /performer cannot be found/i],
  ['/studio/00000000-0000-0000-0000-000000000000', /studio cannot be found/i],
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
    // Give the app time to load its lists and, on the old build, to redirect.
    await page.waitForFunction(() => document.querySelector('img[src*="404.png"]') || document.querySelector('[class*="SceneIndex"]'), null, { timeout: 30000 }).catch(() => {});
    await page.waitForTimeout(1500);

    const state = await page.evaluate(() => ({
      pathname: location.pathname,
      notFound: !!document.querySelector('img[src*="404.png"]'),
      text: (document.querySelector('[class*="NotFound-message"]') || document.body).innerText.replace(/\s+/g, ' ').trim().slice(0, 120),
    }));

    const failures = [];

    if (state.pathname !== route) {
      failures.push(`redirected to ${state.pathname}`);
    }

    if (!state.notFound || !expected.test(state.text)) {
      failures.push('not-found page with message not shown');
    }

    if (failures.length) {
      failed += 1;
    }

    console.log(`${failures.length ? 'FAIL' : 'ok'}  ${route}  at=${state.pathname} notFound=${state.notFound} text="${state.text.slice(0, 60)}"`);

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
