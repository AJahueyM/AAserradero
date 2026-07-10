import AdminPanelSettingsIcon from '@mui/icons-material/AdminPanelSettings';
import AssignmentTurnedInIcon from '@mui/icons-material/AssignmentTurnedIn';
import BarChartIcon from '@mui/icons-material/BarChart';
import CalendarMonthIcon from '@mui/icons-material/CalendarMonth';
import GavelIcon from '@mui/icons-material/Gavel';
import Inventory2Icon from '@mui/icons-material/Inventory2';
import { type SvgIconComponent } from '@mui/icons-material';
import { Box, CircularProgress } from '@mui/material';
import { Suspense, lazy } from 'react';
import { type Capability } from '../auth/capabilities';
import { RegulationPage } from '../layout/RegulationPage';

// Feature pages are code-split and lazy-loaded. Each feature agent implements the default-export
// page at the path below; this registry file is NOT edited by feature agents.
const ReservationsPage = lazy(() => import('./reservations/ReservationsPage'));
const CheckInOutPage = lazy(() => import('./checkinout/CheckInOutPage'));
const CatalogPage = lazy(() => import('./catalog/CatalogPage'));
const ReportsPage = lazy(() => import('./reports/ReportsPage'));
const AdministrationPage = lazy(() => import('./administration/AdministrationPage'));

function withSuspense(node: React.ReactNode): React.ReactNode {
  return (
    <Suspense
      fallback={
        <Box sx={{ display: 'flex', justifyContent: 'center', p: 6 }}>
          <CircularProgress />
        </Box>
      }
    >
      {node}
    </Suspense>
  );
}

export type CapabilityRequirement =
  | { mode: 'always' }
  | { mode: 'all'; capabilities: Capability[] }
  | { mode: 'any'; capabilities: Capability[] };
export interface FeatureRoute {
  id: string;
  path: string;
  labelKey: string;
  descriptionKey?: string;
  icon: SvgIconComponent;
  requirement: CapabilityRequirement;
  element: React.ReactNode;
}
export function requireAllCapabilities(...capabilities: Capability[]): CapabilityRequirement {
  return { mode: 'all', capabilities };
}
export function requireAnyCapability(...capabilities: Capability[]): CapabilityRequirement {
  return { mode: 'any', capabilities };
}
const bothManagementCapabilities = requireAllCapabilities('Catalog.Manage', 'Reservations.Manage');

export const featureRoutes: FeatureRoute[] = [
  {
    id: 'reservations',
    path: 'reservations',
    labelKey: 'nav.reservations',
    descriptionKey: 'routes.reservationsDescription',
    icon: CalendarMonthIcon,
    requirement: { mode: 'always' },
    element: withSuspense(<ReservationsPage />),
  },
  {
    id: 'check-in-out',
    path: 'check-in-out',
    labelKey: 'nav.checkInOut',
    descriptionKey: 'routes.checkInOutDescription',
    icon: AssignmentTurnedInIcon,
    requirement: requireAllCapabilities('Reservations.Manage'),
    element: withSuspense(<CheckInOutPage />),
  },
  {
    id: 'catalog',
    path: 'catalog',
    labelKey: 'nav.catalog',
    descriptionKey: 'routes.catalogDescription',
    icon: Inventory2Icon,
    requirement: requireAllCapabilities('Catalog.Manage'),
    element: withSuspense(<CatalogPage />),
  },
  {
    id: 'reports',
    path: 'reports',
    labelKey: 'nav.reports',
    descriptionKey: 'routes.reportsDescription',
    icon: BarChartIcon,
    requirement: bothManagementCapabilities,
    element: withSuspense(<ReportsPage />),
  },
  {
    id: 'administration',
    path: 'administration',
    labelKey: 'nav.administration',
    descriptionKey: 'routes.administrationDescription',
    icon: AdminPanelSettingsIcon,
    requirement: bothManagementCapabilities,
    element: withSuspense(<AdministrationPage />),
  },
  {
    id: 'regulation',
    path: 'regulation',
    labelKey: 'nav.regulation',
    icon: GavelIcon,
    requirement: { mode: 'always' },
    element: <RegulationPage />,
  },
];

export function canAccessRoute(route: FeatureRoute, capabilities: Capability[]) {
  switch (route.requirement.mode) {
    case 'always':
      return true;
    case 'all':
      return route.requirement.capabilities.every((capability) =>
        capabilities.includes(capability),
      );
    case 'any':
      return route.requirement.capabilities.some((capability) => capabilities.includes(capability));
  }
}
export function getVisibleFeatureRoutes(capabilities: Capability[]) {
  return featureRoutes.filter((route) => canAccessRoute(route, capabilities));
}
