import {
  Alert,
  Box,
  Button,
  Checkbox,
  FormControlLabel,
  LinearProgress,
  MenuItem,
  Paper,
  Stack,
  Tab,
  Tabs,
  TextField,
  Typography,
} from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ApiError, isApiErrorBody } from '../../api/apiError';
import { apiGet, getAccessTokenForApi, toApiUrl } from '../../api/httpClient';
import { useCurrentUser } from '../../auth/useCurrentUser';
import { useConfirm } from '../../ui/ConfirmProvider';
import { useNotify } from '../../ui/NotifyProvider';
import './i18n';

const REQUIRED_CAPABILITIES = ['Catalog.Manage', 'Reservations.Manage'] as const;
const MONTHLY = 0;
const ANNUAL = 1;
const WEEKDAYS = [
  { value: 0, key: 'sunday' },
  { value: 1, key: 'monday' },
  { value: 2, key: 'tuesday' },
  { value: 3, key: 'wednesday' },
  { value: 4, key: 'thursday' },
  { value: 5, key: 'friday' },
  { value: 6, key: 'saturday' },
] as const;

interface ExportMetadata {
  entities: ExportEntityMetadata[];
}

interface ExportEntityMetadata {
  entity: string;
  fields: string[];
}

interface OccupancyExportRequest {
  periodType: number;
  year: number;
  month: number | null;
  weekdays: number[];
}

function hasRequiredCapabilities(capabilities: readonly string[]) {
  return REQUIRED_CAPABILITIES.every((capability) => capabilities.includes(capability));
}

function getErrorMessage(error: unknown, fallback: string) {
  return error instanceof ApiError ? error.message : fallback;
}

async function getExportMetadata() {
  return apiGet<ExportMetadata>('/api/reports/exports/metadata');
}

async function postDownload(path: string, body: unknown, fallbackName: string) {
  const token = await getAccessTokenForApi();
  const response = await fetch(toApiUrl(path), {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${token}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(body),
  });

  if (!response.ok) throw await parseDownloadError(response);

  const blob = await response.blob();
  const filename = getFilename(response.headers.get('content-disposition')) ?? fallbackName;
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

async function parseDownloadError(response: Response) {
  try {
    const payload = (await response.json()) as unknown;
    if (isApiErrorBody(payload)) {
      return new ApiError({
        code: payload.error.code,
        message: payload.error.message,
        details: payload.error.details,
        status: response.status,
      });
    }
  } catch {
    // Fall through to a generic API error.
  }
  return new ApiError({
    code: 'Reports.DownloadFailed',
    message: response.statusText || 'No se pudo generar el archivo.',
    status: response.status,
  });
}

function getFilename(contentDisposition: string | null) {
  if (!contentDisposition) return null;
  const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(contentDisposition);
  if (utf8Match?.[1]) return decodeURIComponent(utf8Match[1]);
  const asciiMatch = /filename="?([^";]+)"?/i.exec(contentDisposition);
  return asciiMatch?.[1] ?? null;
}

function timestamp() {
  return new Date().toISOString().replace(/[:.]/g, '-');
}

export default function ReportsPage() {
  const { t } = useTranslation();
  const currentUser = useCurrentUser();
  const [tab, setTab] = useState(0);
  const canManage = hasRequiredCapabilities(currentUser.data?.capabilities ?? []);

  if (currentUser.isLoading) return <Alert severity="info">{t('ui.loading')}</Alert>;

  return (
    <Stack spacing={3}>
      <Box>
        <Typography variant="h4" component="h1">
          {t('reports.title')}
        </Typography>
        <Typography color="text.secondary">{t('reports.subtitle')}</Typography>
      </Box>
      {!canManage ? (
        <Alert severity="warning">{t('reports.accessDenied')}</Alert>
      ) : (
        <>
          <Tabs value={tab} onChange={(_, next: number) => setTab(next)} aria-label={t('reports.tabsLabel')}>
            <Tab label={t('reports.export.tab')} />
            <Tab label={t('reports.occupancy.tab')} />
          </Tabs>
          {tab === 0 && <DataExport />}
          {tab === 1 && <OccupancyReport />}
        </>
      )}
    </Stack>
  );
}

function DataExport() {
  const { t } = useTranslation();
  const notify = useNotify();
  const confirm = useConfirm();
  const metadataQuery = useQuery({ queryKey: ['export-metadata'], queryFn: getExportMetadata });
  const [selectedFields, setSelectedFields] = useState<Record<string, string[]>>({});
  const [generatingEntity, setGeneratingEntity] = useState<string | null>(null);

  const entities = metadataQuery.data?.entities ?? [];

  const toggleEntity = (entity: ExportEntityMetadata, checked: boolean) => {
    setSelectedFields((current) => ({
      ...current,
      [entity.entity]: checked ? entity.fields : [],
    }));
  };

  const toggleField = (entity: string, field: string, checked: boolean) => {
    setSelectedFields((current) => {
      const currentFields = current[entity] ?? [];
      const next = checked
        ? Array.from(new Set([...currentFields, field]))
        : currentFields.filter((candidate) => candidate !== field);
      return { ...current, [entity]: next };
    });
  };

  const handleExport = async (entity: string) => {
    const fields = selectedFields[entity] ?? [];
    if (fields.length === 0) {
      notify.warning(t('reports.export.selectFields'));
      return;
    }
    const accepted = await confirm({
      title: t('reports.export.confirmTitle'),
      description: t('reports.export.confirmDescription', { entity }),
    });
    if (!accepted) return;

    setGeneratingEntity(entity);
    try {
      await postDownload('/api/reports/exports', { entity, fields }, `${entity}-${timestamp()}.xlsx`);
      notify.success(t('reports.export.generated'));
    } catch (error) {
      notify.error(getErrorMessage(error, t('reports.export.generateError')));
    } finally {
      setGeneratingEntity(null);
    }
  };

  return (
    <Paper sx={{ p: 3 }}>
      <Stack spacing={2}>
        <Typography variant="h6">{t('reports.export.heading')}</Typography>
        <Typography color="text.secondary">{t('reports.export.description')}</Typography>
        {metadataQuery.isError && (
          <Alert severity="error">
            {getErrorMessage(metadataQuery.error, t('reports.export.metadataError'))}
          </Alert>
        )}
        {metadataQuery.isFetching && <LinearProgress aria-label={t('ui.loading')} />}
        {entities.map((entity) => {
          const selected = selectedFields[entity.entity] ?? [];
          const allSelected = selected.length === entity.fields.length && entity.fields.length > 0;
          const partiallySelected = selected.length > 0 && !allSelected;
          return (
            <Paper key={entity.entity} variant="outlined" sx={{ p: 2 }}>
              <Stack spacing={1}>
                <Stack
                  direction={{ xs: 'column', sm: 'row' }}
                  spacing={2}
                  sx={{ alignItems: { sm: 'center' }, justifyContent: 'space-between' }}
                >
                  <FormControlLabel
                    control={
                      <Checkbox
                        checked={allSelected}
                        indeterminate={partiallySelected}
                        disabled={generatingEntity !== null}
                        onChange={(event) => toggleEntity(entity, event.target.checked)}
                      />
                    }
                    label={
                      <Box>
                        <Typography sx={{ fontWeight: 600 }}>{entity.entity}</Typography>
                        <Typography variant="body2" color="text.secondary">
                          {t('reports.export.fieldCount', { count: entity.fields.length })}
                        </Typography>
                      </Box>
                    }
                  />
                  <Button
                    variant="contained"
                    disabled={generatingEntity !== null || selected.length === 0}
                    onClick={() => void handleExport(entity.entity)}
                  >
                    {generatingEntity === entity.entity
                      ? t('reports.export.generating')
                      : t('reports.export.download')}
                  </Button>
                </Stack>
                <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap' }}>
                  {entity.fields.map((field) => (
                    <FormControlLabel
                      key={field}
                      control={
                        <Checkbox
                          size="small"
                          checked={selected.includes(field)}
                          disabled={generatingEntity !== null}
                          onChange={(event) =>
                            toggleField(entity.entity, field, event.target.checked)
                          }
                        />
                      }
                      label={field}
                    />
                  ))}
                </Stack>
              </Stack>
            </Paper>
          );
        })}
        {entities.length === 0 && !metadataQuery.isFetching && (
          <Alert severity="info">{t('reports.export.empty')}</Alert>
        )}
      </Stack>
    </Paper>
  );
}

function OccupancyReport() {
  const { t } = useTranslation();
  const notify = useNotify();
  const confirm = useConfirm();
  const now = useMemo(() => new Date(), []);
  const [periodType, setPeriodType] = useState<number>(MONTHLY);
  const [year, setYear] = useState(String(now.getFullYear()));
  const [month, setMonth] = useState(String(now.getMonth() + 1));
  const [weekdays, setWeekdays] = useState<number[]>([1, 2, 3, 4, 5, 6, 0]);
  const [generating, setGenerating] = useState(false);
  const [validation, setValidation] = useState('');

  const toggleWeekday = (weekday: number, checked: boolean) => {
    setWeekdays((current) =>
      checked ? Array.from(new Set([...current, weekday])) : current.filter((day) => day !== weekday),
    );
  };

  const buildRequest = (): OccupancyExportRequest | null => {
    const parsedYear = Number(year);
    const parsedMonth = Number(month);
    if (!Number.isInteger(parsedYear) || parsedYear < 2000 || parsedYear > 2100) {
      setValidation(t('reports.occupancy.yearRequired'));
      return null;
    }
    if (periodType === MONTHLY && (!Number.isInteger(parsedMonth) || parsedMonth < 1 || parsedMonth > 12)) {
      setValidation(t('reports.occupancy.monthRequired'));
      return null;
    }
    if (weekdays.length === 0) {
      setValidation(t('reports.occupancy.weekdaysRequired'));
      return null;
    }
    setValidation('');
    return {
      periodType,
      year: parsedYear,
      month: periodType === MONTHLY ? parsedMonth : null,
      weekdays,
    };
  };

  const handleGenerate = async () => {
    const request = buildRequest();
    if (!request) return;
    const accepted = await confirm({
      title: t('reports.occupancy.confirmTitle'),
      description: t('reports.occupancy.confirmDescription'),
    });
    if (!accepted) return;

    setGenerating(true);
    try {
      const label =
        periodType === MONTHLY ? `${request.year}-${String(request.month).padStart(2, '0')}` : request.year;
      await postDownload(
        '/api/reports/occupancy-financial/export',
        request,
        `ocupacion-finanzas-${label}-${timestamp()}.xlsx`,
      );
      notify.success(t('reports.occupancy.generated'));
    } catch (error) {
      notify.error(getErrorMessage(error, t('reports.occupancy.generateError')));
    } finally {
      setGenerating(false);
    }
  };

  return (
    <Paper sx={{ p: 3 }}>
      <Stack spacing={2}>
        <Typography variant="h6">{t('reports.occupancy.heading')}</Typography>
        <Typography color="text.secondary">{t('reports.occupancy.description')}</Typography>
        {generating && <LinearProgress aria-label={t('reports.occupancy.generating')} />}
        {validation && <Alert severity="warning">{validation}</Alert>}
        <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
          <TextField
            select
            label={t('reports.occupancy.periodType')}
            value={String(periodType)}
            disabled={generating}
            onChange={(event) => setPeriodType(Number(event.target.value))}
            sx={{ minWidth: 200 }}
          >
            <MenuItem value={String(MONTHLY)}>{t('reports.occupancy.monthly')}</MenuItem>
            <MenuItem value={String(ANNUAL)}>{t('reports.occupancy.annual')}</MenuItem>
          </TextField>
          <TextField
            label={t('reports.occupancy.year')}
            type="number"
            value={year}
            disabled={generating}
            onChange={(event) => setYear(event.target.value)}
          />
          {periodType === MONTHLY && (
            <TextField
              select
              label={t('reports.occupancy.month')}
              value={month}
              disabled={generating}
              onChange={(event) => setMonth(event.target.value)}
              sx={{ minWidth: 180 }}
            >
              {Array.from({ length: 12 }, (_, index) => index + 1).map((value) => (
                <MenuItem key={value} value={String(value)}>
                  {t(`reports.months.${value}`)}
                </MenuItem>
              ))}
            </TextField>
          )}
        </Stack>
        <Box>
          <Typography sx={{ fontWeight: 600 }}>{t('reports.occupancy.weekdays')}</Typography>
          <Typography variant="body2" color="text.secondary">
            {t('reports.occupancy.weekdaysHelp')}
          </Typography>
          <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', mt: 1 }}>
            {WEEKDAYS.map((weekday) => (
              <FormControlLabel
                key={weekday.value}
                control={
                  <Checkbox
                    checked={weekdays.includes(weekday.value)}
                    disabled={generating}
                    onChange={(event) => toggleWeekday(weekday.value, event.target.checked)}
                  />
                }
                label={t(`reports.weekdays.${weekday.key}`)}
              />
            ))}
          </Stack>
        </Box>
        <Box>
          <Button variant="contained" disabled={generating} onClick={() => void handleGenerate()}>
            {generating ? t('reports.occupancy.generating') : t('reports.occupancy.download')}
          </Button>
        </Box>
      </Stack>
    </Paper>
  );
}
