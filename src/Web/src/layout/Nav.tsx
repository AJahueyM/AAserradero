import {
  Box,
  Divider,
  Drawer,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Toolbar,
} from '@mui/material';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useLocation } from 'react-router-dom';
import { useCurrentUser } from '../auth/useCurrentUser';
import { getVisibleFeatureRoutes } from '../features/routes';

interface AppNavProps {
  drawerWidth: number;
  mobileOpen: boolean;
  desktopOpen: boolean;
  onMobileClose: () => void;
}
export function AppNav({ drawerWidth, mobileOpen, desktopOpen, onMobileClose }: AppNavProps) {
  const { t } = useTranslation();
  const location = useLocation();
  const currentUser = useCurrentUser();
  const routes = getVisibleFeatureRoutes(currentUser.data?.capabilities ?? []);
  const content = (
    <Box role="navigation" aria-label={t('nav.open')}>
      <Toolbar />
      <Divider />
      <List>
        {routes.map((route) => {
          const Icon = route.icon;
          const to = `/${route.path}`;
          const selected = location.pathname === to || location.pathname.startsWith(`${to}/`);
          return (
            <ListItem key={route.id} disablePadding>
              <ListItemButton
                component={RouterLink}
                to={to}
                selected={selected}
                onClick={onMobileClose}
              >
                <ListItemIcon>
                  <Icon color={selected ? 'primary' : 'inherit'} aria-hidden="true" />
                </ListItemIcon>
                <ListItemText primary={t(route.labelKey)} />
              </ListItemButton>
            </ListItem>
          );
        })}
      </List>
    </Box>
  );
  return (
    <Box component="nav" sx={{ width: desktopOpen ? drawerWidth : 0, flexShrink: 0 }}>
      <Drawer
        variant="temporary"
        open={mobileOpen}
        onClose={onMobileClose}
        ModalProps={{ keepMounted: true }}
        sx={{ display: desktopOpen ? 'none' : 'block', '& .MuiDrawer-paper': { width: drawerWidth } }}
      >
        {content}
      </Drawer>
      <Drawer
        variant="permanent"
        open={desktopOpen}
        sx={{
          display: desktopOpen ? 'block' : 'none',
          '& .MuiDrawer-paper': { width: drawerWidth, boxSizing: 'border-box' },
        }}
      >
        {content}
      </Drawer>
    </Box>
  );
}
