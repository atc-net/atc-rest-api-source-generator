import { defineConfig, devices } from '@playwright/test';

const API_PORT = 15046;
const BLAZOR_PORT = 5048;
const REACT_FETCH_PORT = 5173;
const REACT_AXIOS_PORT = 5174;

export default defineConfig({
  testDir: './specs',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: [['html', { open: 'never' }]],

  use: {
    trace: 'on-first-retry',
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
      reuseExistingServer: true,
      timeout: 60_000,
    },
    {
      command: 'dotnet run --project ../../sample/Showcase/Showcase.BlazorApp',
      port: BLAZOR_PORT,
      reuseExistingServer: true,
      timeout: 120_000,
    },
    {
      command: 'npm run dev --prefix ../../sample/Showcase/Showcase.ReactApp.Fetch',
      port: REACT_FETCH_PORT,
      reuseExistingServer: true,
      timeout: 30_000,
      env: { PORT: String(REACT_FETCH_PORT) },
    },
    {
      command: 'npm run dev --prefix ../../sample/Showcase/Showcase.ReactApp.Axios',
      port: REACT_AXIOS_PORT,
      reuseExistingServer: true,
      timeout: 30_000,
      env: { PORT: String(REACT_AXIOS_PORT) },
    },
  ],
});
