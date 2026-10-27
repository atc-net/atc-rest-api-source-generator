import { type Page } from '@playwright/test';
import { NameTagListPage } from './name-tag-list.page';

export class AccountsPage extends NameTagListPage {
  constructor(page: Page) {
    super(page, '/accounts', 'Accounts');
  }
}
