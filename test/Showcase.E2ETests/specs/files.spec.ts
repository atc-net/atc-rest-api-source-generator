import { test, expect } from '../fixtures/test';
import { FilesPage } from '../page-objects/files.page';

test.describe('Files', () => {
  let filesPage: FilesPage;

  test.beforeEach(async ({ page }) => {
    filesPage = new FilesPage(page);
    await filesPage.goto();
  });

  test('displays heading', async () => {
    await expect(filesPage.heading).toBeVisible();
  });

  test('shows 5 sample file cards', async () => {
    await expect(filesPage.getFileCards()).toHaveCount(5);
  });

  test('preview button shows file content', async ({ page }) => {
    await filesPage.previewFile(0);
    // React: opens a MUI Dialog (role="dialog")
    // Blazor: shows inline preview card with "File Preview:" heading
    // React: MUI Dialog (role="dialog"), Blazor: inline card with "File Preview:" text
    const dialog = page.getByRole('dialog').first();
    const inlinePreview = page.getByText(/file preview:/i);
    await expect(dialog.or(inlinePreview).first()).toBeVisible({ timeout: 10_000 });
  });
});
