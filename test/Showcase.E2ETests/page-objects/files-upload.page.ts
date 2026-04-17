import { type Page, type Locator } from '@playwright/test';

export class FilesUploadPage {
  readonly heading: Locator;
  readonly singleFileTab: Locator;
  readonly multipleFilesTab: Locator;
  readonly fileWithMetadataTab: Locator;
  readonly multiWithMetadataTab: Locator;

  constructor(private readonly page: Page) {
    // Blazor: "Files - Upload", React: "File Upload"
    this.heading = page.getByRole('heading', { name: /file.*upload|files.*upload/i });
    this.singleFileTab = page.getByRole('tab', { name: 'Single File', exact: true });
    this.multipleFilesTab = page.getByRole('tab', { name: 'Multiple Files', exact: true });
    this.fileWithMetadataTab = page.getByRole('tab', { name: 'File with Metadata', exact: true });
    // Blazor: "Multiple Files with Metadata", React: "Multi with Metadata"
    this.multiWithMetadataTab = page.getByRole('tab', { name: /multi.*with metadata/i });
  }

  async goto() {
    await this.page.goto('/files/upload');
  }

  async selectTab(index: number) {
    const tabs = [
      this.singleFileTab,
      this.multipleFilesTab,
      this.fileWithMetadataTab,
      this.multiWithMetadataTab,
    ];
    await tabs[index].click();
  }

  getUploadButton() {
    // Match "Upload", "Upload All", "Upload with Metadata", "Upload All with Metadata"
    return this.page.getByRole('button', { name: /upload/i }).first();
  }
}
