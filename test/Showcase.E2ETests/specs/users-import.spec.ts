import { test, expect } from '../fixtures/test';
import { apiBaseUrl } from '../ports';
import { uniqueSurname } from '../fixtures/api';

const demoToken = 'demo';

// A list of users as XML, the second one with the given first name; surnames of their own, as the Showcase refuses a
// second user with the same name
const usersXml = (stamp: string, secondFirstName: string, johnson: string, vaughan: string) => `<users>
  <user><firstName>Katherine</firstName><lastName>${johnson}</lastName><email>katherine.${stamp}@example.com</email><dateOfBirth>1918-08-26</dateOfBirth><role>Employee</role><address><street>1 Langley Blvd</street><city>Hampton</city><postalCode>23681</postalCode><country>United States</country></address></user>
  <user><firstName>${secondFirstName}</firstName><lastName>${vaughan}</lastName><email>dorothy.${stamp}@example.com</email><dateOfBirth>1910-09-20</dateOfBirth><role>Manager</role><address><street>2 Langley Blvd</street><city>Hampton</city><postalCode>23681</postalCode><country>United States</country></address></user>
</users>`;

test.describe('Users import', () => {
  test('imports a list of users sent as XML, and the server validates every user', async ({ page, request, browserErrors }, testInfo) => {
    browserErrors.expectStatus(400, '/api/v1/users/import', 'the last import sends an invalid user on purpose');
    await page.goto('/users');
    const xml = page.getByTestId('users-import-xml');
    const status = page.getByTestId('users-import-status');

    // The list the generated XML mapping wrote: a <users> root with a <user> per item
    await expect(xml).toHaveValue(/^<users>\s*<user>\s*<firstName>Ada<\/firstName>/);
    await page.getByTestId('users-import').click();
    await expect(status).toHaveText('Imported 2 users from XML');
    const rows = page.getByTestId('users-import-row');
    await expect(rows).toHaveCount(2);
    await expect(rows.nth(0)).toContainText(/^Ada Lovelace-\w+ · ada\./);
    await expect(rows.nth(1)).toContainText(/^Grace Hopper-\w+ · grace\./);

    // The users exist: the API finds the imported email
    const email = ((await rows.nth(1).textContent()) ?? '').split('· ')[1]!.trim();
    const found = await request.get(`${apiBaseUrl}/users?search=${encodeURIComponent(email)}`, { headers: { Authorization: `Bearer ${demoToken}` } });
    expect(found.ok()).toBeTruthy();
    expect(((await found.json()) as { email: string }[]).map((u) => u.email)).toEqual([email]);

    // A list of our own
    const stamp = `${testInfo.project.name}-${Date.now()}`;
    const johnson = uniqueSurname('Johnson');
    const vaughan = uniqueSurname('Vaughan');
    await xml.fill(usersXml(stamp, 'Dorothy', johnson, vaughan));
    await page.getByTestId('users-import').click();
    await expect(status).toHaveText('Imported 2 users from XML');
    await expect(rows).toHaveText([`Katherine ${johnson} · katherine.${stamp}@example.com`, `Dorothy ${vaughan} · dorothy.${stamp}@example.com`]);

    // An invalid second user (a first name too short): the validation filter names that item - new surnames, so the
    // first user is no duplicate of the one just imported
    await xml.fill(usersXml(`${stamp}-invalid`, 'D', uniqueSurname('Johnson'), uniqueSurname('Vaughan')));
    await page.getByTestId('users-import').click();
    await expect(status).toHaveText(/^400: \[1\]\.firstName: /);
    await expect(rows).toHaveCount(0);
  });
});
