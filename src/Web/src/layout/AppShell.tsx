import LogoutIcon from '@mui/icons-material/Logout';
import MenuIcon from '@mui/icons-material/Menu';
import {
  AppBar,
  Box,
  Button,
  Container,
  IconButton,
  Stack,
  Toolbar,
  Typography,
  useMediaQuery,
} from '@mui/material';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Outlet } from 'react-router-dom';
import { useAuth } from '../auth/useAuth';
import { useCurrentUser } from '../auth/useCurrentUser';
import { AppNav } from './Nav';
import logoUrl from '../assets/logo.png';

const drawerWidth = 280;
export function AppShell() {
  const { t } = useTranslation();
  const { signOut } = useAuth();
  const currentUser = useCurrentUser();
  const isDesktop = useMediaQuery('(min-width:1080px)');
  const [mobileOpen, setMobileOpen] = useState(false);
  const displayName = currentUser.data?.displayName ?? '';
  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', bgcolor: 'background.default' }}>
      <Button
        href="#main-content"
        sx={{ position: 'absolute', left: -10000, '&:focus': { left: 16, top: 16, zIndex: 2000 } }}
      >
        {t('app.skipToContent')}
      </Button>
      <AppBar position="fixed" sx={{ zIndex: (muiTheme) => muiTheme.zIndex.drawer + 1 }}>
        <Toolbar>
          {!isDesktop ? (
            <IconButton
              color="inherit"
              edge="start"
              onClick={() => setMobileOpen(true)}
              aria-label={t('nav.open')}
              sx={{ mr: 1 }}
            >
              <MenuIcon />
            </IconButton>
          ) : null}
          <Box
            component="img"
            src={logoUrl}
            alt={t('app.name')}
            sx={{ height: 40, width: 'auto', display: 'block' }}
          />
          <Box sx={{ flexGrow: 1 }} />
          <Stack direction="row" spacing={2} sx={{ alignItems: 'center' }}>
            {displayName ? (
              <Typography variant="body2" sx={{ display: { xs: 'none', sm: 'block' } }}>
                {t('auth.signedInAs', { name: displayName })}
              </Typography>
            ) : null}
            <Button color="inherit" startIcon={<LogoutIcon />} onClick={() => void signOut()}>
              {t('auth.signOut')}
            </Button>
          </Stack>
        </Toolbar>
      </AppBar>
      <AppNav
        drawerWidth={drawerWidth}
        mobileOpen={mobileOpen}
        desktopOpen={isDesktop}
        onMobileClose={() => setMobileOpen(false)}
      />
      <Box
        component="main"
        id="main-content"
        sx={{ flexGrow: 1, pt: 10, width: '100%' }}
        tabIndex={-1}
      >
        <Container maxWidth="xl" sx={{ pb: 4 }}>
          <Outlet />
        </Container>
      </Box>
    </Box>
  );
}
