import {
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  FormControlLabel,
  Paper,
  Stack,
  Switch,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tabs,
  TextField,
  Typography,
} from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ApiError } from '../../api/apiError';
import { apiDelete, apiGet, apiPost, apiPut } from '../../api/httpClient';
import { useCurrentUser } from '../../auth/useCurrentUser';
import { formatLocal } from '../../lib/datetime';
import { useConfirm } from '../../ui/ConfirmProvider';
import { useNotify } from '../../ui/NotifyProvider';
import { ClientLookup } from '../clients/ClientLookup';
import type { ClientDto } from '../clients/clientsApi';
import './i18n';

const REQUIRED_CAPABILITIES = ['Catalog.Manage', 'Reservations.Manage'] as const;

interface ClientUpdateRequest {
  name: string;
  taxId: string | null;
  address: string | null;
  email: string | null;
  phone: string | null;
  cellphone: string;
  isVip: boolean;
  isBlacklisted: boolean;
  blacklistReason: string | null;
}

interface ConfigValueDto {
  key: string;
  value: string;
  updatedAt: string;
}

interface StaffUserDto {
  id: number;
  displayName: string;
  userName: string;
  isActive: boolean;
  assignedCapabilities: string[];
}

interface StaffUserListResponse {
  items: StaffUserDto[];
}

interface CreateStaffUserRequest {
  email: string;
  displayName: string;
  initialPassword: string;
}

interface ResetPasswordRequest {
  newPassword: string;
  forceChangePasswordNextSignIn: boolean;
}

type FieldErrors = Record<string, string>;

function hasRequiredCapabilities(capabilities: readonly string[]) {
  return REQUIRED_CAPABILITIES.every((capability) => capabilities.includes(capability));
}

function nullableText(value: string) {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

function getErrorMessage(error: unknown, fallback: string) {
  return error instanceof ApiError ? error.message : fallback;
}

function detailsToFieldErrors(details: unknown): FieldErrors {
  if (!details || typeof details !== 'object') return {};
  const record = details as Record<string, unknown>;
  const result: FieldErrors = {};
  for (const [key, value] of Object.entries(record)) {
    if (Array.isArray(value)) result[key[0]?.toLowerCase() + key.slice(1)] = value.join(' ');
    if (typeof value === 'string') result[key[0]?.toLowerCase() + key.slice(1)] = value;
  }
  const errors = record.errors;
  if (errors && typeof errors === 'object') {
    for (const [key, value] of Object.entries(errors as Record<string, unknown>)) {
      if (Array.isArray(value)) result[key[0]?.toLowerCase() + key.slice(1)] = value.join(' ');
    }
  }
  const field = record.field;
  if (typeof field === 'string' && typeof record.message === 'string') {
    result[field[0]?.toLowerCase() + field.slice(1)] = record.message;
  }
  return result;
}

async function getClient(id: number) {
  return apiGet<ClientDto>(`/api/clients/${id}`);
}

async function updateClient(id: number, body: ClientUpdateRequest) {
  return apiPut<ClientUpdateRequest, ClientDto>(`/api/clients/${id}`, body);
}

async function getPaymentInstructions() {
  return apiGet<ConfigValueDto>('/api/config/PaymentInstructions');
}

async function updatePaymentInstructions(value: string) {
  return apiPut<{ value: string }, ConfigValueDto>('/api/config/PaymentInstructions', { value });
}

async function listUsers() {
  return apiGet<StaffUserListResponse>('/api/users');
}

async function createUser(body: CreateStaffUserRequest) {
  return apiPost<CreateStaffUserRequest, StaffUserDto>('/api/users', body);
}

async function updateUser(id: number, body: { displayName?: string; isActive?: boolean }) {
  return apiPut<typeof body, StaffUserDto>(`/api/users/${id}`, body);
}

async function setCapability(id: number, capability: string, assigned: boolean) {
  if (assigned) {
    return apiPost<{ capability: string }, StaffUserDto>(`/api/users/${id}/capabilities`, {
      capability,
    });
  }
  return apiDelete<StaffUserDto>(
    `/api/users/${id}/capabilities/${encodeURIComponent(capability)}`,
  );
}

async function resetPassword(id: number, body: ResetPasswordRequest) {
  return apiPost<ResetPasswordRequest, void>(`/api/users/${id}/password`, body);
}

async function disableUser(id: number) {
  return apiDelete<void>(`/api/users/${id}`);
}

export default function AdministrationPage() {
  const { t } = useTranslation();
  const currentUser = useCurrentUser();
  const [tab, setTab] = useState(0);
  const canManage = hasRequiredCapabilities(currentUser.data?.capabilities ?? []);

  if (currentUser.isLoading) return <Alert severity="info">{t('ui.loading')}</Alert>;

  return (
    <Stack spacing={3}>
      <Box>
        <Typography variant="h4" component="h1">
          {t('administration.title')}
        </Typography>
        <Typography color="text.secondary">{t('administration.subtitle')}</Typography>
      </Box>
      {!canManage ? (
        <Alert severity="warning">{t('administration.accessDenied')}</Alert>
      ) : (
        <>
          <Tabs
            value={tab}
            onChange={(_, next: number) => setTab(next)}
            aria-label={t('administration.tabsLabel')}
          >
            <Tab label={t('administration.clients.tab')} />
            <Tab label={t('administration.payment.tab')} />
            <Tab label={t('administration.users.tab')} />
          </Tabs>
          {tab === 0 && <ClientAdministration />}
          {tab === 1 && <PaymentInstructions />}
          {tab === 2 && <UserManagement />}
        </>
      )}
    </Stack>
  );
}

function ClientAdministration() {
  const { t } = useTranslation();
  const [selectedClient, setSelectedClient] = useState<ClientDto | null>(null);

  const clientQuery = useQuery({
    queryKey: ['administration-client', selectedClient?.id],
    queryFn: () => getClient(selectedClient!.id),
    enabled: selectedClient !== null,
  });

  return (
    <Paper sx={{ p: 3 }}>
      <Stack spacing={3}>
        <ClientLookup
          value={selectedClient}
          onChange={setSelectedClient}
        />
        {clientQuery.isError && (
          <Alert severity="error">
            {getErrorMessage(clientQuery.error, t('administration.clients.loadError'))}
          </Alert>
        )}
        {clientQuery.isFetching && <Alert severity="info">{t('ui.loading')}</Alert>}
        {clientQuery.data ? (
          <ClientEditForm key={clientQuery.data.id} client={clientQuery.data} onSaved={setSelectedClient} />
        ) : (
          <Alert severity="info">{t('administration.clients.empty')}</Alert>
        )}
      </Stack>
    </Paper>
  );
}

function toClientForm(client: ClientDto): ClientUpdateRequest {
  return {
    name: client.name,
    taxId: client.taxId,
    address: client.address,
    email: client.email,
    phone: client.phone,
    cellphone: client.cellphone,
    isVip: client.isVip,
    isBlacklisted: client.isBlacklisted,
    blacklistReason: client.blacklistReason,
  };
}

function ClientEditForm({
  client,
  onSaved,
}: {
  client: ClientDto;
  onSaved: (client: ClientDto) => void;
}) {
  const { t } = useTranslation();
  const notify = useNotify();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [form, setForm] = useState<ClientUpdateRequest>(() => toClientForm(client));
  const [errors, setErrors] = useState<FieldErrors>({});

  const saveMutation = useMutation({
    mutationFn: (body: ClientUpdateRequest) => updateClient(client.id, body),
    onSuccess: (savedClient) => {
      setForm(toClientForm(savedClient));
      onSaved(savedClient);
      void queryClient.invalidateQueries({ queryKey: ['administration-client', savedClient.id] });
      notify.success(t('administration.clients.saved'));
    },
    onError: (error) => {
      if (error instanceof ApiError) setErrors(detailsToFieldErrors(error.details));
      notify.error(getErrorMessage(error, t('administration.clients.saveError')));
    },
  });

  const setField = <K extends keyof ClientUpdateRequest>(field: K, value: ClientUpdateRequest[K]) =>
    setForm((current) => ({ ...current, [field]: value }));

  const handleSave = async () => {
    const nextErrors: FieldErrors = {};
    if (!form.name.trim()) nextErrors.name = t('administration.validation.required');
    if (!form.cellphone.trim()) nextErrors.cellphone = t('administration.validation.required');
    if (form.isBlacklisted && !form.blacklistReason?.trim()) {
      nextErrors.blacklistReason = t('administration.clients.blacklistReasonRequired');
    }
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) return;

    const accepted = await confirm({
      title: t('administration.clients.confirmTitle'),
      description: t('administration.clients.confirmDescription'),
    });
    if (!accepted) return;

    saveMutation.mutate({
      ...form,
      name: form.name.trim(),
      cellphone: form.cellphone.trim(),
      taxId: nullableText(form.taxId ?? ''),
      address: nullableText(form.address ?? ''),
      email: nullableText(form.email ?? ''),
      phone: nullableText(form.phone ?? ''),
      blacklistReason: form.isBlacklisted ? nullableText(form.blacklistReason ?? '') : null,
    });
  };

  return (
          <Stack spacing={2}>
            <TextField
              label={t('administration.clients.name')}
              required
              value={form.name}
              error={Boolean(errors.name)}
              helperText={errors.name}
              disabled={saveMutation.isPending}
              onChange={(event) => setField('name', event.target.value)}
            />
            <TextField
              label={t('administration.clients.taxId')}
              value={form.taxId ?? ''}
              error={Boolean(errors.taxId)}
              helperText={errors.taxId}
              disabled={saveMutation.isPending}
              onChange={(event) => setField('taxId', event.target.value)}
            />
            <TextField
              label={t('administration.clients.address')}
              value={form.address ?? ''}
              error={Boolean(errors.address)}
              helperText={errors.address}
              disabled={saveMutation.isPending}
              multiline
              minRows={2}
              onChange={(event) => setField('address', event.target.value)}
            />
            <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
              <TextField
                fullWidth
                type="email"
                label={t('administration.clients.email')}
                value={form.email ?? ''}
                error={Boolean(errors.email)}
                helperText={errors.email}
                disabled={saveMutation.isPending}
                onChange={(event) => setField('email', event.target.value)}
              />
              <TextField
                fullWidth
                label={t('administration.clients.phone')}
                value={form.phone ?? ''}
                error={Boolean(errors.phone)}
                helperText={errors.phone}
                disabled={saveMutation.isPending}
                onChange={(event) => setField('phone', event.target.value)}
              />
              <TextField
                fullWidth
                required
                label={t('administration.clients.cellphone')}
                value={form.cellphone}
                error={Boolean(errors.cellphone)}
                helperText={errors.cellphone}
                disabled={saveMutation.isPending}
                onChange={(event) => setField('cellphone', event.target.value)}
              />
            </Stack>
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
              <FormControlLabel
                control={
                  <Switch
                    checked={form.isVip}
                    disabled={saveMutation.isPending}
                    onChange={(event) => setField('isVip', event.target.checked)}
                  />
                }
                label={t('administration.clients.vip')}
              />
              <FormControlLabel
                control={
                  <Switch
                    checked={form.isBlacklisted}
                    disabled={saveMutation.isPending}
                    onChange={(event) => setField('isBlacklisted', event.target.checked)}
                  />
                }
                label={t('administration.clients.blacklisted')}
              />
            </Stack>
            <TextField
              label={t('administration.clients.blacklistReason')}
              required={form.isBlacklisted}
              value={form.blacklistReason ?? ''}
              error={Boolean(errors.blacklistReason)}
              helperText={
                errors.blacklistReason ??
                (form.isBlacklisted ? t('administration.clients.blacklistReasonHelp') : '')
              }
              disabled={saveMutation.isPending || !form.isBlacklisted}
              multiline
              minRows={2}
              onChange={(event) => setField('blacklistReason', event.target.value)}
            />
            <Box>
              <Button variant="contained" onClick={handleSave} disabled={saveMutation.isPending}>
                {saveMutation.isPending ? t('administration.saving') : t('administration.save')}
              </Button>
            </Box>
          </Stack>
  );
}

function PaymentInstructions() {
  const { t } = useTranslation();
  const configQuery = useQuery({
    queryKey: ['payment-instructions'],
    queryFn: getPaymentInstructions,
  });

  return (
    <Paper sx={{ p: 3 }}>
      <Stack spacing={2}>
        <Typography variant="h6">{t('administration.payment.heading')}</Typography>
        <Typography color="text.secondary">{t('administration.payment.description')}</Typography>
        {configQuery.isError && (
          <Alert severity="error">
            {getErrorMessage(configQuery.error, t('administration.payment.loadError'))}
          </Alert>
        )}
        {configQuery.isFetching && <Alert severity="info">{t('ui.loading')}</Alert>}
        {configQuery.data && <PaymentInstructionsForm config={configQuery.data} />}
      </Stack>
    </Paper>
  );
}

function PaymentInstructionsForm({ config }: { config: ConfigValueDto }) {
  const { t } = useTranslation();
  const confirm = useConfirm();
  const notify = useNotify();
  const [value, setValue] = useState(config.value);

  const saveMutation = useMutation({
    mutationFn: updatePaymentInstructions,
    onSuccess: (savedConfig) => {
      setValue(savedConfig.value);
      notify.success(t('administration.payment.saved'));
    },
    onError: (error) =>
      notify.error(getErrorMessage(error, t('administration.payment.saveError'))),
  });

  const updatedAt = config.updatedAt;

  return (
    <>
        {updatedAt && !updatedAt.startsWith('0001-') && (
          <Typography variant="body2" color="text.secondary">
            {t('administration.payment.updatedAt', {
              date: formatLocal(updatedAt, 'PPpp'),
            })}
          </Typography>
        )}
        <TextField
          label={t('administration.payment.textLabel')}
          value={value}
          disabled={saveMutation.isPending}
          multiline
          minRows={8}
          helperText={t('administration.payment.helper', { count: value.length })}
          onChange={(event) => setValue(event.target.value)}
        />
        <Box>
          <Button
            variant="contained"
            disabled={saveMutation.isPending}
            onClick={async () => {
              const accepted = await confirm({
                title: t('administration.payment.confirmTitle'),
                description: t('administration.payment.confirmDescription'),
              });
              if (accepted) saveMutation.mutate(value);
            }}
          >
            {saveMutation.isPending ? t('administration.saving') : t('administration.save')}
          </Button>
        </Box>
    </>
  );
}

function UserManagement() {
  const { t } = useTranslation();
  const notify = useNotify();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [createOpen, setCreateOpen] = useState(false);
  const [resetUser, setResetUser] = useState<StaffUserDto | null>(null);
  const [selectedUser, setSelectedUser] = useState<StaffUserDto | null>(null);
  const [displayName, setDisplayName] = useState('');
  const [isActive, setIsActive] = useState(true);
  const usersQuery = useQuery({ queryKey: ['staff-users'], queryFn: listUsers });

  const refreshUsers = () => queryClient.invalidateQueries({ queryKey: ['staff-users'] });

  const updateMutation = useMutation({
    mutationFn: ({ id, body }: { id: number; body: { displayName?: string; isActive?: boolean } }) =>
      updateUser(id, body),
    onSuccess: (user) => {
      setSelectedUser(user);
      setDisplayName(user.displayName);
      setIsActive(user.isActive);
      void refreshUsers();
      notify.success(t('administration.users.updated'));
    },
    onError: (error) => notify.error(getErrorMessage(error, t('administration.users.updateError'))),
  });

  const capabilityMutation = useMutation({
    mutationFn: ({ id, capability, assigned }: { id: number; capability: string; assigned: boolean }) =>
      setCapability(id, capability, assigned),
    onSuccess: (user) => {
      if (selectedUser?.id === user.id) {
        setSelectedUser(user);
        setDisplayName(user.displayName);
        setIsActive(user.isActive);
      }
      void refreshUsers();
      notify.success(t('administration.users.capabilityUpdated'));
    },
    onError: (error) =>
      notify.error(getErrorMessage(error, t('administration.users.capabilityError'))),
  });

  const disableMutation = useMutation({
    mutationFn: disableUser,
    onSuccess: () => {
      setSelectedUser(null);
      void refreshUsers();
      notify.success(t('administration.users.disabled'));
    },
    onError: (error) => notify.error(getErrorMessage(error, t('administration.users.disableError'))),
  });

  const users = usersQuery.data?.items ?? [];
  const busy = updateMutation.isPending || capabilityMutation.isPending || disableMutation.isPending;

  return (
    <Paper sx={{ p: 3 }}>
      <Stack spacing={2}>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ justifyContent: 'space-between' }}>
          <Box>
            <Typography variant="h6">{t('administration.users.heading')}</Typography>
            <Typography color="text.secondary">{t('administration.users.description')}</Typography>
          </Box>
          <Button variant="contained" onClick={() => setCreateOpen(true)}>
            {t('administration.users.create')}
          </Button>
        </Stack>
        {usersQuery.isError && (
          <Alert severity="error">
            {getErrorMessage(usersQuery.error, t('administration.users.loadError'))}
          </Alert>
        )}
        {usersQuery.isFetching && <Alert severity="info">{t('ui.loading')}</Alert>}
        <TableContainer>
          <Table aria-label={t('administration.users.tableLabel')} size="small">
            <TableHead>
              <TableRow>
                <TableCell>{t('administration.users.name')}</TableCell>
                <TableCell>{t('administration.users.userName')}</TableCell>
                <TableCell>{t('administration.users.status')}</TableCell>
                <TableCell>{t('administration.users.capabilities')}</TableCell>
                <TableCell align="right">{t('administration.users.actions')}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {users.map((user) => (
                <TableRow key={user.id} selected={selectedUser?.id === user.id}>
                  <TableCell>{user.displayName}</TableCell>
                  <TableCell>{user.userName}</TableCell>
                  <TableCell>
                    <Chip
                      size="small"
                      color={user.isActive ? 'success' : 'default'}
                      label={
                        user.isActive
                          ? t('administration.users.active')
                          : t('administration.users.inactive')
                      }
                    />
                  </TableCell>
                  <TableCell>
                    <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap' }}>
                      {REQUIRED_CAPABILITIES.map((capability) => (
                        <FormControlLabel
                          key={capability}
                          control={
                            <Checkbox
                              checked={user.assignedCapabilities.includes(capability)}
                              disabled={busy}
                              onChange={async (event) => {
                                const accepted = await confirm({
                                  title: t('administration.users.confirmCapabilityTitle'),
                                  description: t('administration.users.confirmCapabilityDescription', {
                                    capability,
                                    user: user.displayName,
                                  }),
                                });
                                if (accepted) {
                                  capabilityMutation.mutate({
                                    id: user.id,
                                    capability,
                                    assigned: event.target.checked,
                                  });
                                }
                              }}
                            />
                          }
                          label={capability}
                        />
                      ))}
                    </Stack>
                  </TableCell>
                  <TableCell align="right">
                    <Stack direction="row" spacing={1} sx={{ justifyContent: 'flex-end' }}>
                      <Button
                        size="small"
                        onClick={() => {
                          setSelectedUser(user);
                          setDisplayName(user.displayName);
                          setIsActive(user.isActive);
                        }}
                      >
                        {t('administration.users.edit')}
                      </Button>
                      <Button size="small" onClick={() => setResetUser(user)}>
                        {t('administration.users.resetPassword')}
                      </Button>
                      <Button
                        size="small"
                        color="error"
                        disabled={busy || !user.isActive}
                        onClick={async () => {
                          const accepted = await confirm({
                            title: t('administration.users.confirmDisableTitle'),
                            description: t('administration.users.confirmDisableDescription', {
                              user: user.displayName,
                            }),
                            confirmLabel: t('administration.users.disable'),
                          });
                          if (accepted) disableMutation.mutate(user.id);
                        }}
                      >
                        {t('administration.users.disable')}
                      </Button>
                    </Stack>
                  </TableCell>
                </TableRow>
              ))}
              {users.length === 0 && !usersQuery.isFetching && (
                <TableRow>
                  <TableCell colSpan={5}>
                    <Alert severity="info">{t('administration.users.empty')}</Alert>
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
        {selectedUser && (
          <>
            <Divider />
            <Stack spacing={2} component="section" aria-label={t('administration.users.editPanel')}>
              <Typography variant="h6">
                {t('administration.users.editing', { user: selectedUser.displayName })}
              </Typography>
              <TextField
                label={t('administration.users.name')}
                value={displayName}
                disabled={busy}
                onChange={(event) => setDisplayName(event.target.value)}
              />
              <FormControlLabel
                control={
                  <Switch
                    checked={isActive}
                    disabled={busy}
                    onChange={(event) => setIsActive(event.target.checked)}
                  />
                }
                label={t('administration.users.active')}
              />
              <Stack direction="row" spacing={1}>
                <Button
                  variant="contained"
                  disabled={busy || !displayName.trim()}
                  onClick={async () => {
                    const accepted = await confirm({
                      title: t('administration.users.confirmUpdateTitle'),
                      description: t('administration.users.confirmUpdateDescription'),
                    });
                    if (accepted) {
                      updateMutation.mutate({
                        id: selectedUser.id,
                        body: { displayName: displayName.trim(), isActive },
                      });
                    }
                  }}
                >
                  {t('administration.save')}
                </Button>
                <Button onClick={() => setSelectedUser(null)}>{t('ui.cancel')}</Button>
              </Stack>
            </Stack>
          </>
        )}
      </Stack>
      <CreateUserDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onCreated={() => void refreshUsers()}
      />
      <ResetPasswordDialog
        user={resetUser}
        onClose={() => setResetUser(null)}
      />
    </Paper>
  );
}

function CreateUserDialog({
  open,
  onClose,
  onCreated,
}: {
  open: boolean;
  onClose: () => void;
  onCreated: () => void;
}) {
  const { t } = useTranslation();
  const notify = useNotify();
  const confirm = useConfirm();
  const [form, setForm] = useState<CreateStaffUserRequest>({
    email: '',
    displayName: '',
    initialPassword: '',
  });
  const [errors, setErrors] = useState<FieldErrors>({});

  const mutation = useMutation({
    mutationFn: createUser,
    onSuccess: () => {
      notify.success(t('administration.users.created'));
      setForm({ email: '', displayName: '', initialPassword: '' });
      setErrors({});
      onCreated();
      onClose();
    },
    onError: (error) => {
      if (error instanceof ApiError) setErrors(detailsToFieldErrors(error.details));
      notify.error(getErrorMessage(error, t('administration.users.createError')));
    },
  });

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>{t('administration.users.create')}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          <TextField
            label={t('administration.users.email')}
            type="email"
            required
            value={form.email}
            error={Boolean(errors.email)}
            helperText={errors.email}
            disabled={mutation.isPending}
            onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))}
          />
          <TextField
            label={t('administration.users.name')}
            required
            value={form.displayName}
            error={Boolean(errors.displayName)}
            helperText={errors.displayName}
            disabled={mutation.isPending}
            onChange={(event) =>
              setForm((current) => ({ ...current, displayName: event.target.value }))
            }
          />
          <TextField
            label={t('administration.users.initialPassword')}
            type="password"
            required
            value={form.initialPassword}
            error={Boolean(errors.initialPassword)}
            helperText={errors.initialPassword ?? t('administration.users.passwordHelp')}
            disabled={mutation.isPending}
            onChange={(event) =>
              setForm((current) => ({ ...current, initialPassword: event.target.value }))
            }
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{t('ui.cancel')}</Button>
        <Button
          variant="contained"
          disabled={mutation.isPending}
          onClick={async () => {
            const nextErrors: FieldErrors = {};
            if (!form.email.trim()) nextErrors.email = t('administration.validation.required');
            if (!form.displayName.trim()) {
              nextErrors.displayName = t('administration.validation.required');
            }
            if (form.initialPassword.length < 12) {
              nextErrors.initialPassword = t('administration.users.passwordHelp');
            }
            setErrors(nextErrors);
            if (Object.keys(nextErrors).length > 0) return;
            const accepted = await confirm({
              title: t('administration.users.confirmCreateTitle'),
              description: t('administration.users.confirmCreateDescription'),
            });
            if (accepted) {
              mutation.mutate({
                email: form.email.trim(),
                displayName: form.displayName.trim(),
                initialPassword: form.initialPassword,
              });
            }
          }}
        >
          {t('administration.users.create')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

function ResetPasswordDialog({
  user,
  onClose,
}: {
  user: StaffUserDto | null;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const notify = useNotify();
  const confirm = useConfirm();
  const [newPassword, setNewPassword] = useState('');
  const [forceChange, setForceChange] = useState(true);
  const [error, setError] = useState('');

  const mutation = useMutation({
    mutationFn: (body: ResetPasswordRequest) => resetPassword(user!.id, body),
    onSuccess: () => {
      notify.success(t('administration.users.passwordReset'));
      setNewPassword('');
      setForceChange(true);
      setError('');
      onClose();
    },
    onError: (apiError) =>
      notify.error(getErrorMessage(apiError, t('administration.users.passwordResetError'))),
  });

  return (
    <Dialog open={Boolean(user)} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>
        {t('administration.users.resetPasswordFor', { user: user?.displayName ?? '' })}
      </DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          <TextField
            label={t('administration.users.newPassword')}
            type="password"
            required
            value={newPassword}
            error={Boolean(error)}
            helperText={error || t('administration.users.passwordHelp')}
            disabled={mutation.isPending}
            onChange={(event) => setNewPassword(event.target.value)}
          />
          <FormControlLabel
            control={
              <Checkbox
                checked={forceChange}
                disabled={mutation.isPending}
                onChange={(event) => setForceChange(event.target.checked)}
              />
            }
            label={t('administration.users.forceChange')}
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{t('ui.cancel')}</Button>
        <Button
          variant="contained"
          disabled={mutation.isPending}
          onClick={async () => {
            if (newPassword.length < 12) {
              setError(t('administration.users.passwordHelp'));
              return;
            }
            const accepted = await confirm({
              title: t('administration.users.confirmResetTitle'),
              description: t('administration.users.confirmResetDescription'),
            });
            if (accepted) {
              mutation.mutate({
                newPassword,
                forceChangePasswordNextSignIn: forceChange,
              });
            }
          }}
        >
          {t('administration.users.resetPassword')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
