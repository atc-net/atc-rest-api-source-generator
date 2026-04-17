import { type Locator, type Page } from '@playwright/test';

export class UserDetailsPage {
  readonly page: Page;
  readonly backButton: Locator;
  readonly editButton: Locator;
  readonly deleteButton: Locator;
  readonly nameHeading: Locator;
  readonly emailText: Locator;

  constructor(page: Page) {
    this.page = page;
    this.backButton = page.getByRole('button', { name: /back/i }).or(
      page.getByRole('link', { name: /back|users/i }),
    );
    this.editButton = page.getByRole('button', { name: /edit/i }).or(
      page.getByRole('link', { name: /edit/i }),
    );
    this.deleteButton = page.getByRole('button', { name: /delete/i });
    this.nameHeading = page.getByRole('heading', { level: 5 });
    this.emailText = page.getByText(/@/);
  }

  async goto(userId: string) {
    await this.page.goto(`/users/${userId}`);
  }

  async clickEdit() {
    await this.editButton.click();
  }

  async clickDelete() {
    await this.deleteButton.click();
  }
}
