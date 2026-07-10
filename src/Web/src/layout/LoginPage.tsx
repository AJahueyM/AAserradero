import LoginIcon from '@mui/icons-material/Login';
import { Alert, Box, Button, CircularProgress, Paper, Stack, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../auth/useAuth';

export function LoginPage() {
  const { t } = useTranslation();
  const location = useLocation();
  const { isAuthenticated, isAuthBusy, loginError, signIn } = useAuth();
  if (isAuthenticated) return <Navigate to={getRedirectPath(location.state)} replace />;
  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'grid',
        placeItems: 'center',
        bgcolor: 'background.default',
        p: 2,
      }}
    >
      <Paper elevation={4} sx={{ width: '100%', maxWidth: 480, p: { xs: 3, sm: 5 } }}>
        <Stack spacing={3} sx={{ alignItems: 'stretch' }}>
          <Box>
            <Typography component="p" variant="overline" color="primary">
              {t('app.name')}
            </Typography>
            <Typography component="h1" variant="h4">
              {t('auth.signInTitle')}
            </Typography>
            <Typography color="text.secondary" sx={{ mt: 1 }}>
              {t('auth.signInSubtitle')}
            </Typography>
          </Box>
          {loginError ? <LoginErrorAlert kind={loginError.kind} /> : null}
          <Button
            size="large"
            variant="contained"
            startIcon={isAuthBusy ? <CircularProgress size={20} color="inherit" /> : <LoginIcon />}
            onClick={() => void signIn()}
            disabled={isAuthBusy}
            aria-busy={isAuthBusy}
          >
            {isAuthBusy ? t('auth.signingIn') : t('auth.signInButton')}
          </Button>
        </Stack>
      </Paper>
    </Box>
  );
}
function LoginErrorAlert({ kind }: { kind: string }) {
  const { t } = useTranslation();
  const messageKey =
    kind === 'invalidCredentials'
      ? 'auth.invalidCredentials'
      : kind === 'authUnavailable'
        ? 'auth.authUnavailable'
        : 'auth.unknownAuthError';
  return (
    <Alert severity="error" role="alert">
      {t(messageKey)}
    </Alert>
  );
}
function getRedirectPath(state: unknown) {
  if (
    typeof state === 'object' &&
    state !== null &&
    'from' in state &&
    typeof state.from === 'object' &&
    state.from !== null &&
    'pathname' in state.from &&
    typeof state.from.pathname === 'string'
  )
    return state.from.pathname;
  return '/reservations';
}
