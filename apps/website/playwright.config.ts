import { defineConfig } from '@playwright/test';

// Issue #146, QA Part 6: the only piece of the QA procedure the API-level Gherkin acceptance
// suite (WebApplicationFactory<Program>) cannot reach — the Facilitator cockpit page and the
// Domain Expert respond page are browser UI, so "copy the share link from the cockpit" and
// "that link only ever behaves as its own role" need a real browser. The Modeller.Api backend
// itself is mocked via page.route() (RoleScopedSessionCredentials.spec.ts) so this stays
// deterministic and doesn't require a running .NET process — the server-side enforcement those
// mocked responses stand in for is already exercised end-to-end by
// tests/Modeller.Api.Acceptance/Features/RoleScopedSessionCredentials.feature.
export default defineConfig({
  testDir: './tests/e2e',
  timeout: 30_000,
  reporter: process.env.CI ? 'html' : 'list',
  retries: process.env.CI ? 2 : 0,
  use: {
    baseURL: 'http://localhost:3200',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:3200',
    reuseExistingServer: true,
    timeout: 60_000,
  },
});
