import AddIcon from '@mui/icons-material/Add';
import EditIcon from '@mui/icons-material/Edit';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import {
  Alert,
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  IconButton,
  MenuItem,
  Paper,
  Stack,
  Switch,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  Tabs,
  TextField,
  Typography,
} from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ApiError } from '../../api/apiError';
import { apiGet, apiPost, apiPut } from '../../api/httpClient';
import { useCurrentUser } from '../../auth/useCurrentUser';
import { useConfirm } from '../../ui/ConfirmProvider';
import { useNotify } from '../../ui/NotifyProvider';
import './i18n';

type TabKey = 'areas' | 'rooms' | 'concepts';
interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}
interface AreaDto {
  id: number;
  name: string;
  checkInTime: string;
  checkOutTime: string;
  receptionOpenTime: string;
  receptionCloseTime: string;
  isActive: boolean;
}
interface UpsertAreaRequest {
  name: string;
  checkInTime: string;
  checkOutTime: string;
  receptionOpenTime: string;
  receptionCloseTime: string;
}
interface RoomDto {
  id: number;
  areaId: number;
  areaName: string;
  name: string;
  capacity: number;
  unitCount: number;
  nightlyFare: number;
  description: string | null;
  displayOrder: number;
  isActive: boolean;
}
interface UpsertRoomRequest {
  areaId: number;
  name: string;
  capacity: number;
  unitCount: number;
  nightlyFare: number;
  description: string | null;
  displayOrder: number;
}
interface ConceptDto {
  id: number;
  code: string;
  name: string;
  isDiscount: boolean;
  isProtected: boolean;
  isActive: boolean;
}
interface UpsertConceptRequest {
  code: string;
  name: string;
  isDiscount: boolean;
}
interface MutationWithWarnings<T> {
  area?: T;
  room?: T;
  warnings: string[];
}

type FieldErrors = Record<string, string>;
const PAGE_SIZE = 10;
const moneyFormatter = new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN' });

function listCatalog<T>(path: string, search: string, page: number) {
  const query = new URLSearchParams({ page: String(page + 1), pageSize: String(PAGE_SIZE) });
  if (search.trim()) query.set('search', search.trim());
  return apiGet<PagedResult<T>>(`${path}?${query.toString()}`);
}
function asApiMessage(error: unknown, fallback: string) {
  return error instanceof ApiError ? error.message : fallback;
}
function readFieldErrors(error: unknown): FieldErrors {
  if (!(error instanceof ApiError) || !error.details || typeof error.details !== 'object') return {};
  const details = error.details as Record<string, unknown>;
  const field = typeof details.field === 'string' ? details.field : undefined;
  return field ? { [field[0].toLowerCase() + field.slice(1)]: error.message } : {};
}
function timeValue(value: string) {
  return value.slice(0, 5);
}
function statusChip(isActive: boolean, t: (key: string) => string) {
  return <Chip size="small" color={isActive ? 'success' : 'default'} label={t(isActive ? 'catalog.active' : 'catalog.inactive')} />;
}

export default function CatalogPage() {
  const { t } = useTranslation();
  const currentUser = useCurrentUser();
  const canManage = currentUser.data?.capabilities.includes('Catalog.Manage') ?? false;
  const [tab, setTab] = useState<TabKey>('areas');

  return (
    <Stack spacing={3}>
      <Box>
        <Typography variant="h4" component="h1">{t('catalog.title')}</Typography>
        <Typography color="text.secondary">{t('catalog.subtitle')}</Typography>
      </Box>
      {!canManage && <Alert severity="info">{t('catalog.noPermission')}</Alert>}
      <Paper>
        <Tabs value={tab} onChange={(_, next: TabKey) => setTab(next)} aria-label={t('catalog.title')}>
          <Tab value="areas" label={t('catalog.areas')} />
          <Tab value="rooms" label={t('catalog.rooms')} />
          <Tab value="concepts" label={t('catalog.concepts')} />
        </Tabs>
      </Paper>
      {tab === 'areas' && <AreasSection canManage={canManage} />}
      {tab === 'rooms' && <RoomsSection canManage={canManage} />}
      {tab === 'concepts' && <ConceptsSection canManage={canManage} />}
    </Stack>
  );
}

function SectionToolbar({
  title,
  search,
  onSearch,
  onCreate,
  canManage,
}: {
  title: string;
  search: string;
  onSearch: (value: string) => void;
  onCreate: () => void;
  canManage: boolean;
}) {
  const { t } = useTranslation();
  return (
    <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ justifyContent: 'space-between' }}>
      <Typography variant="h5" component="h2">{title}</Typography>
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
        <TextField
          size="small"
          label={t('catalog.search')}
          value={search}
          onChange={(event) => onSearch(event.target.value)}

        />
        <Button variant="contained" startIcon={<AddIcon />} onClick={onCreate} disabled={!canManage}>
          {t('catalog.add')}
        </Button>
      </Stack>
    </Stack>
  );
}

function AreasSection({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation();
  const notify = useNotify();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(0);
  const [editing, setEditing] = useState<AreaDto | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const query = useQuery({ queryKey: ['catalog', 'areas', search, page], queryFn: () => listCatalog<AreaDto>('/api/areas', search, page) });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['catalog', 'areas'] });
  const deactivate = useMutation({
    mutationFn: (area: AreaDto) => apiPost<undefined, MutationWithWarnings<AreaDto>>(`/api/areas/${area.id}/deactivate`, undefined),
    onSuccess: (result) => {
      notify.success(t('catalog.deactivateSuccess'));
      result.warnings.forEach((warning) => notify.warning(warning));
      void invalidate();
    },
    onError: (error) => notify.error(asApiMessage(error, t('catalog.loadError'))),
  });
  const handleDeactivate = async (area: AreaDto) => {
    const accepted = await confirm({ title: t('catalog.confirmTitle'), description: t('catalog.confirmDescription'), confirmLabel: t('catalog.deactivate') });
    if (accepted) deactivate.mutate(area);
  };
  return (
    <Paper sx={{ p: 2 }}>
      <Stack spacing={2}>
        <SectionToolbar title={t('catalog.areas')} search={search} onSearch={(value) => { setSearch(value); setPage(0); }} onCreate={() => { setEditing(null); setDialogOpen(true); }} canManage={canManage} />
        <CatalogState query={query} colSpan={7} />
        {query.data && (
          <TableContainer>
            <Table size="small" aria-label={t('catalog.areas')}>
              <TableHead><TableRow><TableCell>{t('catalog.area.name')}</TableCell><TableCell>{t('catalog.area.checkInTime')}</TableCell><TableCell>{t('catalog.area.checkOutTime')}</TableCell><TableCell>{t('catalog.area.receptionOpenTime')}</TableCell><TableCell>{t('catalog.area.receptionCloseTime')}</TableCell><TableCell>{t('catalog.status')}</TableCell><TableCell align="right">{t('catalog.actions')}</TableCell></TableRow></TableHead>
              <TableBody>
                {query.data.items.map((area) => (
                  <TableRow key={area.id} hover>
                    <TableCell>{area.name}</TableCell><TableCell>{timeValue(area.checkInTime)}</TableCell><TableCell>{timeValue(area.checkOutTime)}</TableCell><TableCell>{timeValue(area.receptionOpenTime)}</TableCell><TableCell>{timeValue(area.receptionCloseTime)}</TableCell><TableCell>{statusChip(area.isActive, t)}</TableCell>
                    <TableCell align="right"><RowActions disabled={!canManage || deactivate.isPending} canDeactivate={area.isActive} onEdit={() => { setEditing(area); setDialogOpen(true); }} onDeactivate={() => void handleDeactivate(area)} /></TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
        <Pager count={query.data?.totalCount ?? 0} page={page} onPage={setPage} />
      </Stack>
      {dialogOpen && <AreaDialog key={editing?.id ?? 'new-area'} open={dialogOpen} area={editing} onClose={() => setDialogOpen(false)} onSaved={() => { setDialogOpen(false); void invalidate(); }} />}
    </Paper>
  );
}

function RoomsSection({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation();
  const notify = useNotify();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(0);
  const [editing, setEditing] = useState<RoomDto | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const query = useQuery({ queryKey: ['catalog', 'rooms', search, page], queryFn: () => listCatalog<RoomDto>('/api/rooms', search, page) });
  const areasQuery = useQuery({ queryKey: ['catalog', 'areas', 'all'], queryFn: () => listCatalog<AreaDto>('/api/areas', '', 0) });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['catalog', 'rooms'] });
  const deactivate = useMutation({
    mutationFn: (room: RoomDto) => apiPost<undefined, MutationWithWarnings<RoomDto>>(`/api/rooms/${room.id}/deactivate`, undefined),
    onSuccess: (result) => {
      notify.success(t('catalog.deactivateSuccess'));
      result.warnings.forEach((warning) => notify.warning(warning));
      void invalidate();
    },
    onError: (error) => notify.error(asApiMessage(error, t('catalog.loadError'))),
  });
  const handleDeactivate = async (room: RoomDto) => {
    const accepted = await confirm({ title: t('catalog.confirmTitle'), description: t('catalog.confirmDescription'), confirmLabel: t('catalog.deactivate') });
    if (accepted) deactivate.mutate(room);
  };
  return (
    <Paper sx={{ p: 2 }}>
      <Stack spacing={2}>
        <SectionToolbar title={t('catalog.rooms')} search={search} onSearch={(value) => { setSearch(value); setPage(0); }} onCreate={() => { setEditing(null); setDialogOpen(true); }} canManage={canManage} />
        <CatalogState query={query} colSpan={8} />
        {query.data && (
          <TableContainer>
            <Table size="small" aria-label={t('catalog.rooms')}>
              <TableHead><TableRow><TableCell>{t('catalog.room.area')}</TableCell><TableCell>{t('catalog.room.name')}</TableCell><TableCell align="right">{t('catalog.room.capacity')}</TableCell><TableCell align="right">{t('catalog.room.unitCount')}</TableCell><TableCell align="right">{t('catalog.room.nightlyFare')}</TableCell><TableCell>{t('catalog.room.description')}</TableCell><TableCell>{t('catalog.status')}</TableCell><TableCell align="right">{t('catalog.actions')}</TableCell></TableRow></TableHead>
              <TableBody>
                {query.data.items.map((room) => (
                  <TableRow key={room.id} hover><TableCell>{room.areaName}</TableCell><TableCell>{room.name}</TableCell><TableCell align="right">{room.capacity}</TableCell><TableCell align="right">{room.unitCount}</TableCell><TableCell align="right">{moneyFormatter.format(room.nightlyFare)}</TableCell><TableCell>{room.description}</TableCell><TableCell>{statusChip(room.isActive, t)}</TableCell><TableCell align="right"><RowActions disabled={!canManage || deactivate.isPending} canDeactivate={room.isActive} onEdit={() => { setEditing(room); setDialogOpen(true); }} onDeactivate={() => void handleDeactivate(room)} /></TableCell></TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
        <Pager count={query.data?.totalCount ?? 0} page={page} onPage={setPage} />
      </Stack>
      {dialogOpen && <RoomDialog key={editing?.id ?? 'new-room'} open={dialogOpen} room={editing} areas={areasQuery.data?.items ?? []} onClose={() => setDialogOpen(false)} onSaved={() => { setDialogOpen(false); void invalidate(); }} />}
    </Paper>
  );
}

function ConceptsSection({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation();
  const notify = useNotify();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(0);
  const [editing, setEditing] = useState<ConceptDto | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const query = useQuery({ queryKey: ['catalog', 'concepts', search, page], queryFn: () => listCatalog<ConceptDto>('/api/concepts', search, page) });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['catalog', 'concepts'] });
  const deactivate = useMutation({
    mutationFn: (concept: ConceptDto) => apiPost<undefined, ConceptDto>(`/api/concepts/${concept.id}/deactivate`, undefined),
    onSuccess: () => { notify.success(t('catalog.deactivateSuccess')); void invalidate(); },
    onError: (error) => notify.error(asApiMessage(error, t('catalog.loadError'))),
  });
  const handleDeactivate = async (concept: ConceptDto) => {
    if (concept.isProtected) { notify.warning(t('catalog.confirmProtected')); return; }
    const accepted = await confirm({ title: t('catalog.confirmTitle'), description: t('catalog.confirmDescription'), confirmLabel: t('catalog.deactivate') });
    if (accepted) deactivate.mutate(concept);
  };
  return (
    <Paper sx={{ p: 2 }}>
      <Stack spacing={2}>
        <SectionToolbar title={t('catalog.concepts')} search={search} onSearch={(value) => { setSearch(value); setPage(0); }} onCreate={() => { setEditing(null); setDialogOpen(true); }} canManage={canManage} />
        <CatalogState query={query} colSpan={6} />
        {query.data && (
          <TableContainer><Table size="small" aria-label={t('catalog.concepts')}><TableHead><TableRow><TableCell>{t('catalog.concept.code')}</TableCell><TableCell>{t('catalog.concept.name')}</TableCell><TableCell>{t('catalog.concept.kind')}</TableCell><TableCell>{t('catalog.protected')}</TableCell><TableCell>{t('catalog.status')}</TableCell><TableCell align="right">{t('catalog.actions')}</TableCell></TableRow></TableHead><TableBody>{query.data.items.map((concept) => (<TableRow key={concept.id} hover><TableCell>{concept.code}</TableCell><TableCell>{concept.name}</TableCell><TableCell><Chip size="small" color={concept.isDiscount ? 'secondary' : 'primary'} label={t(concept.isDiscount ? 'catalog.concept.discount' : 'catalog.concept.charge')} /></TableCell><TableCell>{concept.isProtected ? <Chip size="small" icon={<WarningAmberIcon />} label={t('catalog.protected')} /> : '—'}</TableCell><TableCell>{statusChip(concept.isActive, t)}</TableCell><TableCell align="right"><RowActions disabled={!canManage || deactivate.isPending} canDeactivate={concept.isActive && !concept.isProtected} onEdit={() => { setEditing(concept); setDialogOpen(true); }} onDeactivate={() => void handleDeactivate(concept)} /></TableCell></TableRow>))}</TableBody></Table></TableContainer>
        )}
        <Pager count={query.data?.totalCount ?? 0} page={page} onPage={setPage} />
      </Stack>
      {dialogOpen && <ConceptDialog key={editing?.id ?? 'new-concept'} open={dialogOpen} concept={editing} onClose={() => setDialogOpen(false)} onSaved={() => { setDialogOpen(false); void invalidate(); }} />}
    </Paper>
  );
}

function CatalogState<T>({ query, colSpan }: { query: { isLoading: boolean; isError: boolean; data?: PagedResult<T> }; colSpan: number }) {
  const { t } = useTranslation();
  if (query.isLoading) return <Alert severity="info">{t('catalog.loading')}</Alert>;
  if (query.isError) return <Alert severity="error">{t('catalog.loadError')}</Alert>;
  if (query.data?.items.length === 0) return <Alert severity="info">{t('catalog.empty')}</Alert>;
  return <Box aria-hidden sx={{ display: 'none' }}>{colSpan}</Box>;
}
function RowActions({ disabled, canDeactivate, onEdit, onDeactivate }: { disabled: boolean; canDeactivate: boolean; onEdit: () => void; onDeactivate: () => void }) {
  const { t } = useTranslation();
  return <Stack direction="row" spacing={1} sx={{ justifyContent: 'flex-end' }}><IconButton aria-label={t('catalog.edit')} onClick={onEdit} disabled={disabled}><EditIcon /></IconButton><Button size="small" color="warning" onClick={onDeactivate} disabled={disabled || !canDeactivate}>{t('catalog.deactivate')}</Button></Stack>;
}
function Pager({ count, page, onPage }: { count: number; page: number; onPage: (page: number) => void }) {
  return <TablePagination component="div" count={count} page={page} rowsPerPage={PAGE_SIZE} rowsPerPageOptions={[PAGE_SIZE]} onPageChange={(_, next) => onPage(next)} />;
}

const emptyArea: UpsertAreaRequest = { name: '', checkInTime: '15:00', checkOutTime: '12:00', receptionOpenTime: '08:00', receptionCloseTime: '20:00' };
function AreaDialog({ open, area, onClose, onSaved }: { open: boolean; area: AreaDto | null; onClose: () => void; onSaved: () => void }) {
  const { t } = useTranslation();
  const notify = useNotify();
  const [form, setForm] = useState<UpsertAreaRequest>(() => area ? { name: area.name, checkInTime: timeValue(area.checkInTime), checkOutTime: timeValue(area.checkOutTime), receptionOpenTime: timeValue(area.receptionOpenTime), receptionCloseTime: timeValue(area.receptionCloseTime) } : emptyArea);
  const [errors, setErrors] = useState<FieldErrors>({});
  const mutation = useMutation({ mutationFn: (request: UpsertAreaRequest) => area ? apiPut<UpsertAreaRequest, AreaDto>(`/api/areas/${area.id}`, request) : apiPost<UpsertAreaRequest, AreaDto>('/api/areas', request), onSuccess: () => { notify.success(t(area ? 'catalog.updateSuccess' : 'catalog.createSuccess')); onSaved(); }, onError: (error) => { setErrors(readFieldErrors(error)); notify.error(asApiMessage(error, t('catalog.loadError'))); } });
  const save = () => { const next = requiredErrors({ name: form.name, checkInTime: form.checkInTime, checkOutTime: form.checkOutTime, receptionOpenTime: form.receptionOpenTime, receptionCloseTime: form.receptionCloseTime }, t); setErrors(next); if (Object.keys(next).length) return; mutation.mutate(trimArea(form)); };
  return <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm"><DialogTitle>{t(area ? 'catalog.area.edit' : 'catalog.area.create')}</DialogTitle><DialogContent><Stack spacing={2} sx={{ pt: 1 }}><TextField label={t('catalog.area.name')} required value={form.name} error={Boolean(errors.name)} helperText={errors.name} onChange={(event) => setForm({ ...form, name: event.target.value })} autoFocus /><TextField type="time" label={t('catalog.area.checkInTime')} required value={form.checkInTime} error={Boolean(errors.checkInTime)} helperText={errors.checkInTime} onChange={(event) => setForm({ ...form, checkInTime: event.target.value })} /><TextField type="time" label={t('catalog.area.checkOutTime')} required value={form.checkOutTime} error={Boolean(errors.checkOutTime)} helperText={errors.checkOutTime} onChange={(event) => setForm({ ...form, checkOutTime: event.target.value })} /><TextField type="time" label={t('catalog.area.receptionOpenTime')} required value={form.receptionOpenTime} error={Boolean(errors.receptionOpenTime)} helperText={errors.receptionOpenTime} onChange={(event) => setForm({ ...form, receptionOpenTime: event.target.value })} /><TextField type="time" label={t('catalog.area.receptionCloseTime')} required value={form.receptionCloseTime} error={Boolean(errors.receptionCloseTime)} helperText={errors.receptionCloseTime} onChange={(event) => setForm({ ...form, receptionCloseTime: event.target.value })} /></Stack></DialogContent><DialogActions><Button onClick={onClose}>{t('catalog.cancel')}</Button><Button variant="contained" onClick={save} disabled={mutation.isPending}>{t('catalog.save')}</Button></DialogActions></Dialog>;
}
function trimArea(form: UpsertAreaRequest): UpsertAreaRequest { return { ...form, name: form.name.trim() }; }

function RoomDialog({ open, room, areas, onClose, onSaved }: { open: boolean; room: RoomDto | null; areas: AreaDto[]; onClose: () => void; onSaved: () => void }) {
  const { t } = useTranslation();
  const notify = useNotify();
  const blank = useMemo<UpsertRoomRequest>(() => ({ areaId: areas[0]?.id ?? 0, name: '', capacity: 0, unitCount: 0, nightlyFare: 0, description: '', displayOrder: 0 }), [areas]);
  const [form, setForm] = useState<UpsertRoomRequest>(() => room ? { areaId: room.areaId, name: room.name, capacity: room.capacity, unitCount: room.unitCount, nightlyFare: room.nightlyFare, description: room.description ?? '', displayOrder: room.displayOrder } : blank);
  const [errors, setErrors] = useState<FieldErrors>({});
  const mutation = useMutation({ mutationFn: (request: UpsertRoomRequest) => room ? apiPut<UpsertRoomRequest, RoomDto>(`/api/rooms/${room.id}`, request) : apiPost<UpsertRoomRequest, RoomDto>('/api/rooms', request), onSuccess: () => { notify.success(t(room ? 'catalog.updateSuccess' : 'catalog.createSuccess')); onSaved(); }, onError: (error) => { setErrors(readFieldErrors(error)); notify.error(asApiMessage(error, t('catalog.loadError'))); } });
  const save = () => { const next = validateRoom(form, t); setErrors(next); if (Object.keys(next).length) return; mutation.mutate({ ...form, name: form.name.trim(), description: form.description?.trim() || null }); };
  return <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm"><DialogTitle>{t(room ? 'catalog.room.edit' : 'catalog.room.create')}</DialogTitle><DialogContent><Stack spacing={2} sx={{ pt: 1 }}><TextField select label={t('catalog.room.area')} required value={form.areaId} error={Boolean(errors.areaId)} helperText={errors.areaId} onChange={(event) => setForm({ ...form, areaId: Number(event.target.value) })}>{areas.map((area) => <MenuItem key={area.id} value={area.id}>{area.name}</MenuItem>)}</TextField><TextField label={t('catalog.room.name')} required value={form.name} error={Boolean(errors.name)} helperText={errors.name} onChange={(event) => setForm({ ...form, name: event.target.value })} autoFocus /><NumberField label={t('catalog.room.capacity')} value={form.capacity} error={errors.capacity} onChange={(value) => setForm({ ...form, capacity: value })} /><NumberField label={t('catalog.room.unitCount')} value={form.unitCount} error={errors.unitCount} onChange={(value) => setForm({ ...form, unitCount: value })} /><NumberField label={t('catalog.room.nightlyFare')} value={form.nightlyFare} error={errors.nightlyFare} onChange={(value) => setForm({ ...form, nightlyFare: value })} /><TextField label={t('catalog.room.description')} value={form.description ?? ''} multiline minRows={2} onChange={(event) => setForm({ ...form, description: event.target.value })} /><NumberField label={t('catalog.room.displayOrder')} value={form.displayOrder} error={errors.displayOrder} onChange={(value) => setForm({ ...form, displayOrder: value })} /></Stack></DialogContent><DialogActions><Button onClick={onClose}>{t('catalog.cancel')}</Button><Button variant="contained" onClick={save} disabled={mutation.isPending}>{t('catalog.save')}</Button></DialogActions></Dialog>;
}
function NumberField({ label, value, error, onChange }: { label: string; value: number; error?: string; onChange: (value: number) => void }) { return <TextField label={label} type="number" required value={Number.isFinite(value) ? value : ''} error={Boolean(error)} helperText={error} onChange={(event) => onChange(Number(event.target.value))} />; }
function validateRoom(form: UpsertRoomRequest, t: (key: string) => string): FieldErrors { const errors = requiredErrors({ name: form.name }, t); if (!form.areaId) errors.areaId = t('catalog.required'); (['capacity', 'unitCount', 'nightlyFare', 'displayOrder'] as const).forEach((field) => { if (!Number.isFinite(form[field])) errors[field] = t('catalog.invalidNumber'); else if (form[field] < 0) errors[field] = t('catalog.nonNegative'); }); return errors; }

function ConceptDialog({ open, concept, onClose, onSaved }: { open: boolean; concept: ConceptDto | null; onClose: () => void; onSaved: () => void }) {
  const { t } = useTranslation();
  const notify = useNotify();
  const [form, setForm] = useState<UpsertConceptRequest>(() => concept ? { code: concept.code, name: concept.name, isDiscount: concept.isDiscount } : { code: '', name: '', isDiscount: false });
  const [errors, setErrors] = useState<FieldErrors>({});
  const mutation = useMutation({ mutationFn: (request: UpsertConceptRequest) => concept ? apiPut<UpsertConceptRequest, ConceptDto>(`/api/concepts/${concept.id}`, request) : apiPost<UpsertConceptRequest, ConceptDto>('/api/concepts', request), onSuccess: () => { notify.success(t(concept ? 'catalog.updateSuccess' : 'catalog.createSuccess')); onSaved(); }, onError: (error) => { setErrors(readFieldErrors(error)); notify.error(asApiMessage(error, t('catalog.loadError'))); } });
  const save = () => { const next = requiredErrors({ code: form.code, name: form.name }, t); setErrors(next); if (Object.keys(next).length) return; mutation.mutate({ code: form.code.trim(), name: form.name.trim(), isDiscount: form.isDiscount }); };
  return <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm"><DialogTitle>{t(concept ? 'catalog.concept.edit' : 'catalog.concept.create')}</DialogTitle><DialogContent><Stack spacing={2} sx={{ pt: 1 }}>{concept?.isProtected && <Alert severity="info">{t('catalog.protected')}</Alert>}<TextField label={t('catalog.concept.code')} required value={form.code} error={Boolean(errors.code)} helperText={errors.code} onChange={(event) => setForm({ ...form, code: event.target.value })} autoFocus /><TextField label={t('catalog.concept.name')} required value={form.name} error={Boolean(errors.name)} helperText={errors.name} onChange={(event) => setForm({ ...form, name: event.target.value })} /><FormControlLabel control={<Switch checked={form.isDiscount} onChange={(event) => setForm({ ...form, isDiscount: event.target.checked })} />} label={t('catalog.concept.isDiscount')} /></Stack></DialogContent><DialogActions><Button onClick={onClose}>{t('catalog.cancel')}</Button><Button variant="contained" onClick={save} disabled={mutation.isPending}>{t('catalog.save')}</Button></DialogActions></Dialog>;
}
function requiredErrors(values: Record<string, string>, t: (key: string) => string): FieldErrors { return Object.fromEntries(Object.entries(values).filter(([, value]) => !value.trim()).map(([key]) => [key, t('catalog.required')])); }
