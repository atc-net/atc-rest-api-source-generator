import { test, expect } from '../fixtures/test';
import { apiBaseUrl } from '../ports';

const authorization = { Authorization: 'Bearer demo' };

// The live price feed (listLivePriceEvents) as the wire has it: what a long-lived event stream needs besides its
// items - one feed every subscriber shares, a replay of the latest prices after Last-Event-ID, and a heartbeat when the
// market is quiet. The API is the same for every app, so it runs once.

interface ServerSentEvent {
  id?: string;
  event?: string;
  data: string;
}

// The events of a text/event-stream body: blocks separated by a blank line, "field: value" lines
function eventsOf(body: string): ServerSentEvent[] {
  return body
    .replace(/\r\n?/g, '\n')
    .split('\n\n')
    .filter((block) => block.trim().length > 0)
    .map((block) => {
      const event: ServerSentEvent = { data: '' };
      for (const line of block.split('\n')) {
        const colon = line.indexOf(':');
        const field = colon < 0 ? line : line.slice(0, colon);
        const value = colon < 0 ? '' : line.slice(colon + 1).replace(/^ /, '');
        if (field === 'id') event.id = value;
        else if (field === 'event') event.event = value;
        else if (field === 'data') event.data = event.data ? `${event.data}\n${value}` : value;
      }
      return event;
    });
}

const prices = (events: ServerSentEvent[]) => events.filter((e) => e.event === undefined);
const sequences = (events: ServerSentEvent[]) => prices(events).map((e) => Number(e.id));

test.describe('Live price feed', () => {
  test.beforeEach(({ appType }) => {
    test.skip(appType !== 'blazor', 'An API-only test: it runs once, not once per app.');
  });

  test('every subscriber reads the same feed, each price an event with its sequence as the id', async ({ request }) => {
    const [first, second] = await Promise.all([
      request.get(`${apiBaseUrl}/streams/prices/live?count=4`, { headers: authorization, timeout: 60_000 }),
      request.get(`${apiBaseUrl}/streams/prices/live?count=4`, { headers: authorization, timeout: 60_000 }),
    ]);
    expect(first.status()).toBe(200);
    expect(first.headers()['content-type']).toContain('text/event-stream');

    const a = eventsOf(await first.text());
    const b = eventsOf(await second.text());
    expect(sequences(a)).toHaveLength(4);
    expect(sequences(b)).toHaveLength(4);

    // One feed: the prices both read are the same prices, under the same ids
    const shared = sequences(a).filter((s) => sequences(b).includes(s));
    expect(shared.length).toBeGreaterThan(0);
    for (const sequence of shared) {
      expect(prices(a).find((e) => Number(e.id) === sequence)?.data).toBe(prices(b).find((e) => Number(e.id) === sequence)?.data);
    }

    for (const event of prices(a)) {
      expect(JSON.parse(event.data).sequence).toBe(Number(event.id));
    }
  });

  test('a client that resumes gets the prices after its last event id, and how many the buffer no longer had', async ({ request }) => {
    const first = await request.get(`${apiBaseUrl}/streams/prices/live?count=1`, { headers: authorization, timeout: 60_000 });
    const [last] = sequences(eventsOf(await first.text()));

    // The prices after it: from the replay buffer as far as it has them, then live - none twice, none skipped
    const resumed = await request.get(`${apiBaseUrl}/streams/prices/live?count=2`, {
      headers: { ...authorization, 'Last-Event-ID': String(last) },
      timeout: 60_000,
    });
    expect(sequences(eventsOf(await resumed.text()))).toEqual([last + 1, last + 2]);
    expect(resumed.headers()['x-events-missed']).toBe('0');

    // From the start: the buffer holds the latest prices only - X-Events-Missed says how many came before them
    const fromTheStart = await request.get(`${apiBaseUrl}/streams/prices/live?count=1`, {
      headers: { ...authorization, 'Last-Event-ID': '0' },
      timeout: 60_000,
    });
    const [oldest] = sequences(eventsOf(await fromTheStart.text()));
    expect(Number(fromTheStart.headers()['x-events-missed'])).toBe(oldest - 1);
  });

  test('a quiet market sends a ping event, which is no price', async ({ request }) => {
    // Prices come between a fifth of a second and three seconds apart: with a one-second heartbeat, the quiet
    // stretches among eight prices have pings
    const response = await request.get(`${apiBaseUrl}/streams/prices/live?count=8&heartbeatSeconds=1`, { headers: authorization, timeout: 90_000 });
    const events = eventsOf(await response.text());

    expect(prices(events)).toHaveLength(8);
    const pings = events.filter((e) => e.event === 'ping');
    expect(pings.length).toBeGreaterThan(0);
    for (const ping of pings) {
      expect(ping.id).toBeUndefined();
    }
  });
});