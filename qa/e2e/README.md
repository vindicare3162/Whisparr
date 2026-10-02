# Browser regression tests

Plain Node scripts driven by [Playwright](https://playwright.dev) that run
against a live Whisparr instance. There is no frontend unit-test runner in this
repo, so UI regressions are pinned here instead.

```sh
npm i -g playwright && npx playwright install chromium   # once
BASE=http://localhost:6969 node qa/e2e/activity-pages.test.cjs
```

`playwright` is resolved through Node's normal lookup, so it can also live in a
scratch folder exposed with `NODE_PATH=<folder>/node_modules`.

To test a rebuilt frontend without redeploying the backend container, point
`UI_DIR` at the webpack output. The script rewrites the live `index.html` to
load the local entry bundle and serves every asset that exists in `UI_DIR`
from disk, so the backend keeps serving the API while the browser runs the
local frontend:

```sh
yarn build --env production
UI_DIR=_output/UI BASE=http://localhost:6969 node qa/e2e/activity-pages.test.cjs
```

Each script exits non-zero on failure and prints one line per page.
