import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
} from '@mui/material';
import { createContext, useCallback, useContext, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

export interface ConfirmOptions {
  title?: string;
  description?: string;
  confirmLabel?: string;
  cancelLabel?: string;
}
type ConfirmFn = (options?: ConfirmOptions) => Promise<boolean>;
const ConfirmContext = createContext<ConfirmFn | null>(null);
interface PendingConfirmation extends ConfirmOptions {
  resolve: (value: boolean) => void;
}

export function ConfirmProvider({ children }: { children: React.ReactNode }) {
  const { t } = useTranslation();
  const [pending, setPending] = useState<PendingConfirmation | null>(null);
  const confirm = useCallback<ConfirmFn>(
    (options = {}) => new Promise<boolean>((resolve) => setPending({ ...options, resolve })),
    [],
  );
  const close = useCallback(
    (result: boolean) => {
      pending?.resolve(result);
      setPending(null);
    },
    [pending],
  );
  const value = useMemo(() => confirm, [confirm]);
  return (
    <ConfirmContext.Provider value={value}>
      {children}
      <Dialog
        open={Boolean(pending)}
        onClose={() => close(false)}
        aria-labelledby="confirm-dialog-title"
        aria-describedby="confirm-dialog-description"
      >
        <DialogTitle id="confirm-dialog-title">
          {pending?.title ?? t('ui.confirmTitle')}
        </DialogTitle>
        <DialogContent>
          <DialogContentText id="confirm-dialog-description">
            {pending?.description ?? t('ui.confirmDescription')}
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => close(false)}>{pending?.cancelLabel ?? t('ui.cancel')}</Button>
          <Button color="primary" variant="contained" onClick={() => close(true)} autoFocus>
            {pending?.confirmLabel ?? t('ui.confirm')}
          </Button>
        </DialogActions>
      </Dialog>
    </ConfirmContext.Provider>
  );
}
export function useConfirm() {
  const context = useContext(ConfirmContext);
  if (!context) throw new Error('useConfirm must be used within ConfirmProvider.');
  return context;
}
