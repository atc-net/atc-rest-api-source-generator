import { test as base } from '@playwright/test';

export type AppType = 'blazor' | 'react';

type CustomFixtures = {
  appType: AppType;
};

export const test = base.extend<CustomFixtures>({
  appType: async ({}, use, testInfo) => {
    const appType = (testInfo.project.metadata?.appType as AppType) ?? 'react';
    await use(appType);
  },
});

export { expect } from '@playwright/test';
