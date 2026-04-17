import { type Locator, type Page } from '@playwright/test';

export class TasksPage {
  readonly page: Page;
  readonly heading: Locator;
  readonly nameInput: Locator;
  readonly tagInput: Locator;
  readonly createButton: Locator;
  readonly loadTasksButton: Locator;
  readonly table: Locator;
  readonly successAlert: Locator;
  readonly errorAlert: Locator;

  constructor(page: Page) {
    this.page = page;
    this.heading = page.getByRole('heading', { name: 'Tasks', exact: true });
    this.nameInput = page.getByLabel(/^name$/i);
    this.tagInput = page.getByLabel(/tag/i);
    this.createButton = page.getByRole('button', { name: /^create$/i });
    this.loadTasksButton = page.getByRole('button', { name: /load tasks/i });
    this.table = page.getByRole('table');
    this.successAlert = page.getByRole('alert').filter({ hasText: /success/i });
    this.errorAlert = page.getByRole('alert').filter({ hasText: /error|fail/i });
  }

  async goto() {
    await this.page.goto('/tasks');
  }

  async createTask(name: string, tag?: string) {
    await this.nameInput.fill(name);
    if (tag) {
      await this.tagInput.fill(tag);
    }
    await this.createButton.click();
  }

  async loadTasks() {
    await this.loadTasksButton.click();
  }

  getTableRows(): Locator {
    return this.table.locator('tbody tr');
  }
}
