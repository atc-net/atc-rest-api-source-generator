import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { createHash } from 'node:crypto';
import { test, expect } from '../fixtures/test';
import { DocumentsPage } from '../page-objects/documents.page';

// Real files of each kind (fixtures/files/generate.mjs): a PDF, an Excel workbook, and a PNG and a JPEG encoded by
// Chromium - the preview decodes the images for real.
const fixtures = [
  { file: 'report.pdf', mimeType: 'application/pdf', kind: 'pdf', image: false },
  { file: 'budget.xlsx', mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', kind: 'spreadsheet', image: false },
  { file: 'chart.png', mimeType: 'image/png', kind: 'image', image: true },
  { file: 'photo.jpg', mimeType: 'image/jpeg', kind: 'image', image: true },
];

const sha256 = (bytes: Buffer) => createHash('sha256').update(bytes).digest('hex');

test.describe('Documents', () => {
  for (const fixture of fixtures) {
    test(`${fixture.file}: upload with its own media type, download with its bytes and file name, summary as JSON and PDF`, async ({ page }, testInfo) => {
      const documents = new DocumentsPage(page);
      const bytes = readFileSync(join(process.cwd(), 'fixtures', 'files', fixture.file));
      // A unique, non-ASCII file name: the download must bring it back through Content-Disposition (filename*)
      const name = `Råpport ${testInfo.project.name} ${Date.now()} ${fixture.file}`;

      await documents.goto();
      await expect(documents.heading).toBeVisible();

      // Upload: the file is the raw request body, with its own media type
      await documents.fileInput.setInputFiles({ name, mimeType: fixture.mimeType, buffer: bytes });
      await expect(documents.status).toContainText(`Uploaded ${name} as ${fixture.mimeType}`);
      const row = documents.row(name);
      await expect(row).toBeVisible();

      // Download: the same bytes, under the same name (the browser can read the exposed Content-Disposition)
      const [download] = await Promise.all([page.waitForEvent('download'), row.getByTestId('document-download').click()]);
      expect(download.suggestedFilename()).toBe(name);
      expect(sha256(readFileSync(await download.path()))).toBe(sha256(bytes));

      // Preview: an image is decoded by the browser; any other kind is described
      await row.getByTestId('document-preview').click();
      if (fixture.image) {
        await expect(documents.previewImage).toBeVisible();
        await expect.poll(() => documents.previewImage.evaluate((img: HTMLImageElement) => img.naturalWidth)).toBe(64);
      } else {
        await expect(documents.previewMessage).toContainText(fixture.mimeType);
      }

      // A model or a file: the summary as JSON ...
      await row.getByTestId('document-summary').click();
      await expect(documents.summary).toBeVisible();
      await expect(documents.summaryKind).toHaveText(fixture.kind);
      await expect(documents.summaryFileName).toHaveText(name);

      // ... and as a PDF
      const [summaryPdf] = await Promise.all([page.waitForEvent('download'), row.getByTestId('document-summary-pdf').click()]);
      expect(summaryPdf.suggestedFilename()).toBe(`${name.replace(/\.[^.]+$/, '')}-summary.pdf`);
      const pdf = readFileSync(await summaryPdf.path());
      expect(pdf.subarray(0, 5).toString('latin1')).toBe('%PDF-');
      expect(pdf.toString('latin1')).toContain(`(Kind: ${fixture.kind})`);
    });
  }

  test('copy answers 303 See Other to the copy, and the summary answers 304 Not Modified for its ETag', async ({ page }, testInfo) => {
    const documents = new DocumentsPage(page);
    const name = `Status ${testInfo.project.name} ${Date.now()} report.pdf`;
    await documents.goto();
    await documents.fileInput.setInputFiles({ name, mimeType: 'application/pdf', buffer: readFileSync(join(process.cwd(), 'fixtures', 'files', 'report.pdf')) });
    const row = documents.row(name);
    await expect(row).toBeVisible();

    // 303: the POST is redirected to the copy's summary, which the browser reads with a GET
    const [followed] = await Promise.all([
      page.waitForResponse((r) => r.url().endsWith('/summary') && r.request().redirectedFrom()?.url().endsWith('/copy') === true),
      row.getByTestId('document-copy').click(),
    ]);
    expect(followed.request().method()).toBe('GET');
    expect(followed.status()).toBe(200);
    expect((await followed.request().redirectedFrom()!.response())!.status()).toBe(303);
    await expect(page.getByTestId('document-copy-status')).toHaveText(`Copied as Copy of ${name} (303 See Other, followed to its summary)`);
    await expect(documents.row(`Copy of ${name}`)).toBeVisible();

    // 304: the summary's ETag sent back as If-None-Match
    await row.getByTestId('document-summary').click();
    await expect(page.getByTestId('summary-etag')).toHaveText(/^"[0-9a-f]{16}-json"$/);
    const [notModified] = await Promise.all([
      page.waitForResponse((r) => r.url().includes('/summary') && r.status() === 304),
      page.getByTestId('summary-check').click(),
    ]);
    expect(await notModified.request().headerValue('If-None-Match')).toMatch(/^"[0-9a-f]{16}-json"$/);
    await expect(page.getByTestId('summary-check-status')).toHaveText(/^Not modified \(304\) - "[0-9a-f]{16}-json" is current$/);
  });
});