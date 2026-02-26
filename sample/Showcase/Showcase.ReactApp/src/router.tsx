import { createBrowserRouter } from 'react-router';
import { AppLayout } from './layout/AppLayout';
import IndexPage from './pages/IndexPage';
import AccountsPage from './pages/AccountsPage';
import AccountsPaginatedPage from './pages/AccountsPaginatedPage';
import AccountsStreamingPage from './pages/AccountsStreamingPage';
import TasksPage from './pages/TasksPage';
import UsersPage from './pages/UsersPage';
import UserFormPage from './pages/UserFormPage';
import UserDetailsPage from './pages/UserDetailsPage';
import FilesPage from './pages/FilesPage';
import FilesUploadPage from './pages/FilesUploadPage';
import NotificationsPage from './pages/NotificationsPage';
import NotificationSubscriptionsPage from './pages/NotificationSubscriptionsPage';
import WebhookDemoPage from './pages/WebhookDemoPage';
import ExceptionTestingPage from './pages/ExceptionTestingPage';
import NotFoundPage from './pages/NotFoundPage';

export const router = createBrowserRouter([
  {
    element: <AppLayout />,
    children: [
      { path: '/', element: <IndexPage /> },
      { path: '/accounts', element: <AccountsPage /> },
      { path: '/accounts/paginated', element: <AccountsPaginatedPage /> },
      { path: '/accounts/async-enumerable', element: <AccountsStreamingPage /> },
      { path: '/tasks', element: <TasksPage /> },
      { path: '/users', element: <UsersPage /> },
      { path: '/users/create', element: <UserFormPage /> },
      { path: '/users/:userId', element: <UserDetailsPage /> },
      { path: '/users/:userId/edit', element: <UserFormPage /> },
      { path: '/files', element: <FilesPage /> },
      { path: '/files/upload', element: <FilesUploadPage /> },
      { path: '/notifications', element: <NotificationsPage /> },
      { path: '/notifications/subscriptions', element: <NotificationSubscriptionsPage /> },
      { path: '/webhooks/demo', element: <WebhookDemoPage /> },
      { path: '/testing/exceptions', element: <ExceptionTestingPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);
