import { type Page, type Locator } from '@playwright/test';

export class AccountsPaginatedPage {
  readonly heading: Locator;
  readonly pageSizeInput: Locator;
  readonly queryStringInput: Locator;
  readonly loadButton: Locator;
  readonly nextPageButton: Locator;
  readonly previousPageButton: Locator;
  readonly firstPageButton: Locator;
  readonly table: Locator;
  readonly paginationInfo: Locator;

  constructor(private readonly page: Page) {
    // Blazor: "Paginated Accounts", React: "Accounts (Paginated)"
    this.heading = page.getByRole('heading', { name: /paginated|accounts.*paginated/i });
    this.pageSizeInput = page.getByLabel(/page size/i);
    this.queryStringInput = page.getByLabel(/query string/i);
    // Blazor: "Load Page", React: "Load"
    this.loadButton = page.getByRole('button', { name: /^load/i });
    this.nextPageButton = page.getByRole('button', { name: 'Next Page' });
    this.previousPageButton = page.getByRole('button', { name: 'Previous Page' });
    this.firstPageButton = page.getByRole('button', { name: 'First Page' });
    this.table = page.getByRole('table');
    // Blazor: "Page N | Page Size: N | Count: N | Total: N | Has more: ..."
    // React: "Showing X of Y total results | Page N"
    this.paginationInfo = page.getByText(/showing \d+|page \d+|count:/i);
  }

  async goto() {
    await this.page.goto('/accounts/paginated');
  }

  async setPageSize(size: number) {
    await this.pageSizeInput.clear();
    await this.pageSizeInput.fill(String(size));
  }

  async loadPage() {
    await this.loadButton.click();
  }

  async clickNextPage() {
    await this.nextPageButton.click();
  }
}
