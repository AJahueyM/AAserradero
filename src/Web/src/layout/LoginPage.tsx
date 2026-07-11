import LoginIcon from '@mui/icons-material/Login';
import { Alert, Box, Button, CircularProgress, Paper, Stack, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../auth/useAuth';
import logoUrl from '../assets/logo.png';

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
        p: 2,
        background: (theme) =>
          `radial-gradient(1200px 600px at 50% -15%, ${theme.palette.primary.dark}26, transparent 60%),` +
          ` linear-gradient(160deg, ${theme.palette.background.default} 0%, #eef1e5 100%)`,
      }}
    >
      <Paper
        elevation={6}
        sx={{
          width: '100%',
          maxWidth: 460,
          p: { xs: 3, sm: 5 },
          borderTop: (theme) => `6px solid ${theme.palette.primary.main}`,
        }}
      >
        <Stack spacing={3} sx={{ alignItems: 'stretch' }}>
          <Box
            component="img"
            src={logoUrl}
            alt={t('app.name')}
            sx={{ width: '100%', maxWidth: 300, height: 'auto', alignSelf: 'center' }}
          />
          <Box sx={{ textAlign: 'center' }}>
            <Typography component="h1" variant="h5">
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
