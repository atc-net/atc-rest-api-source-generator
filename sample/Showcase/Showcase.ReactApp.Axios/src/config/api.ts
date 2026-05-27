import { ApiClient } from '../api/client/ApiClient';
import { AccountsClient } from '../api/client/AccountsClient';
import { TasksClient } from '../api/client/TasksClient';
import { UsersClient } from '../api/client/UsersClient';
import { FilesClient } from '../api/client/FilesClient';
import { NotificationsClient } from '../api/client/NotificationsClient';
import { TestingClient } from '../api/client/TestingClient';

export const apiBaseUrl =
  import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:15046';

// Same demo token used by the Blazor app — the API accepts any bearer token
// with relaxed validation (no issuer/audience/lifetime/signing key checks).
const demoToken =
  'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJkZW1vLXVzZXIiLCJuYW1lIjoiRGVtbyBVc2VyIiwiaWF0IjoxNzE2MjM5MDIyfQ.demo-signature';

const apiClient = new ApiClient(apiBaseUrl + '/api/v1', {
  getAccessToken: () => demoToken,
});

export const accountsClient = new AccountsClient(apiClient);
export const tasksClient = new TasksClient(apiClient);
export const usersClient = new UsersClient(apiClient);
export const filesClient = new FilesClient(apiClient);
export const notificationsClient = new NotificationsClient(apiClient);
export const testingClient = new TestingClient(apiClient);