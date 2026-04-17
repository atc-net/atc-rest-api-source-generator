import { type Locator, type Page } from '@playwright/test';

export class UsersPage {
  readonly page: Page;
  readonly heading: Locator;
  readonly searchInput: Locator;
  readonly roleSelect: Locator;
  readonly createUserButton: Locator;
  readonly searchButton: Locator;
  readonly table: Locator;
  readonly successAlert: Locator;
  readonly errorAlert: Locator;

  constructor(page: Page) {
    this.page = page;
    this.heading = page.getByRole('heading', { name: 'Users', exact: true });
    // Blazor: "Search (name, email)", React: "Search"
    this.searchInput = page.getByLabel(/search/i);
    // MUI Select doesn't use standard label association; use getByText
    this.roleSelect = page.getByText(/role/i).first();
    // Blazor: "New User", React: "Create User"
    this.createUserButton = page.getByRole('button', { name: /create user|new user/i }).or(
      page.getByRole('link', { name: /create user|new user/i }),
    );
    this.searchButton = page.getByRole('button', { name: 'Search', exact: true }).or(
      page.getByRole('button', { name: /^search$/i }),
    );
    this.table = page.getByRole('table');
    this.successAlert = page.getByRole('alert').filter({ hasText: /success/i });
    this.errorAlert = page.getByRole('alert').filter({ hasText: /error|fail/i });
  }

  async goto() {
    await this.page.goto('/users');
  }

  async searchUsers(query: string) {
    await this.searchInput.fill(query);
    await this.searchButton.click();
  }

  async clickCreateUser() {
    await this.createUserButton.click();
  }

  getTableRows(): Locator {
    return this.table.locator('tbody tr');
  }
}
