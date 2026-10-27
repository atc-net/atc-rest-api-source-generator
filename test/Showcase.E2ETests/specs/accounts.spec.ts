import { describeNameTagCrud } from '../fixtures/name-tag-crud';
import { AccountsPage } from '../page-objects/accounts.page';

// createAccount, listAccounts, updateAccountById, getAccountById and deleteAccountById
describeNameTagCrud('Accounts', '/accounts', (page) => new AccountsPage(page));
