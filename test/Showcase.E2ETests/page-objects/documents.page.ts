import { type Page, type Locator } from '@playwright/test';

export class DocumentsPage {
  readonly heading: Locator;
  readonly fileInput: Locator;
  readonly status: Locator;
  readonly previewImage: Locator;
  readonly previewMessage: Locator;
  readonly summary: Locator;
  readonly summaryKind: Locator;
  readonly summaryFileName: Locator;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole('heading', { name: /^documents$/i });
    this.fileInput = page.getByTestId('documents-file');
    this.status = page.getByTestId('documents-status');
    this.previewImage = page.getByTestId('document-preview-image');
    this.previewMessage = page.getByTestId('document-preview-message');
    this.summary = page.getByTestId('document-summary-json');
    this.summaryKind = page.getByTestId('summary-kind');
    this.summaryFileName = page.getByTestId('summary-file-name');
  }

  async goto() {
    await this.page.goto('/documents');
  }

  row(fileName: string) {
    return this.page.locator(`[data-testid="document-row"][data-file-name="${fileName}"]`);
  }
}