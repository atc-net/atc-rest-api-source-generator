import type { Page, Locator } from '@playwright/test';
import type { AppType } from '../fixtures/test';

export class NavigationPage {
  readonly page: Page;
  readonly appType: AppType;

  constructor(page: Page, appType: AppType) {
    this.page = page;
    this.appType = appType;
  }

  async clickNavItem(label: string) {
    if (this.appType === 'blazor') {
      await this.page.getByRole('navigation').getByRole('link', { name: label }).click();
    } else {
      await this.page.getByRole('navigation').getByRole('button', { name: label }).click();
    }
  }

  async isNavItemVisible(label: string): Promise<Locator> {
    if (this.appType === 'blazor') {
      return this.page.getByRole('navigation').getByRole('link', { name: label });
    }
    return this.page.getByRole('navigation').getByRole('button', { name: label });
  }
}
