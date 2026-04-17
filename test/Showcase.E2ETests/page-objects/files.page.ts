import { type Page, type Locator } from '@playwright/test';

export class FilesPage {
  readonly heading: Locator;
  readonly previewButtons: Locator;
  readonly downloadButtons: Locator;
  readonly fileIdInput: Locator;
  readonly lookupButton: Locator;
  readonly previewDialog: Locator;

  constructor(private readonly page: Page) {
    // Blazor: "Files - Download", React: "Files"
    this.heading = page.getByRole('heading', { name: /^files/i });
    this.previewButtons = page.getByRole('button', { name: 'Preview' });
    this.downloadButtons = page.getByRole('button', { name: 'Download' });
    this.fileIdInput = page.getByLabel('File ID');
    this.lookupButton = page.getByRole('button', { name: 'Lookup' });
    this.previewDialog = page.getByRole('dialog');
  }

  async goto() {
    await this.page.goto('/files');
  }

  getFileCards() {
    // Each file card has a Preview button; count those as a proxy for card count
    return this.previewButtons;
  }

  async previewFile(index: number) {
    await this.previewButtons.nth(index).click();
  }

  async downloadFile(index: number) {
    await this.downloadButtons.nth(index).click();
  }
}
