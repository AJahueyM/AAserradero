import { type IPublicClientApplication } from '@azure/msal-browser';
import { MsalProvider } from '@azure/msal-react';
import { CssBaseline, ThemeProvider } from '@mui/material';
import { LocalizationProvider } from '@mui/x-date-pickers';
import { AdapterDateFns } from '@mui/x-date-pickers/AdapterDateFns';
import { QueryClientProvider } from '@tanstack/react-query';
import { es } from 'date-fns/locale/es';
import { RouterProvider } from 'react-router-dom';
import { queryClient } from '../api/queryClient';
import { AuthProvider } from '../auth/AuthProvider';
import { router } from '../routes';
import { theme } from '../theme/theme';
import { ConfirmProvider } from '../ui/ConfirmProvider';
import { LoadingProvider } from '../ui/LoadingProvider';
import { NotifyProvider } from '../ui/NotifyProvider';

interface AppProps {
  msalInstance: IPublicClientApplication;
}

export function App({ msalInstance }: AppProps) {
  return (
    <MsalProvider instance={msalInstance}>
      <AuthProvider>
        <QueryClientProvider client={queryClient}>
          <ThemeProvider theme={theme}>
            <LocalizationProvider dateAdapter={AdapterDateFns} adapterLocale={es}>
              <CssBaseline />
              <LoadingProvider>
                <NotifyProvider>
                  <ConfirmProvider>
                    <RouterProvider router={router} />
                  </ConfirmProvider>
                </NotifyProvider>
              </LoadingProvider>
            </LocalizationProvider>
          </ThemeProvider>
        </QueryClientProvider>
      </AuthProvider>
    </MsalProvider>
  );
}
