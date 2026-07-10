import { Navigate } from 'react-router-dom';
import { useCurrentUser } from '../auth/useCurrentUser';
import { canAccessRoute, type FeatureRoute } from './routes';

interface RequireFeatureAccessProps {
  route: FeatureRoute;
  children: React.ReactNode;
}

export function RequireFeatureAccess({ route, children }: RequireFeatureAccessProps) {
  const currentUser = useCurrentUser();
  const capabilities = currentUser.data?.capabilities ?? [];

  if (!canAccessRoute(route, capabilities)) {
    return <Navigate to="/reservations" replace />;
  }

  return children;
}
