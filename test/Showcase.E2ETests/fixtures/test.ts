import { test as base } from '@playwright/test';

export type AppType = 'blazor' | 'react';
export type AppVariant = 'fetch' | 'axios' | undefined;

type CustomFixtures = {
  appType: AppType;
  appVariant: AppVariant;
};

export const test = base.extend<CustomFixtures>({
  appType: async ({}, use, testInfo) => {
    const appType = (testInfo.project.metadata?.appType as AppType) ?? 'react';
    await use(appType);
  },
  appVariant: async ({}, use, testInfo) => {
    const appVariant = testInfo.project.metadata?.appVariant as AppVariant;
    await use(appVariant);
  },
});

export { expect } from '@playwright/test';
