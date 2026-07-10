import { Backdrop, CircularProgress, Stack, Typography } from '@mui/material';
import { createContext, useCallback, useContext, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

interface LoadingContextValue {
  showLoading: () => () => void;
  isLoading: boolean;
}
const LoadingContext = createContext<LoadingContextValue | null>(null);

export function LoadingProvider({ children }: { children: React.ReactNode }) {
  const { t } = useTranslation();
  const [count, setCount] = useState(0);
  const showLoading = useCallback(() => {
    setCount((value) => value + 1);
    let released = false;
    return () => {
      if (!released) {
        released = true;
        setCount((value) => Math.max(0, value - 1));
      }
    };
  }, []);
  const value = useMemo(() => ({ showLoading, isLoading: count > 0 }), [count, showLoading]);
  return (
    <LoadingContext.Provider value={value}>
      {children}
      <Backdrop open={count > 0} sx={{ color: '#fff', zIndex: (theme) => theme.zIndex.modal + 1 }}>
        <Stack spacing={2} sx={{ alignItems: 'center' }} role="status" aria-live="assertive">
          <CircularProgress color="inherit" aria-label={t('ui.loading')} />
          <Typography>{t('ui.loading')}</Typography>
        </Stack>
      </Backdrop>
    </LoadingContext.Provider>
  );
}
export function useLoading() {
  const context = useContext(LoadingContext);
  if (!context) throw new Error('useLoading must be used within LoadingProvider.');
  return context;
}
