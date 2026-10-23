import { createHmac, randomUUID } from 'node:crypto';
import { test, expect } from '../fixtures/test';
import { apiBaseUrl } from '../ports';
import { unique, uniqueSurname } from '../fixtures/api';

// The Showcase's demo authentication (Showcase.Api/ServiceCollectionExtensions.cs): a token that does not validate
// becomes the demo identity, which holds every scope - that is what the apps send. A token that does validate (signed
// with the demo signing key) keeps its own claims, so it can lack a scope; no token at all is no identity.
const signingKey = 'demo-signing-key-for-showcase-only-not-for-production-use-32bytes!';

const base64Url = (value: string | Buffer) => Buffer.from(value).toString('base64url');

function signedToken(scope: string | undefined) {
  const now = Math.floor(Date.now() / 1000);
  const header = base64Url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const payload = base64Url(JSON.stringify({ sub: 'e2e-user', name: 'E2E User', iat: now, exp: now + 600, ...(scope ? { scope } : {}) }));
  const signature = createHmac('sha256', signingKey).update(`${header}.${payload}`).digest('base64url');
  return `${header}.${payload}.${signature}`;
}

const newUser = (email: string) => ({
  firstName: 'Annie',
  lastName: uniqueSurname('Easley'),
  email,
  dateOfBirth: '1933-04-23',
  role: 'Employee',
  address: { street: '21000 Brookpark Rd', city: 'Cleveland', postalCode: '44135', country: 'United States' },
});

// The 401 and 403 answers of the generated endpoints: requests from the test, as no app sends them (they all use the
// demo token, which may do everything)
test.describe('Authentication and authorization', () => {
  test('an operation that requires a token answers 401 without one', async ({ request }) => {
    const response = await request.get(`${apiBaseUrl}/accounts`);
    expect(response.status()).toBe(401);
  });

  test('createUser and deleteUserById answer 403 to a token without the users:write and admin scopes', async ({ request }) => {
    const readOnly = { Authorization: `Bearer ${signedToken('users:read')}` };

    // The token is valid: an operation without scopes accepts it
    expect((await request.get(`${apiBaseUrl}/accounts?limit=1`, { headers: readOnly })).status()).toBe(200);

    const create = await request.post(`${apiBaseUrl}/users`, { headers: readOnly, data: newUser(`annie.${randomUUID()}@example.com`) });
    expect(create.status()).toBe(403);

    const remove = await request.delete(`${apiBaseUrl}/users/${randomUUID()}`, { headers: readOnly });
    expect(remove.status()).toBe(403);

    // One of the two scopes is not enough
    const writeOnly = { Authorization: `Bearer ${signedToken('users:read users:write')}` };
    expect((await request.post(`${apiBaseUrl}/users`, { headers: writeOnly, data: newUser(`annie.${randomUUID()}@example.com`) })).status()).toBe(403);
  });

  test('createUser answers 201 to a token with both scopes', async ({ request }, testInfo) => {
    const admin = { Authorization: `Bearer ${signedToken('users:read users:write admin')}` };
    const email = `${unique('annie', testInfo).replace(/\s+/g, '.').toLowerCase()}@example.com`;

    const create = await request.post(`${apiBaseUrl}/users`, { headers: admin, data: newUser(email) });
    expect(create.status()).toBe(201);
    const created = (await create.json()) as { id: string; email: string };
    expect(created.email).toBe(email);

    // And the same token may delete it again
    expect((await request.delete(`${apiBaseUrl}/users/${created.id}`, { headers: admin })).status()).toBe(204);
  });
});
