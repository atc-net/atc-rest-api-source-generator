import type { APIRequestContext } from '@playwright/test';
import { test, expect } from '../fixtures/test';
import { demoHeaders, uniqueSurname } from '../fixtures/api';
import { apiBaseUrl } from '../ports';

type Problem = { errors: Record<string, string[]> };

// A valid user to create - a surname of its own, as the Showcase refuses a second user with the same name; each test
// breaks some of its fields
const user = (stamp: string) => ({
  firstName: 'Katherine',
  lastName: uniqueSurname('Johnson'),
  email: `katherine.${stamp}@example.com`,
  phone: '+4512345678',
  website: 'https://example.com/katherine',
  dateOfBirth: '1918-08-26',
  role: 'Employee',
  address: { street: '1 Langley Blvd', city: 'Hampton', postalCode: '23681', country: 'United States' },
});

const stampOf = (projectName: string) => `${projectName}-${Date.now()}-${Math.floor(Math.random() * 1e6)}`;

const createUser = (request: APIRequestContext, data: object) =>
  request.post(`${apiBaseUrl}/users`, { headers: demoHeaders, data });

const updateUser = (request: APIRequestContext, userId: string, data: object) =>
  request.put(`${apiBaseUrl}/users/${userId}`, { headers: demoHeaders, data });

// A date of birth the given number of years ago (and a day, so it is that age today)
const yearsAgo = (years: number) => {
  const date = new Date();
  date.setFullYear(date.getFullYear() - years);
  date.setDate(date.getDate() - 1);
  return date.toISOString().slice(0, 10);
};

// What the specification's constraints say, enforced by the generated server (DataAnnotations): a pattern, format: email
// and format: uri on the model, and a format: date the JSON reader cannot convert - each a 400 naming the field
test.describe('Users validation (API) - the specification', () => {
  test('a valid user is created', async ({ request }, testInfo) => {
    const response = await createUser(request, user(stampOf(testInfo.project.name)));
    expect(response.status()).toBe(201);
  });

  test('a phone number, an email and a website that break their constraints are a 400 naming each', async ({ request }, testInfo) => {
    const response = await createUser(request, { ...user(stampOf(testInfo.project.name)), phone: '12-34-56', email: 'not-an-email', website: 'example.com/katherine' });
    expect(response.status()).toBe(400);
    const problem = (await response.json()) as Problem;
    expect(Object.keys(problem.errors).sort()).toEqual(['email', 'phone', 'website']);
    expect(problem.errors.phone[0]).toContain('^\\+?[1-9]\\d{6,14}$');
  });

  test('a date that does not parse is a 400 naming it', async ({ request }, testInfo) => {
    const response = await createUser(request, { ...user(stampOf(testInfo.project.name)), dateOfBirth: '1918-13-40' });
    expect(response.status()).toBe(400);
    const problem = (await response.json()) as Problem;
    expect(Object.keys(problem.errors)).toEqual(['dateOfBirth']);
  });
});

// The domain's rules, which no specification can state (FluentValidation, Showcase.Api.Domain/Validators): they run
// next to the specification's and answer in the same 400, keyed the same way
test.describe('Users validation (API) - the domain rules', () => {
  test('a name starts with an uppercase letter', async ({ request }, testInfo) => {
    const response = await createUser(request, { ...user(stampOf(testInfo.project.name)), firstName: 'katherine' });
    expect(response.status()).toBe(400);
    const problem = (await response.json()) as Problem;
    expect(problem.errors).toEqual({ firstName: ['FirstName must start with an uppercase letter.'] });
  });

  test('the first and the last name differ', async ({ request }, testInfo) => {
    const response = await createUser(request, { ...user(stampOf(testInfo.project.name)), firstName: 'Johnson', lastName: 'Johnson' });
    expect(response.status()).toBe(400);
    const problem = (await response.json()) as Problem;
    expect(problem.errors).toEqual({ lastName: ['FirstName and LastName must not be the same.'] });
  });

  test('a user is 18 or older', async ({ request }, testInfo) => {
    const tooYoung = await createUser(request, { ...user(stampOf(testInfo.project.name)), dateOfBirth: yearsAgo(17) });
    expect(tooYoung.status()).toBe(400);
    expect(((await tooYoung.json()) as Problem).errors).toEqual({ dateOfBirth: ['A user must be 18 or older.'] });

    expect((await createUser(request, { ...user(stampOf(testInfo.project.name)), dateOfBirth: yearsAgo(18) })).status()).toBe(201);
  });

  test('an administrator has an @example.com e-mail address', async ({ request }, testInfo) => {
    const stamp = stampOf(testInfo.project.name);
    const outside = await createUser(request, { ...user(stamp), role: 'Admin', email: `katherine.${stamp}@mail.com` });
    expect(outside.status()).toBe(400);
    expect(((await outside.json()) as Problem).errors).toEqual({ email: ['An administrator must have an @example.com e-mail address.'] });

    // The same address is fine for anyone else, and an administrator at example.com is fine
    expect((await createUser(request, { ...user(stampOf(testInfo.project.name)), email: `katherine.${stamp}@mail.com` })).status()).toBe(201);
    expect((await createUser(request, { ...user(stampOf(testInfo.project.name)), role: 'Admin' })).status()).toBe(201);
  });

  test('a second user with the same name is refused - an asynchronous check that looks the name up (half a second)', async ({ request }, testInfo) => {
    const first = user(stampOf(testInfo.project.name));
    expect((await createUser(request, first)).status()).toBe(201);

    // The same name in other letter case is the same name
    const sameName = { firstName: first.firstName, lastName: first.lastName.toUpperCase() };
    const started = Date.now();
    const second = await createUser(request, { ...user(stampOf(testInfo.project.name)), ...sameName });
    const elapsed = Date.now() - started;
    expect(second.status()).toBe(400);
    expect(((await second.json()) as Problem).errors).toEqual({ lastName: [`A user named ${sameName.firstName} ${sameName.lastName} already exists.`] });
    expect(elapsed).toBeGreaterThanOrEqual(500);
  });

  test('an update may keep the user\'s own name, but not take another user\'s', async ({ request }, testInfo) => {
    const ada = user(stampOf(testInfo.project.name));
    const grace = user(stampOf(testInfo.project.name));
    const adaId = ((await (await createUser(request, ada)).json()) as { id: string }).id;
    const graceId = ((await (await createUser(request, grace)).json()) as { id: string }).id;

    // Ada keeps her name and moves: no duplicate of herself
    expect((await updateUser(request, adaId, { ...ada, address: { ...ada.address, city: 'Newport News' } })).status()).toBe(200);

    // Grace takes Ada's name: refused, keyed by the body's field although the rule also reads the route's userId
    const taken = await updateUser(request, graceId, { ...grace, lastName: ada.lastName });
    expect(taken.status()).toBe(400);
    expect(((await taken.json()) as Problem).errors).toEqual({ lastName: [`A user named ${ada.firstName} ${ada.lastName} already exists.`] });
  });
});

// Both kinds in one answer: the specification's and the domain's errors together, and on the same field both messages -
// the specification's first
test.describe('Users validation (API) - the specification and the domain rules together', () => {
  test('errors from both, on different fields, come in one 400', async ({ request }, testInfo) => {
    const response = await createUser(request, { ...user(stampOf(testInfo.project.name)), email: 'not-an-email', firstName: 'katherine' });
    expect(response.status()).toBe(400);
    const problem = (await response.json()) as Problem;
    expect(Object.keys(problem.errors).sort()).toEqual(['email', 'firstName']);
    expect(problem.errors.firstName).toEqual(['FirstName must start with an uppercase letter.']);
  });

  test('errors from both on the same field are both listed, the specification\'s first', async ({ request }, testInfo) => {
    const response = await createUser(request, { ...user(stampOf(testInfo.project.name)), firstName: 'k' });
    expect(response.status()).toBe(400);
    const problem = (await response.json()) as Problem;
    expect(Object.keys(problem.errors)).toEqual(['firstName']);
    expect(problem.errors.firstName).toHaveLength(2);
    expect(problem.errors.firstName[0]).toMatch(/length/i);
    expect(problem.errors.firstName[1]).toBe('FirstName must start with an uppercase letter.');
  });
});
