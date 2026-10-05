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

  // The TypeScript clients send a FormData body; the Axios client names it multipart/form-data itself (axios's
  // fetch adapter in Node would label it form-urlencoded). In a browser, axios drops that header so the browser
  // sets it with the boundary - this proves it on React Axios, and the server accepting the files proves the body.
  test('multiple files upload as multipart/form-data with a boundary', async ({ page, appType }) => {
    test.skip(appType !== 'react', 'The FormData body is the TypeScript clients\' (Blazor uses the C# client).');

    await uploadPage.selectTab(1);
    const files = [
      { name: 'first.txt', mimeType: 'text/plain', buffer: Buffer.from('first file') },
      { name: 'second.txt', mimeType: 'text/plain', buffer: Buffer.from('second file') },
    ];
    await page.locator('input[type="file"][multiple]').setInputFiles(files);

    const request = page.waitForRequest((r) => r.url().includes('/files/form-data/multiFile') && r.method() === 'POST');
    await page.getByRole('button', { name: 'Upload (2)' }).click();

    const contentType = (await request).headers()['content-type'] ?? '';
    expect(contentType).toMatch(/^multipart\/form-data; boundary=\S+/);
    await expect(page.getByText('2 file(s) uploaded successfully.')).toBeVisible();
  });
});
