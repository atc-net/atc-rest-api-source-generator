import { type Locator, type Page } from '@playwright/test';

export class UserFormPage {
  readonly page: Page;
  readonly heading: Locator;
  readonly firstNameInput: Locator;
  readonly lastNameInput: Locator;
  readonly emailInput: Locator;
  readonly submitButton: Locator;
  readonly cancelButton: Locator;

  constructor(page: Page) {
    this.page = page;
    // Blazor: "Create New User", React: "Create User" / "Edit User"
    this.heading = page.getByRole('heading', { name: /create.*user|edit user/i });
    this.firstNameInput = page.getByLabel(/first name/i);
    this.lastNameInput = page.getByLabel(/last name/i);
    this.emailInput = page.getByLabel(/^email$/i);
    this.submitButton = page.getByRole('button', { name: /^(create|update)$/i });
    this.cancelButton = page.getByRole('button', { name: /cancel/i }).or(
      page.getByRole('link', { name: /cancel/i }),
    );
  }

  async gotoCreate() {
    await this.page.goto('/users/create');
  }

  async fillBasicInfo(firstName: string, lastName: string, email: string) {
    await this.firstNameInput.fill(firstName);
    await this.lastNameInput.fill(lastName);
    await this.emailInput.fill(email);
  }

  async submit() {
    await this.submitButton.click();
  }
}
