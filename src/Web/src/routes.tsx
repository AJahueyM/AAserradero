import { Navigate, createBrowserRouter } from 'react-router-dom';
import { ProtectedRoute } from './auth/ProtectedRoute';
import { RequireFeatureAccess } from './features/RequireFeatureAccess';
import { featureRoutes } from './features/routes';
import { AppShell } from './layout/AppShell';
import { LoginPage } from './layout/LoginPage';
import { NotFoundPage } from './layout/NotFoundPage';

export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <AppShell />,
        children: [
          { index: true, element: <Navigate to="/reservations" replace /> },
          ...featureRoutes.map((route) => ({
            path: route.path,
            element: <RequireFeatureAccess route={route}>{route.element}</RequireFeatureAccess>,
          })),
          { path: '*', element: <NotFoundPage /> },
        ],
      },
    ],
  },
]);
