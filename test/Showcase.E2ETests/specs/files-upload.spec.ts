import { test, expect } from '../fixtures/test';
import { FilesUploadPage } from '../page-objects/files-upload.page';

test.describe('File Upload', () => {
  let uploadPage: FilesUploadPage;

  test.beforeEach(async ({ page }) => {
    uploadPage = new FilesUploadPage(page);
    await uploadPage.goto();
  });

  test('displays heading', async () => {
    await expect(uploadPage.heading).toBeVisible();
  });

  test('shows 4 tabs', async () => {
    await expect(uploadPage.singleFileTab).toBeVisible();
    await expect(uploadPage.multipleFilesTab).toBeVisible();
    await expect(uploadPage.fileWithMetadataTab).toBeVisible();
    await expect(uploadPage.multiWithMetadataTab).toBeVisible();
  });

  test('first tab has upload content', async ({ page }) => {
    // Verify the default (Single File) tab has upload-related content
    await expect(page.getByText(/upload|choose|select.*file/i).first()).toBeVisible();
  });
});
