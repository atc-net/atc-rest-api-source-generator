import { test, expect } from './test';
import { apiGet, unique } from './api';
import type { NameTagListPage } from '../page-objects/name-tag-list.page';
import type { Page } from '@playwright/test';

type NameTagItem = { id: number; name: string; tag?: string | null };

// The create, update and delete of an item with a name and a tag (accounts, tasks) through the page, each outcome
// read back from the page and from the API. Only the test's own item is looked at: the API is shared by every project.
export function describeNameTagCrud(title: string, apiPath: string, open: (page: Page) => NameTagListPage) {
  test.describe(`${title} Page`, () => {
    test('displays the heading and the create form', async ({ page }) => {
      const list = open(page);
      await list.goto();
      await expect(list.heading).toBeVisible();
      await expect(list.nameInput).toBeVisible();
      await expect(list.tagInput).toBeVisible();
      await expect(list.createButton).toBeVisible();
    });

    test('creates, renames and deletes an item, and the API agrees at every step', async ({ page, request }, testInfo) => {
      const list = open(page);
      const name = unique(`E2E ${title}`, testInfo);
      const renamed = `${name} renamed`;
      const findByName = async (n: string) =>
        (await apiGet<NameTagItem[]>(request, apiPath)).body.find((item) => item.name === n);

      await list.goto();

      // Create: the list reloads with the new item, which the API has too
      await list.create(name, 'created');
      await expect(list.row(name)).toBeVisible();
      await expect(list.row(name)).toContainText('created');
      const created = await findByName(name);
      expect(created, 'the API lists the created item').toBeTruthy();
      expect(created!.tag).toBe('created');

      // Update: the new name and tag in the list, and in the API's read of that one item
      await list.edit(name, renamed, 'updated');
      await expect(list.editNameInput).toBeHidden();
      await expect(list.row(renamed)).toBeVisible();
      await expect(list.row(renamed)).toContainText('updated');
      await expect(list.row(name)).toHaveCount(0);
      const updated = await apiGet<NameTagItem>(request, `${apiPath}/${created!.id}`);
      expect(updated.status).toBe(200);
      expect(updated.body).toMatchObject({ id: created!.id, name: renamed, tag: 'updated' });

      // Delete: gone from the list and from the API
      expect(await list.delete(renamed)).toBe(204);
      await expect(list.row(renamed)).toHaveCount(0);
      expect((await apiGet(request, `${apiPath}/${created!.id}`)).status).toBe(404);
      expect(await findByName(renamed)).toBeUndefined();
    });
  });
}
