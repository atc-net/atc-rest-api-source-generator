import { defineConfig, devices } from '@playwright/test';
import { API_PORT, BLAZOR_PORT, REACT_AXIOS_PORT, REACT_FETCH_PORT } from './ports';

// A server already on a port is reused only when asked (PW_REUSE=1): otherwise an old build, an Aspire or an IDE run
// would be tested silently, and the run fails loudly on the taken port instead.
const reuseExistingServer = !process.env.CI && process.env.PW_REUSE === '1';

export default defineConfig({
  testDir: './specs',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  // No retries: a test that fails once is reported as failing, not hidden as passed-on-retry
  retries: 0,
  workers: process.env.CI ? 1 : undefined,
  // The JSON file feeds the Playwright tab of the test-suite report (Report_EndToEndErrorBodyMatrix)
  reporter: [['html', { open: 'never' }], ['json', { outputFile: 'test-results/results.json' }]],

  use: {
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'blazor',
      use: {
        ...devices['Desktop Chrome'],
        baseURL: `http://localhost:${BLAZOR_PORT}`,
        navigationTimeout: 30_000,
        actionTimeout: 15_000,
      },
      metadata: { appType: 'blazor' },
    },
    {
      name: 'react-fetch',
      use: {
        ...devices['Desktop Chrome'],
        baseURL: `http://localhost:${REACT_FETCH_PORT}`,
        navigationTimeout: 10_000,
        actionTimeout: 10_000,
      },
      metadata: { appType: 'react', appVariant: 'fetch' },
    },
    {
      name: 'react-axios',
      use: {
        ...devices['Desktop Chrome'],
        baseURL: `http://localhost:${REACT_AXIOS_PORT}`,
        navigationTimeout: 10_000,
        actionTimeout: 10_000,
      },
      metadata: { appType: 'react', appVariant: 'axios' },
    },
  ],

  webServer: [
    {
      command: 'dotnet run --project ../../sample/Showcase/Showcase.Api',
      port: API_PORT,
      reuseExistingServer,
      timeout: 300_000,
    },
    {
      command: 'dotnet run --project ../../sample/Showcase/Showcase.BlazorApp',
      port: BLAZOR_PORT,
      reuseExistingServer,
      timeout: 300_000,
    },
    {
      command: 'npm run dev --prefix ../../sample/Showcase/Showcase.ReactApp.Fetch',
      port: REACT_FETCH_PORT,
      reuseExistingServer,
      timeout: 120_000,
      env: { PORT: String(REACT_FETCH_PORT) },
    },
    {
      command: 'npm run dev --prefix ../../sample/Showcase/Showcase.ReactApp.Axios',
      port: REACT_AXIOS_PORT,
      reuseExistingServer,
      timeout: 120_000,
      env: { PORT: String(REACT_AXIOS_PORT) },
    },
  ],
});
