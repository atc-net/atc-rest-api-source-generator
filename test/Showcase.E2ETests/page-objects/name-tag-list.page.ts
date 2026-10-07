import { expect, type Locator, type Page } from '@playwright/test';

const expectHidden = (locator: Locator) => expect(locator).toBeHidden();

// A list of items with a name and a tag - the Accounts and the Tasks pages: create, load, edit and delete
export class NameTagListPage {
  readonly page: Page;
  readonly heading: Locator;
  readonly nameInput: Locator;
  readonly tagInput: Locator;
  readonly createButton: Locator;
  readonly loadButton: Locator;
  readonly table: Locator;
  readonly editNameInput: Locator;
  readonly editTagInput: Locator;
  readonly saveButton: Locator;

  constructor(
    page: Page,
    private readonly path: string,
    title: string,
  ) {
    this.page = page;
    this.heading = page.getByRole('heading', { name: title, exact: true });
    // Blazor label: "Name", React label: "Name". Blazor tag: "Tag (optional)", React: "Tag"
    this.nameInput = page.getByLabel(/^name$/i);
    this.tagInput = page.getByLabel(/^tag/i);
    this.createButton = page.getByRole('button', { name: /^create$/i });
    this.loadButton = page.getByRole('button', { name: new RegExp(`load ${title}`, 'i') });
    this.table = page.getByRole('table');
    // React: a dialog, Blazor: a card above the table
    this.editNameInput = page.getByLabel('New name');
    this.editTagInput = page.getByLabel('New tag');
    this.saveButton = page.getByRole('button', { name: 'Save', exact: true });
  }

  async goto() {
    await this.page.goto(this.path);
  }

  async create(name: string, tag: string) {
    await this.nameInput.fill(name);
    await this.tagInput.fill(tag);
    await this.createButton.click();
  }

  // The row of the item with this name (the table lists every item of the shared API)
  row(name: string): Locator {
    return this.table.getByRole('row').filter({ has: this.page.getByRole('cell', { name, exact: true }) });
  }

  async edit(name: string, newName: string, newTag: string) {
    await this.row(name).getByRole('button', { name: 'Edit' }).click();
    await this.editNameInput.fill(newName);
    await this.editTagInput.fill(newTag);
    await this.saveButton.click();
  }

  // Deletes after the confirmation, and returns the status the API answered the DELETE with
  async delete(name: string) {
    await this.row(name).getByRole('button', { name: 'Delete' }).click();
    const dialog = this.page.getByRole('dialog');
    const [response] = await Promise.all([
      this.page.waitForResponse((r) => r.request().method() === 'DELETE'),
      dialog.getByRole('button', { name: 'Delete' }).click(),
    ]);
    // An open dialog hides the page from the accessibility tree: the list is only looked at again once it closed
    await expectHidden(dialog);
    return response.status();
  }
}
