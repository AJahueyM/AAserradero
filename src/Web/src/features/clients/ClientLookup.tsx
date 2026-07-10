import BlockIcon from '@mui/icons-material/Block';
import StarIcon from '@mui/icons-material/Star';
import {
  Alert,
  Autocomplete,
  Box,
  Button,
  Checkbox,
  Chip,
  FormControlLabel,
  Stack,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ApiError } from '../../api/apiError';
import { useNotify } from '../../ui/NotifyProvider';
import {
  createClient,
  searchClients,
  type ClientDto,
  type CreateClientRequest,
} from './clientsApi';
import { useDebouncedValue } from './useDebouncedValue';

type Mode = 'existing' | 'new';

export interface ClientLookupProps {
  value: ClientDto | null;
  onChange: (client: ClientDto | null) => void;
  disabled?: boolean;
}

/**
 * Shared inline client selector/creator. Consumers pass the currently selected client and an
 * onChange callback; this widget handles searching existing clients (debounced, VIP filter,
 * VIP/blacklist cues) and capturing a new client. Do not modify — extend via props if needed.
 */
export function ClientLookup({ value, onChange, disabled }: ClientLookupProps) {
  const { t } = useTranslation();
  const notify = useNotify();
  const [mode, setMode] = useState<Mode>('existing');
  const [inputValue, setInputValue] = useState('');
  const [vipOnly, setVipOnly] = useState(false);
  const debouncedSearch = useDebouncedValue(inputValue, 300);

  const searchQuery = useQuery({
    queryKey: ['client-search', debouncedSearch, vipOnly],
    queryFn: () =>
      searchClients({ name: debouncedSearch, isVip: vipOnly ? true : undefined, pageSize: 20 }),
    enabled: mode === 'existing' && debouncedSearch.trim().length > 0,
    staleTime: 30_000,
  });

  const options = useMemo(() => {
    const items = searchQuery.data?.items ?? [];
    if (value && !items.some((client) => client.id === value.id)) return [value, ...items];
    return items;
  }, [searchQuery.data, value]);

  const [form, setForm] = useState<CreateClientRequest>({ name: '', cellphone: '' });
  const [errors, setErrors] = useState<{ name?: boolean; cellphone?: boolean }>({});

  const createMutation = useMutation({
    mutationFn: createClient,
    onSuccess: (client) => {
      onChange(client);
      notify.success(t('clients.created'));
      setForm({ name: '', cellphone: '' });
      setMode('existing');
    },
    onError: (error) =>
      notify.error(error instanceof ApiError ? error.message : t('clients.createError')),
  });

  const handleCreate = () => {
    const nextErrors = { name: !form.name.trim(), cellphone: !form.cellphone.trim() };
    setErrors(nextErrors);
    if (nextErrors.name || nextErrors.cellphone) return;
    createMutation.mutate({
      ...form,
      name: form.name.trim(),
      cellphone: form.cellphone.trim(),
    });
  };

  return (
    <Stack spacing={2}>
      <ToggleButtonGroup
        exclusive
        size="small"
        value={mode}
        disabled={disabled}
        onChange={(_, next: Mode | null) => next && setMode(next)}
        aria-label={t('clients.modeLabel')}
      >
        <ToggleButton value="existing">{t('clients.existing')}</ToggleButton>
        <ToggleButton value="new">{t('clients.new')}</ToggleButton>
      </ToggleButtonGroup>

      {mode === 'existing' ? (
        <Stack spacing={1}>
          <Autocomplete<ClientDto>
            value={value}
            options={options}
            loading={searchQuery.isFetching}
            loadingText={t('ui.loading')}
            disabled={disabled}
            getOptionLabel={(option) => option.name}
            isOptionEqualToValue={(a, b) => a.id === b.id}
            filterOptions={(current) => current}
            onChange={(_, next) => onChange(next)}
            inputValue={inputValue}
            onInputChange={(_, next) => setInputValue(next)}
            noOptionsText={debouncedSearch ? t('clients.noResults') : t('clients.searchHint')}
            renderOption={(props, option) => {
              const { key, ...rest } = props;
              return (
                <li key={key} {...rest}>
                  <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
                    <span>{option.name}</span>
                    {option.isVip && (
                      <Chip
                        size="small"
                        color="secondary"
                        icon={<StarIcon />}
                        label={t('clients.vip')}
                      />
                    )}
                    {option.isBlacklisted && (
                      <Chip
                        size="small"
                        color="error"
                        icon={<BlockIcon />}
                        label={t('clients.blacklisted')}
                      />
                    )}
                  </Box>
                </li>
              );
            }}
            renderInput={(params) => (
              <TextField {...params} label={t('clients.searchLabel')} />
            )}
          />
          <FormControlLabel
            control={
              <Checkbox checked={vipOnly} onChange={(event) => setVipOnly(event.target.checked)} />
            }
            label={t('clients.vipFilter')}
          />
          {value && (
            <Box>
              {value.isBlacklisted && (
                <Alert severity="warning" icon={<BlockIcon />} sx={{ mb: 1 }}>
                  {t('clients.blacklistWarning', { reason: value.blacklistReason ?? '' })}
                </Alert>
              )}
              <Typography variant="body2" color="text.secondary">
                {t('clients.recentActivity', { count: value.recentActivityCount })}
              </Typography>
              <Typography variant="body2">
                {value.cellphone}
                {value.email ? ` · ${value.email}` : ''}
              </Typography>
            </Box>
          )}
        </Stack>
      ) : (
        <Stack spacing={2}>
          <TextField
            label={t('clients.name')}
            required
            value={form.name}
            error={errors.name}
            onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))}
          />
          <TextField
            label={t('clients.cellphone')}
            required
            value={form.cellphone}
            error={errors.cellphone}
            onChange={(event) =>
              setForm((current) => ({ ...current, cellphone: event.target.value }))
            }
          />
          <TextField
            label={t('clients.phone')}
            value={form.phone ?? ''}
            onChange={(event) => setForm((current) => ({ ...current, phone: event.target.value }))}
          />
          <TextField
            label={t('clients.email')}
            type="email"
            value={form.email ?? ''}
            onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))}
          />
          <TextField
            label={t('clients.taxId')}
            value={form.taxId ?? ''}
            onChange={(event) => setForm((current) => ({ ...current, taxId: event.target.value }))}
          />
          <TextField
            label={t('clients.address')}
            value={form.address ?? ''}
            onChange={(event) =>
              setForm((current) => ({ ...current, address: event.target.value }))
            }
          />
          <Button variant="contained" onClick={handleCreate} disabled={createMutation.isPending}>
            {t('clients.save')}
          </Button>
        </Stack>
      )}
    </Stack>
  );
}
