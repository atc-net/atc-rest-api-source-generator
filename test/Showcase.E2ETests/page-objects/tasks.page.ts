import { type Page } from '@playwright/test';
import { NameTagListPage } from './name-tag-list.page';

export class TasksPage extends NameTagListPage {
  constructor(page: Page) {
    super(page, '/tasks', 'Tasks');
  }
}
