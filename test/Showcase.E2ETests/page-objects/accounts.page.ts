import { type Locator, type Page } from '@playwright/test';

export class AccountsPage {
  readonly page: Page;
  readonly heading: Locator;
  readonly nameInput: Locator;
  readonly tagInput: Locator;
  readonly createButton: Locator;
  readonly loadAccountsButton: Locator;
  readonly table: Locator;
  readonly successAlert: Locator;
  readonly errorAlert: Locator;

  constructor(page: Page) {
    this.page = page;
    this.heading = page.getByRole('heading', { name: 'Accounts', exact: true });
    // Use getByLabel for better cross-framework compatibility
    // Blazor label: "Name", React label: "Name". Blazor tag: "Tag (optional)", React: "Tag"
    this.nameInput = page.getByLabel(/^name$/i);
    this.tagInput = page.getByLabel(/tag/i);
    this.createButton = page.getByRole('button', { name: /^create$/i });
    this.loadAccountsButton = page.getByRole('button', { name: /load accounts/i });
    this.table = page.getByRole('table');
    this.successAlert = page.getByRole('alert').filter({ hasText: /success/i });
    this.errorAlert = page.getByRole('alert').filter({ hasText: /error|fail/i });
  }

  async goto() {
    await this.page.goto('/accounts');
  }

  async createAccount(name: string, tag?: string) {
    await this.nameInput.fill(name);
    if (tag) {
      await this.tagInput.fill(tag);
    }
    await this.createButton.click();
  }

  async loadAccounts() {
    await this.loadAccountsButton.click();
  }

  getTableRows(): Locator {
    return this.table.locator('tbody tr');
  }

  async deleteFirstAccount() {
    const firstDataRow = this.table.locator('tbody tr').first();
    await firstDataRow.getByRole('button', { name: /delete/i }).click();
  }
}
