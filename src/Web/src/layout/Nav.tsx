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
  onMobileClose: () => void;
}
export function AppNav({ drawerWidth, mobileOpen, onMobileClose }: AppNavProps) {
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
    <Box component="nav" sx={{ width: { md: drawerWidth }, flexShrink: { md: 0 } }}>
      <Drawer
        variant="temporary"
        open={mobileOpen}
        onClose={onMobileClose}
        ModalProps={{ keepMounted: true }}
        sx={{ display: { xs: 'block', md: 'none' }, '& .MuiDrawer-paper': { width: drawerWidth } }}
      >
        {content}
      </Drawer>
      <Drawer
        variant="permanent"
        open
        sx={{
          display: { xs: 'none', md: 'block' },
          '& .MuiDrawer-paper': { width: drawerWidth, boxSizing: 'border-box' },
        }}
      >
        {content}
      </Drawer>
    </Box>
  );
}
