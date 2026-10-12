import { randomUUID } from 'node:crypto';
import type { APIRequestContext, Page } from '@playwright/test';
import { test, expect } from '../fixtures/test';
import { apiBaseUrl } from '../ports';
import { FilesUploadPage } from '../page-objects/files-upload.page';

// The four upload operations. Each answers 200 without a body, so a test checks the request the generated client
// sent (its media type), the 200, what the page then says - and the file the API stored, read back with getFileById.
const text = (name: string) => ({ name, mimeType: 'text/plain', buffer: Buffer.from(`${name} ${randomUUID()}`) });

// What each app says after an upload (Blazor: a snackbar, React: an alert)
const uploaded = {
  single: { blazor: (name: string) => `Uploaded: ${name}`, react: () => 'File uploaded successfully.' },
  multi: { blazor: () => 'Uploaded 2 files', react: () => '2 file(s) uploaded successfully.' },
  meta: { blazor: (name: string) => `Uploaded: ${name} with metadata`, react: () => 'File with metadata uploaded successfully.' },
  multiMeta: { blazor: () => 'Uploaded 2 files with metadata', react: () => '2 file(s) with metadata uploaded successfully.' },
};

type StoredFile = { id: number; fileName: string | undefined; contentType: string | undefined };

// The stored file with this content: the API numbers its files 1, 2, 3, ... (no list operation), and every test's
// content is unique, so the files are read one by one until the ids run out
async function findStoredFile(request: APIRequestContext, content: Buffer): Promise<StoredFile | undefined> {
  for (let id = 1, misses = 0; misses < 3; id++) {
    const response = await request.get(`${apiBaseUrl}/files/${id}`);
    if (response.status() === 404) {
      misses++;
      continue;
    }
    misses = 0;
    if ((await response.body()).equals(content)) {
      const disposition = response.headers()['content-disposition'] ?? '';
      return {
        id,
        fileName: /filename\*=UTF-8''([^;]+)/i.exec(disposition)?.[1] ?? /filename="?([^";]+)"?/i.exec(disposition)?.[1],
        contentType: response.headers()['content-type'],
      };
    }
  }
  return undefined;
}

async function upload(page: Page, path: string, button: RegExp | string) {
  const [request] = await Promise.all([
    page.waitForRequest((r) => r.method() === 'POST' && new URL(r.url()).pathname === `/api/v1/files/form-data/${path}`),
    page.getByRole('button', typeof button === 'string' ? { name: button, exact: true } : { name: button }).click(),
  ]);
  expect((await request.response())?.status(), `${path} answers 200`).toBe(200);
  return request;
}

const multipart = /^multipart\/form-data; boundary=\S+/;

test.describe('File Upload', () => {
  let uploadPage: FilesUploadPage;

  test.beforeEach(async ({ page }) => {
    uploadPage = new FilesUploadPage(page);
    await uploadPage.goto();
  });

  test('displays heading and the four upload tabs', async () => {
    await expect(uploadPage.heading).toBeVisible();
    await expect(uploadPage.singleFileTab).toBeVisible();
    await expect(uploadPage.multipleFilesTab).toBeVisible();
    await expect(uploadPage.fileWithMetadataTab).toBeVisible();
    await expect(uploadPage.multiWithMetadataTab).toBeVisible();
  });

  // The operation accepts application/octet-stream only: a text file is sent as that, not as text/plain (a 415)
  test('a single file is the request body, as application/octet-stream (uploadSingleFileAsFormData)', async ({ page, request, appType }) => {
    const file = text('single.txt');
    await uploadPage.fileInput.setInputFiles(file);

    const sent = await upload(page, 'singleFile', 'Upload');
    expect(sent.headers()['content-type']).toBe('application/octet-stream');
    await expect(page.getByText(uploaded.single[appType](file.name), { exact: true }).first()).toBeVisible();

    // The handler stores a raw body under a fixed name
    expect(await findStoredFile(request, file.buffer)).toMatchObject({ fileName: 'uploaded-file' });
  });

  // The TypeScript clients send a FormData body; the Axios client names it multipart/form-data itself (axios's fetch
  // adapter in Node would label it form-urlencoded). In a browser, axios drops that header so the browser sets it with
  // the boundary - the stored files prove the body on every client.
  test('multiple files are the parts of one multipart/form-data body (uploadMultiFilesAsFormData)', async ({ page, request, appType }) => {
    await uploadPage.selectTab(1);
    const files = [text('first.txt'), text('second.txt')];
    await uploadPage.fileInput.setInputFiles(files);

    const sent = await upload(page, 'multiFile', /^upload( all)? \(2( files)?\)$/i);
    expect(sent.headers()['content-type']).toMatch(multipart);
    await expect(page.getByText(uploaded.multi[appType](), { exact: true }).first()).toBeVisible();

    for (const file of files) {
      expect(await findStoredFile(request, file.buffer)).toMatchObject({ fileName: file.name, contentType: expect.stringMatching(/^text\/plain/) });
    }
  });

  test('a file with its metadata is one multipart/form-data body (uploadSingleObjectWithFileAsFormData)', async ({ page, request, appType }) => {
    await uploadPage.selectTab(2);
    const file = text('report.txt');
    await page.getByLabel('Item Name').fill('Quarterly');
    await page.getByLabel('Items (comma-separated)').fill('finance, q3');
    await uploadPage.fileInput.setInputFiles(file);

    const sent = await upload(page, 'singleObject', 'Upload with Metadata');
    expect(sent.headers()['content-type']).toMatch(multipart);
    await expect(page.getByText(uploaded.meta[appType](file.name), { exact: true }).first()).toBeVisible();

    // The handler names the stored file after the item name and the items: what reached the server - every item, from
    // every client (Atc.Rest.Client before 2.3.0 kept only the last value of a repeated form field)
    expect(await findStoredFile(request, file.buffer)).toMatchObject({ fileName: 'Quarterly-finance-q3.bin' });
  });

  test('files as the files of a model are one multipart/form-data body (uploadSingleObjectWithFilesAsFormData)', async ({ page, request, appType }) => {
    await uploadPage.selectTab(3);
    const files = [text('a.txt'), text('b.txt')];
    await uploadPage.fileInput.setInputFiles(files);

    const sent = await upload(page, 'singleObjectMultiFile', /^upload( all with metadata)? \(2( files)?\)$/i);
    expect(sent.headers()['content-type']).toMatch(multipart);
    await expect(page.getByText(uploaded.multiMeta[appType](), { exact: true }).first()).toBeVisible();

    for (const file of files) {
      expect(await findStoredFile(request, file.buffer)).toMatchObject({ fileName: file.name });
    }
  });
});
