import { test, expect } from '../fixtures/test';
import { LiveStreamsPage, type StreamFormat } from '../page-objects/live-streams.page';

// The API streams the same forty prices in five formats; every app reads each of them with its generated client
const formats: { key: StreamFormat; name: string; header: string }[] = [
  { key: 'sse', name: 'Server-Sent Events', header: 'Server-Sent Events' },
  { key: 'lines', name: 'JSON Lines', header: 'JSON Lines' },
  { key: 'array', name: 'a JSON array', header: 'JSON array' },
  { key: 'seq', name: 'a JSON Text Sequence', header: 'JSON Text Sequence' },
  { key: 'mixed', name: 'multipart/mixed', header: 'multipart/mixed' },
];

test.describe('Live Streams', () => {
  let streams: LiveStreamsPage;

  test.beforeEach(async ({ page, browserErrors }) => {
    // A streamed response is released by cancelling its request: once every price is read (the reader stops at the
    // last record, not at the end of the connection), when the user stops it, and after an error body is read.
    // What the tests assert is what was read: all forty prices, or the typed failure.
    browserErrors.expectAborted('/streams/prices/', 'a streamed response is released by cancelling its request');
    streams = new LiveStreamsPage(page);
    await streams.goto();
    await expect(streams.heading).toBeVisible();
  });

  for (const format of formats) {
    test(`reads every price streamed as ${format.name}`, async ({ appType }) => {
      await streams.select(format.key);
      await streams.startButton.click();

      // The prices arrive one at a time: some are shown while the stream is still open
      await expect(streams.quotes.first()).toBeVisible();
      await expect(streams.arrivals.locator('tbody tr').first()).toContainText('ms');

      await expect(streams.status).toHaveText('Completed: 40 prices', { timeout: 30_000 });
      await expect(streams.items).toHaveText('40');
      await expect(streams.arrivals.locator('tbody tr').first()).toContainText('40');

      if (appType === 'blazor') {
        // The response header the operation declares, read by the generated result class
        await expect(streams.header).toHaveText(format.header);
      }
    });
  }

  test('stops a stream the user cancels', async () => {
    await streams.startButton.click();
    await expect(streams.arrivals.locator('tbody tr').nth(2)).toBeVisible();

    await streams.stopButton.click();

    await expect(streams.status).toHaveText(/^Stopped after \d+ prices$/);
    await expect(streams.startButton).toBeEnabled();
  });

  test('resumes a dropped event stream after the last event id', async ({ browserErrors }) => {
    // The API drops the connection after five events - that is the test: the browser logs the broken response
    // (as the failed request, and as the console error for it)
    browserErrors.expect(
      /\/streams\/prices\/events\?\S* net::ERR_INCOMPLETE_CHUNKED_ENCODING|net::ERR_INCOMPLETE_CHUNKED_ENCODING \(\S*\/streams\/prices\/events\?/,
      'the API drops the stream on purpose',
    );

    await streams.resumeButton.click();

    // All twelve prices, each once: five before the drop, and the seven after event 5
    await expect(streams.resumeStatus).toHaveText('Completed: 12 prices, resumed after event 5', { timeout: 30_000 });
  });

  for (const format of formats) {
    test(`shows the typed failure of a closed exchange (${format.name})`, async ({ browserErrors }) => {
      browserErrors.expectStatus(503, '/streams/prices/', 'the test asks for the closed exchange');
      await streams.select(format.key);

      await streams.closedButton.click();

      await expect(streams.failureStatus).toHaveText('503');
      await expect(streams.failureProblem).toContainText('The exchange is closed.');
      await expect(streams.failureRetry).toHaveText('30');
    });
  }
});
