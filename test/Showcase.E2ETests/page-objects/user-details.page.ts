import { type Locator, type Page } from '@playwright/test';

export class UserDetailsPage {
  readonly page: Page;
  readonly editButton: Locator;
  readonly deleteButton: Locator;

  constructor(page: Page) {
    this.page = page;
    // Blazor: a link, React: a button
    this.editButton = page.getByRole('button', { name: 'Edit', exact: true }).or(page.getByRole('link', { name: 'Edit', exact: true }));
    this.deleteButton = page.getByRole('button', { name: 'Delete', exact: true });
  }

  // The profile card's name: an h5 in both apps (Blazor's app bar title is an h5 too)
  nameHeading(name: string): Locator {
    return this.page.getByRole('heading', { level: 5, name, exact: true });
  }

  async goto(userId: string) {
    await this.page.goto(`/users/${userId}`);
  }

  // Deletes after the confirmation, and returns the status the API answered the DELETE with
  async delete() {
    await this.deleteButton.click();
    const [response] = await Promise.all([
      this.page.waitForResponse((r) => r.request().method() === 'DELETE'),
      this.page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click(),
    ]);
    return response.status();
  }
}
