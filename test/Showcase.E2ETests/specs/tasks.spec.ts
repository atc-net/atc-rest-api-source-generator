import { test, expect } from '../fixtures/test';
import { TasksPage } from '../page-objects/tasks.page';

test.describe('Tasks Page', () => {
  let tasksPage: TasksPage;

  test.beforeEach(async ({ page }) => {
    tasksPage = new TasksPage(page);
    await tasksPage.goto();
  });

  test('displays heading', async () => {
    await expect(tasksPage.heading).toBeVisible();
  });

  test('shows create form with Name and Tag fields', async () => {
    await expect(tasksPage.nameInput).toBeVisible();
    await expect(tasksPage.tagInput).toBeVisible();
    await expect(tasksPage.createButton).toBeVisible();
  });

  test('can load tasks', async () => {
    await tasksPage.loadTasks();
    await expect(tasksPage.table).toBeVisible();
  });

  test('create form accepts input', async () => {
    await tasksPage.nameInput.fill('Test Task');
    await expect(tasksPage.nameInput).toHaveValue('Test Task');
    await expect(tasksPage.createButton).toBeEnabled();
  });
});
