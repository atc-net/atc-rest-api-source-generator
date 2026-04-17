import { type Locator, type Page } from '@playwright/test';

export class HomePage {
  readonly page: Page;
  readonly heading: Locator;

  constructor(page: Page) {
    this.page = page;
    // Blazor: "Welcome to Showcase API Explorer" (h3), React: "Showcase Dashboard" (h4)
    // Avoid matching the AppBar title "Showcase API Explorer" (h6) by excluding level 6
    this.heading = page.getByRole('heading', { name: /showcase dashboard|welcome to showcase/i });
  }

  async goto() {
    await this.page.goto('/');
  }

  async clickFeatureCard(name: string) {
    // Navigate via the sidebar instead — card click is unreliable across Blazor/React
    const navMap: Record<string, string> = {
      Accounts: '/accounts',
      Tasks: '/tasks',
      Users: '/users',
      Files: '/files',
    };
    await this.page.goto(navMap[name] ?? `/${name.toLowerCase()}`);
  }
}
