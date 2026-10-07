import { randomUUID } from 'node:crypto';
import { test, expect } from '../fixtures/test';
import { apiGet } from '../fixtures/api';

type User = { id: string; email: string };

// queryUsers: the OpenAPI 3.2 QUERY method - a read whose criteria (a set of ids) travel in the request body
test.describe('Users query (HTTP QUERY)', () => {
  test('looks users up by their ids with a QUERY request, and skips an unknown id', async ({ page, request }) => {
    const users = (await apiGet<User[]>(request, '/users?limit=2')).body;
    expect(users).toHaveLength(2);
    const unknownId = randomUUID();
    const ids = [users[0].id, unknownId, users[1].id];

    await page.goto('/users');
    await page.getByLabel('User ids').fill(ids.join('\n'));
    const [query] = await Promise.all([
      page.waitForRequest((r) => r.method() === 'QUERY' && new URL(r.url()).pathname === '/api/v1/users'),
      page.getByTestId('users-query').click(),
    ]);

    // The ids are the request body, not the URL
    expect(new URL(query.url()).search).toBe('');
    expect(query.postDataJSON()).toEqual({ ids });
    expect((await query.response())?.status()).toBe(200);

    await expect(page.getByTestId('users-query-status')).toHaveText('Found 2 of 3 users');
    const rows = page.getByTestId('users-query-row');
    await expect(rows).toHaveCount(2);
    for (const user of users) {
      await expect(rows.filter({ hasText: user.email })).toHaveCount(1);
    }
  });
});
