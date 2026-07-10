import { InteractionStatus } from '@azure/msal-browser';
import { useIsAuthenticated, useMsal } from '@azure/msal-react';
import { Alert, Box, CircularProgress } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { ApiError } from '../api/apiError';
import { useCurrentUser } from './useCurrentUser';

export function ProtectedRoute() {
  const isAuthenticated = useIsAuthenticated();
  const { inProgress } = useMsal();
  const location = useLocation();
  const { t } = useTranslation();
  const currentUser = useCurrentUser();
  if (inProgress !== InteractionStatus.None) return <CenteredProgress label={t('auth.loading')} />;
  if (!isAuthenticated) return <Navigate to="/login" state={{ from: location }} replace />;
  if (currentUser.isLoading) return <CenteredProgress label={t('auth.loadingProfile')} />;
  if (currentUser.error) {
    if (currentUser.error instanceof ApiError && currentUser.error.status === 401) return null;
    return (
      <Box sx={{ p: 3 }}>
        <Alert severity="error">{t('auth.profileError')}</Alert>
      </Box>
    );
  }
  return <Outlet />;
}

function CenteredProgress({ label }: { label: string }) {
  return (
    <Box
      role="status"
      aria-live="polite"
      sx={{ minHeight: '100vh', display: 'grid', placeItems: 'center' }}
    >
      <CircularProgress aria-label={label} />
    </Box>
  );
}
