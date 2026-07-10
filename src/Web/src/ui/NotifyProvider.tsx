import { Alert, Snackbar, type AlertColor } from '@mui/material';
import { createContext, useCallback, useContext, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

export type NotifySeverity = Extract<AlertColor, 'error' | 'success' | 'info' | 'warning'>;
export interface NotifyOptions {
  message: string;
  severity?: NotifySeverity;
  autoHideDuration?: number;
}
interface QueuedNotification extends Required<NotifyOptions> {
  id: number;
}
interface NotifyContextValue {
  notify: (options: NotifyOptions) => void;
  error: (message: string) => void;
  success: (message: string) => void;
  info: (message: string) => void;
  warning: (message: string) => void;
}
const NotifyContext = createContext<NotifyContextValue | null>(null);

export function NotifyProvider({ children }: { children: React.ReactNode }) {
  const { t } = useTranslation();
  const [queue, setQueue] = useState<QueuedNotification[]>([]);
  const notify = useCallback(
    (options: NotifyOptions) =>
      setQueue((current) => [
        ...current,
        {
          id: Date.now() + current.length,
          message: options.message,
          severity: options.severity ?? 'info',
          autoHideDuration: options.autoHideDuration ?? 5000,
        },
      ]),
    [],
  );
  const value = useMemo<NotifyContextValue>(
    () => ({
      notify,
      error: (message) => notify({ message, severity: 'error' }),
      success: (message) => notify({ message, severity: 'success' }),
      info: (message) => notify({ message, severity: 'info' }),
      warning: (message) => notify({ message, severity: 'warning' }),
    }),
    [notify],
  );
  const active = queue[0];
  const dismissActive = useCallback(() => setQueue((current) => current.slice(1)), []);
  return (
    <NotifyContext.Provider value={value}>
      {children}
      <Snackbar
        key={active?.id}
        open={Boolean(active)}
        autoHideDuration={active?.autoHideDuration}
        onClose={dismissActive}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        {active ? (
          <Alert
            onClose={dismissActive}
            severity={active.severity}
            variant="filled"
            role="status"
            aria-label={active.message}
            closeText={t('ui.close')}
            sx={{ width: '100%' }}
          >
            {active.message}
          </Alert>
        ) : undefined}
      </Snackbar>
    </NotifyContext.Provider>
  );
}
export function useNotify() {
  const context = useContext(NotifyContext);
  if (!context) throw new Error('useNotify must be used within NotifyProvider.');
  return context;
}
