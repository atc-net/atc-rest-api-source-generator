import { type Page, type Locator } from '@playwright/test';

export class FilesUploadPage {
  readonly heading: Locator;
  readonly singleFileTab: Locator;
  readonly multipleFilesTab: Locator;
  readonly fileWithMetadataTab: Locator;
  readonly multiWithMetadataTab: Locator;
  // Both apps render the file input of the selected tab only
  readonly fileInput: Locator;

  constructor(private readonly page: Page) {
    // Blazor: "Files - Upload", React: "File Upload"
    this.heading = page.getByRole('heading', { name: /file.*upload|files.*upload/i });
    this.singleFileTab = page.getByRole('tab', { name: 'Single File', exact: true });
    this.multipleFilesTab = page.getByRole('tab', { name: 'Multiple Files', exact: true });
    this.fileWithMetadataTab = page.getByRole('tab', { name: 'File with Metadata', exact: true });
    // Blazor: "Multiple Files with Metadata", React: "Multi with Metadata"
    this.multiWithMetadataTab = page.getByRole('tab', { name: /multi.*with metadata/i });
    this.fileInput = page.locator('input[type="file"]');
  }

  async goto() {
    await this.page.goto('/files/upload');
  }

  async selectTab(index: number) {
    const tabs = [this.singleFileTab, this.multipleFilesTab, this.fileWithMetadataTab, this.multiWithMetadataTab];
    await tabs[index].click();
  }
}
