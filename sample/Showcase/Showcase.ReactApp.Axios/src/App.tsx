import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider } from 'react-router';
import { AppThemeProvider } from './theme/ThemeContext';
import { router } from './router';
import { ApiProvider } from './api/hooks/ApiProvider';
import { apiBaseUrl } from './config/api';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

const demoToken =
  'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJkZW1vLXVzZXIiLCJuYW1lIjoiRGVtbyBVc2VyIiwiaWF0IjoxNzE2MjM5MDIyfQ.demo-signature';

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ApiProvider baseUrl={apiBaseUrl + '/api/v1'} options={{ getAccessToken: () => demoToken }}>
        <AppThemeProvider>
          <RouterProvider router={router} />
        </AppThemeProvider>
      </ApiProvider>
    </QueryClientProvider>
  );
}
