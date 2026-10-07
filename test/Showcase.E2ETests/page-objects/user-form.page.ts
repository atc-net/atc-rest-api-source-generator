import { type Locator, type Page } from '@playwright/test';
import type { AppType } from '../fixtures/test';

export type NewUser = {
  firstName: string;
  lastName: string;
  email: string;
  street: string;
  city: string;
  postalCode: string;
  country: string;
};

export class UserFormPage {
  readonly page: Page;
  readonly heading: Locator;
  readonly firstNameInput: Locator;
  readonly lastNameInput: Locator;
  readonly emailInput: Locator;
  readonly dateOfBirthInput: Locator;
  readonly streetInput: Locator;
  readonly cityInput: Locator;
  readonly postalCodeInput: Locator;
  readonly countryInput: Locator;
  readonly submitButton: Locator;
  readonly cancelButton: Locator;

  constructor(
    page: Page,
    private readonly appType: AppType,
  ) {
    this.page = page;
    // Blazor: "Create New User" / "Edit User", React: "Create User" / "Edit User"
    this.heading = page.getByRole('heading', { name: /create.*user|edit user/i });
    this.firstNameInput = page.getByLabel(/first name/i);
    this.lastNameInput = page.getByLabel(/last name/i);
    this.emailInput = page.getByLabel(/^email/i);
    this.dateOfBirthInput = page.getByLabel(/date of birth/i);
    // Blazor: "Street Address", React: "Street"
    this.streetInput = page.getByLabel(/^street/i);
    this.cityInput = page.getByLabel(/^city/i);
    this.postalCodeInput = page.getByLabel(/^postal code/i);
    // Blazor also has "Country Code"
    this.countryInput = page.getByLabel(/^country\b(?! code)/i);
    this.submitButton = page.getByRole('button', { name: /^(create|update)$/i });
    // Blazor: a link, React: a button
    this.cancelButton = page.getByRole('button', { name: /cancel/i }).or(page.getByRole('link', { name: /cancel/i }));
  }

  async gotoCreate() {
    await this.page.goto('/users/create');
  }

  async fill(user: NewUser) {
    await this.firstNameInput.fill(user.firstName);
    await this.lastNameInput.fill(user.lastName);
    await this.emailInput.fill(user.email);
    // Blazor's date picker starts on a date of birth; React's date field starts empty
    if (this.appType === 'react') {
      await this.dateOfBirthInput.fill('1990-05-17');
    }
    await this.streetInput.fill(user.street);
    await this.cityInput.fill(user.city);
    await this.postalCodeInput.fill(user.postalCode);
    await this.countryInput.fill(user.country);
  }

  async submit() {
    await this.submitButton.click();
  }
}
