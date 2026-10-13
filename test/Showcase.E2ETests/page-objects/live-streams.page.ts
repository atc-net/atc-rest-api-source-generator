import { type Page, type Locator } from '@playwright/test';

export type StreamFormat = 'sse' | 'lines' | 'array' | 'seq' | 'mixed';

// The live-streams page is the same in the three apps: the same controls, by the same test ids
export class LiveStreamsPage {
  readonly heading: Locator;
  readonly startButton: Locator;
  readonly stopButton: Locator;
  readonly status: Locator;
  readonly items: Locator;
  readonly header: Locator;
  readonly quotes: Locator;
  readonly arrivals: Locator;
  readonly closedButton: Locator;
  readonly failureStatus: Locator;
  readonly failureProblem: Locator;
  readonly failureRetry: Locator;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole('heading', { name: 'Live Streams' });
    this.startButton = page.getByTestId('stream-start');
    this.stopButton = page.getByTestId('stream-stop');
    this.status = page.getByTestId('stream-status');
    this.items = page.getByTestId('stream-items');
    this.header = page.getByTestId('stream-header');
    this.quotes = page.getByTestId('stream-quote');
    this.arrivals = page.getByTestId('stream-arrivals');
    this.closedButton = page.getByTestId('stream-closed');
    this.failureStatus = page.getByTestId('stream-failure-status');
    this.failureProblem = page.getByTestId('stream-failure-problem');
    this.failureRetry = page.getByTestId('stream-failure-retry');
  }

  async goto() {
    await this.page.goto('/streams/prices');
  }

  async select(format: StreamFormat) {
    await this.page.getByTestId(`stream-format-${format}`).click();
  }
}
