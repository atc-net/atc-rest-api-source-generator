import { test, expect } from '../fixtures/test';
import { apiBaseUrl } from '../ports';

const demoToken = 'demo';

// A valid user to create; each test breaks some of its fields
const user = (stamp: string) => ({
  firstName: 'Katherine',
  lastName: 'Johnson',
  email: `katherine.${stamp}@example.com`,
  phone: '+4512345678',
  website: 'https://example.com/katherine',
  dateOfBirth: '1918-08-26',
  role: 'Employee',
  address: { street: '1 Langley Blvd', city: 'Hampton', postalCode: '23681', country: 'United States' },
});

// What the specification's constraints say, enforced by the generated server: a pattern, format: email and
// format: uri on the model, and a format: date the JSON reader cannot convert - each a 400 naming the field
test.describe('Users validation (API)', () => {
  test('a valid user is created', async ({ request }, testInfo) => {
    const response = await request.post(`${apiBaseUrl}/users`, {
      headers: { Authorization: `Bearer ${demoToken}` },
      data: user(`${testInfo.project.name}-${Date.now()}`),
    });
    expect(response.status()).toBe(201);
  });

  test('a phone number, an email and a website that break their constraints are a 400 naming each', async ({ request }, testInfo) => {
    const response = await request.post(`${apiBaseUrl}/users`, {
      headers: { Authorization: `Bearer ${demoToken}` },
      data: { ...user(`${testInfo.project.name}-${Date.now()}`), phone: '12-34-56', email: 'not-an-email', website: 'example.com/katherine' },
    });
    expect(response.status()).toBe(400);
    const problem = (await response.json()) as { errors: Record<string, string[]> };
    expect(Object.keys(problem.errors).sort()).toEqual(['email', 'phone', 'website']);
    expect(problem.errors.phone[0]).toContain('^\\+?[1-9]\\d{6,14}$');
  });

  test('a date that does not parse is a 400 naming it', async ({ request }, testInfo) => {
    const response = await request.post(`${apiBaseUrl}/users`, {
      headers: { Authorization: `Bearer ${demoToken}` },
      data: { ...user(`${testInfo.project.name}-${Date.now()}`), dateOfBirth: '1918-13-40' },
    });
    expect(response.status()).toBe(400);
    const problem = (await response.json()) as { errors: Record<string, string[]> };
    expect(Object.keys(problem.errors)).toEqual(['dateOfBirth']);
  });
});
