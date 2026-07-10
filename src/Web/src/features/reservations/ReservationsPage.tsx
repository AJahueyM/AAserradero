import AddIcon from '@mui/icons-material/Add';
import CheckCircleOutlinedIcon from '@mui/icons-material/CheckCircleOutlined';
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft';
import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import EventBusyIcon from '@mui/icons-material/EventBusy';
import HistoryIcon from '@mui/icons-material/History';
import HotelIcon from '@mui/icons-material/Hotel';
import MailIcon from '@mui/icons-material/Mail';
import PaidIcon from '@mui/icons-material/Paid';
import SyncAltIcon from '@mui/icons-material/SyncAlt';
import { Alert, Box, Button, Checkbox, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Divider, FormControlLabel, FormHelperText, IconButton, LinearProgress, MenuItem, Paper, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Tooltip, Typography } from '@mui/material';
import { DatePicker } from '@mui/x-date-pickers/DatePicker';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { addDays, addMonths, eachDayOfInterval, endOfMonth, format, startOfDay, startOfMonth, subMonths } from 'date-fns';
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { ApiError } from '../../api/apiError';
import { apiDelete, apiGet, apiPost, apiPut } from '../../api/httpClient';
import { useServerEvents } from '../../api/useServerEvents';
import { useCurrentUser } from '../../auth/useCurrentUser';
import { formatLocal, parseApiDate, toUtcIso } from '../../lib/datetime';
import { useConfirm } from '../../ui/ConfirmProvider';
import { useLoading } from '../../ui/LoadingProvider';
import { useNotify } from '../../ui/NotifyProvider';
import { ClientLookup } from '../clients/ClientLookup';
import type { ClientDto } from '../clients/clientsApi';
import './i18n';

type DateMode = 'Calendar' | 'Arrivals' | 'Departures';
type DialogState = { mode: 'closed' } | { mode: 'create'; seed?: ReservationSeed } | { mode: 'review'; reservationId: number };
interface ReservationSeed { roomId?: number; entryDate?: Date }
interface PagedResult<T> { items: T[]; page: number; pageSize: number; totalCount: number }
interface RoomDto { id: number; areaId: number; areaName: string; name: string; capacity: number; unitCount: number; nightlyFare: number; description: string | null; displayOrder: number; isActive: boolean }
interface ReservationStatusDto { id: number; code: string; label: string; sortOrder: number }
interface UserLookupDto { id: number; displayName: string }
interface ReferenceItemDto { id: number; code: string; name: string; isActive: boolean }
interface ConceptDto extends ReferenceItemDto { isDiscount: boolean; isProtected: boolean }
interface ReservationFinancialSummary { charges: number; payments: number; outstandingBalance: number }
interface ReservationClientResponse { id: number; name: string; phone: string | null; cellphone: string }
interface ReservationRoomResponse { id: number; name: string; capacity: number; nightlyFare: number }
interface ReservationStatusResponse { id: number; code: string; label: string }
interface ReservationSummaryResponse { id: number; client: ReservationClientResponse; room: ReservationRoomResponse; entryDate: string; exitDate: string; nights: number; adults: number; children: number; infants: number; pets: number; fare: number; status: ReservationStatusResponse; promotorId: number; notes: string | null; createdById: number; createdAt: string; financialSummary: ReservationFinancialSummary }
interface MovementReferenceResponse { id: number; code: string; name: string }
interface MovementConceptResponse extends MovementReferenceResponse { isDiscount: boolean }
interface MovementResponse { id: number; reservationId: number; concept: MovementConceptResponse; paymentMethod: MovementReferenceResponse | null; paymentLocation: MovementReferenceResponse | null; charge: number; payment: number; date: string; responsibleUserId: number; createdAt: string }
interface ReservationDetailResponse extends ReservationSummaryResponse { movements: MovementResponse[] }
interface UpsertReservationRequest { roomId: number; clientId: number; entryDate: string; exitDate: string; adults: number; children: number; infants: number; pets: number; fare?: number | null; statusCode?: string | null; promotorId: number; notes?: string | null }
interface UpsertMovementRequest { conceptCode: string; paymentMethodCode?: string | null; paymentLocationCode?: string | null; charge: number; payment: number; date?: string | null }
interface ConfirmationOptions { includePaymentInstructions: boolean; compact: boolean }
interface ConfirmationContent { subject: string; htmlBody: string; textBody: string; recipientEmail: string }
interface SendConfirmationResponse { sent: boolean; messageId: string; attempts: number }
interface ReferenceData { rooms: RoomDto[]; statuses: ReservationStatusDto[]; promotors: UserLookupDto[]; concepts: ConceptDto[]; paymentMethods: ReferenceItemDto[]; paymentLocations: ReferenceItemDto[] }
interface ReservationFormState { roomId: number | ''; statusCode: string; promotorId: number | ''; fare: string; entryDate: Date | null; exitDate: Date | null; entryTime: string; exitTime: string; adults: string; children: string; infants: string; pets: string; notes: string; client: ClientDto | null }
interface MovementFormState { id: number | null; conceptCode: string; paymentMethodCode: string; paymentLocationCode: string; charge: string; payment: string; date: Date | null }
interface CalendarCell { reservation: ReservationSummaryResponse | null; label: string; ariaLabel: string; icon: ReactNode; background: string; borderColor: string }
const currencyFormatter = new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN' });
const dayMs = 86_400_000;
export default function ReservationsPage() {
  const { t } = useTranslation();
  const notify = useNotify();
  const { showLoading } = useLoading();
  const queryClient = useQueryClient();
  const currentUser = useCurrentUser();
  const [month, setMonth] = useState(() => startOfMonth(new Date()));
  const [searchTerm, setSearchTerm] = useState('');
  const [searchResults, setSearchResults] = useState<ReservationSummaryResponse[]>([]);
  const [dialogState, setDialogState] = useState<DialogState>({ mode: 'closed' });
  const canManage = currentUser.data?.capabilities.includes('Reservations.Manage') ?? false;
  const roomsQuery = useQuery({ queryKey: ['reservations', 'rooms'], queryFn: listRooms, staleTime: 300_000 });
  const statusesQuery = useQuery({ queryKey: ['reservations', 'statuses'], queryFn: listReservationStatuses, staleTime: 300_000 });
  const promotorsQuery = useQuery({ queryKey: ['reservations', 'promotors'], queryFn: listPromotors, staleTime: 300_000 });
  const conceptsQuery = useQuery({ queryKey: ['reservations', 'concepts'], queryFn: listConcepts, staleTime: 300_000 });
  const paymentMethodsQuery = useQuery({ queryKey: ['reservations', 'payment-methods'], queryFn: listPaymentMethods, staleTime: 300_000 });
  const paymentLocationsQuery = useQuery({ queryKey: ['reservations', 'payment-locations'], queryFn: listPaymentLocations, staleTime: 300_000 });
  const range = useMemo(() => ({ from: toUtcIso(startOfMonth(month)), to: toUtcIso(endOfMonth(month)) }), [month]);
  const calendarQuery = useQuery({ queryKey: ['reservations', 'calendar', range.from, range.to], queryFn: () => searchReservations({ from: range.from, to: range.to, dateMode: 'Calendar' }), staleTime: 15_000 });
  const invalidateReservationData = useCallback((reservationId?: number) => {
    void queryClient.invalidateQueries({ queryKey: ['reservations', 'calendar'] });
    void queryClient.invalidateQueries({ queryKey: ['reservations', 'search'] });
    if (reservationId) void queryClient.invalidateQueries({ queryKey: ['reservation', reservationId] });
  }, [queryClient]);
  const serverEvents = useServerEvents<{ reservationId?: number }>({ onMessage: useCallback((message) => {
    if (message.type === 'reservation.changed' || message.type === 'reservation.movement.changed') invalidateReservationData(message.payload?.reservationId);
  }, [invalidateReservationData]) });
  useEffect(() => { if (serverEvents.status === 'error') notify.warning(t('reservations.liveError')); }, [notify, serverEvents.status, t]);
  const isInitialLoading = roomsQuery.isLoading || statusesQuery.isLoading || promotorsQuery.isLoading || conceptsQuery.isLoading || paymentMethodsQuery.isLoading || paymentLocationsQuery.isLoading;
  useEffect(() => { if (!isInitialLoading) return undefined; return showLoading(); }, [isInitialLoading, showLoading]);
  const referenceData = useMemo<ReferenceData>(() => ({ rooms: roomsQuery.data ?? [], statuses: statusesQuery.data ?? [], promotors: promotorsQuery.data ?? [], concepts: conceptsQuery.data ?? [], paymentMethods: paymentMethodsQuery.data ?? [], paymentLocations: paymentLocationsQuery.data ?? [] }), [conceptsQuery.data, paymentLocationsQuery.data, paymentMethodsQuery.data, promotorsQuery.data, roomsQuery.data, statusesQuery.data]);
  const searchMutation = useMutation({ mutationFn: searchReservationsByTerm, onSuccess: (matches) => {
    if (matches.length === 0) notify.info(t('reservations.searchNoResults'));
    else if (matches.length === 1) setDialogState({ mode: 'review', reservationId: matches[0].id });
    else setSearchResults(matches);
  }, onError: (error) => notify.error(apiMessage(error, t('reservations.unknownError'))) });
  const handleSearch = () => { const value = searchTerm.trim(); if (value) searchMutation.mutate(value); };
  const queryError = firstError(roomsQuery.error, statusesQuery.error, promotorsQuery.error, conceptsQuery.error, paymentMethodsQuery.error, paymentLocationsQuery.error, calendarQuery.error);
  return <Stack spacing={3}>
    <Stack spacing={1}><Typography variant="h4" component="h1">{t('reservations.title')}</Typography><Typography color="text.secondary">{t('reservations.subtitle')}</Typography></Stack>
    {queryError ? <Alert severity="error">{apiMessage(queryError, t('reservations.unknownError'))}</Alert> : null}
    <Paper sx={{ p: 2 }}><Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ alignItems: 'center' }}>
      <IconButton aria-label={t('reservations.previousMonth')} onClick={() => setMonth(subMonths(month, 1))}><ChevronLeftIcon /></IconButton>
      <DatePicker label={t('reservations.monthLabel')} value={month} views={['year', 'month']} onChange={(value) => value && setMonth(startOfMonth(value))} />
      <IconButton aria-label={t('reservations.nextMonth')} onClick={() => setMonth(addMonths(month, 1))}><ChevronRightIcon /></IconButton>
      <Button variant="outlined" onClick={() => setMonth(startOfMonth(new Date()))}>{t('reservations.today')}</Button>
      <Box sx={{ flexGrow: 1 }} />
      <TextField label={t('reservations.searchLabel')} value={searchTerm} onChange={(event) => setSearchTerm(event.target.value)} onKeyDown={(event) => { if (event.key === 'Enter') handleSearch(); }} helperText={t('reservations.searchHint')} fullWidth />
      <Button variant="contained" onClick={handleSearch} disabled={searchMutation.isPending || !searchTerm.trim()}>{t('reservations.searchButton')}</Button>
      {canManage && <Button variant="contained" startIcon={<AddIcon />} onClick={() => setDialogState({ mode: 'create' })}>{t('reservations.newReservation')}</Button>}
    </Stack></Paper>
    {calendarQuery.isFetching && <LinearProgress aria-label={t('reservations.calendarLoading')} />}
    <CalendarGrid month={month} rooms={referenceData.rooms} reservations={calendarQuery.data ?? []} onAvailableClick={(roomId, day) => setDialogState({ mode: 'create', seed: { roomId, entryDate: day } })} onReservationClick={(reservationId) => setDialogState({ mode: 'review', reservationId })} />
    <SearchResultsDialog open={searchResults.length > 0} results={searchResults} onClose={() => setSearchResults([])} onSelect={(reservationId) => { setSearchResults([]); setDialogState({ mode: 'review', reservationId }); }} />
    <ReservationDialog state={dialogState} referenceData={referenceData} canManage={canManage} onClose={() => setDialogState({ mode: 'closed' })} onSaved={(reservationId) => { invalidateReservationData(reservationId); setDialogState({ mode: 'review', reservationId }); }} />
  </Stack>;
}

function CalendarGrid({ month, rooms, reservations, onAvailableClick, onReservationClick }: { month: Date; rooms: RoomDto[]; reservations: ReservationSummaryResponse[]; onAvailableClick: (roomId: number, day: Date) => void; onReservationClick: (reservationId: number) => void }) {
  const { t } = useTranslation();
  const days = useMemo(() => eachDayOfInterval({ start: startOfMonth(month), end: endOfMonth(month) }), [month]);
  const activeRooms = useMemo(() => rooms.filter((room) => room.isActive).sort(compareRooms), [rooms]);
  const reservationsByRoom = useMemo(() => {
    const map = new Map<number, ReservationSummaryResponse[]>();
    for (const reservation of reservations) map.set(reservation.room.id, [...(map.get(reservation.room.id) ?? []), reservation]);
    for (const list of map.values()) list.sort((a, b) => utcDayNumber(a.entryDate) - utcDayNumber(b.entryDate));
    return map;
  }, [reservations]);
  if (!activeRooms.length) return <Alert severity="info">{t('reservations.roomsEmpty')}</Alert>;
  return <TableContainer component={Paper} sx={{ maxHeight: '70vh' }}><Table stickyHeader size="small" aria-label={t('reservations.title')}><TableHead><TableRow><TableCell sx={{ minWidth: 190 }}>{t('reservations.roomColumn')}</TableCell>{days.map((day) => <TableCell key={day.toISOString()} align="center" sx={{ minWidth: 108 }}><Typography variant="caption">{format(day, 'EEE')}</Typography><Typography variant="body2" sx={{ fontWeight: 700 }}>{format(day, 'dd')}</Typography></TableCell>)}</TableRow></TableHead><TableBody>{activeRooms.map((room) => <TableRow key={room.id} hover><TableCell component="th" scope="row"><Typography sx={{ fontWeight: 700 }}>{room.name}</Typography><Typography variant="caption" color="text.secondary">{room.areaName} · {t('reservations.capacityShort', { count: room.capacity })}</Typography></TableCell>{days.map((day) => { const cell = getCalendarCell(room, day, reservationsByRoom.get(room.id) ?? [], t); return <TableCell key={`${room.id}-${day.toISOString()}`} align="center" sx={{ p: 0.5 }}><CalendarCellButton cell={cell} onClick={() => cell.reservation ? onReservationClick(cell.reservation.id) : onAvailableClick(room.id, day)} /></TableCell>; })}</TableRow>)}</TableBody></Table></TableContainer>;
}
function CalendarCellButton({ cell, onClick }: { cell: CalendarCell; onClick: () => void }) {
  return <Tooltip title={cell.reservation ? <ReservationTooltip reservation={cell.reservation} /> : cell.label} arrow><Box component="button" type="button" onClick={onClick} aria-label={cell.ariaLabel} sx={{ width: '100%', minHeight: 58, border: '1px solid', borderColor: cell.borderColor, borderRadius: 1, cursor: 'pointer', color: 'text.primary', background: cell.background, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 0.25, '&:focus-visible': { outline: '3px solid', outlineColor: 'primary.main' } }}>{cell.icon}<Typography variant="caption" sx={{ fontWeight: 700, lineHeight: 1.1 }}>{cell.label}</Typography></Box></Tooltip>;
}
function ReservationTooltip({ reservation }: { reservation: ReservationSummaryResponse }) {
  const { t } = useTranslation();
  return <Stack spacing={0.5} sx={{ p: 0.5 }}><Typography variant="subtitle2">{t('reservations.reference', { id: reservation.id })}</Typography><Typography variant="body2">{reservation.client.name}</Typography><Typography variant="body2">{t('reservations.stayDates', { entry: formatLocal(reservation.entryDate, 'dd/MM/yyyy HH:mm'), exit: formatLocal(reservation.exitDate, 'dd/MM/yyyy HH:mm') })}</Typography><Typography variant="body2">{reservation.status.label}</Typography><Typography variant="body2">{t('reservations.outstanding', { amount: money(reservation.financialSummary.outstandingBalance) })}</Typography></Stack>;
}
function SearchResultsDialog({ open, results, onClose, onSelect }: { open: boolean; results: ReservationSummaryResponse[]; onClose: () => void; onSelect: (reservationId: number) => void }) {
  const { t } = useTranslation();
  return <Dialog open={open} onClose={onClose} fullWidth maxWidth="md"><DialogTitle>{t('reservations.searchResultsTitle')}</DialogTitle><DialogContent dividers><Table size="small"><TableHead><TableRow><TableCell>{t('reservations.reference', { id: '' })}</TableCell><TableCell>{t('reservations.client')}</TableCell><TableCell>{t('reservations.room')}</TableCell><TableCell>{t('reservations.status')}</TableCell><TableCell align="right">{t('reservations.balance')}</TableCell><TableCell align="right">{t('reservations.selectReservation')}</TableCell></TableRow></TableHead><TableBody>{results.map((reservation) => <TableRow key={reservation.id} hover><TableCell>#{reservation.id}</TableCell><TableCell><Typography>{reservation.client.name}</Typography><Typography variant="caption" color="text.secondary">{reservation.client.cellphone || reservation.client.phone}</Typography></TableCell><TableCell>{reservation.room.name}</TableCell><TableCell>{reservation.status.label}</TableCell><TableCell align="right">{money(reservation.financialSummary.outstandingBalance)}</TableCell><TableCell align="right"><Button size="small" onClick={() => onSelect(reservation.id)}>{t('reservations.openReservation')}</Button></TableCell></TableRow>)}</TableBody></Table></DialogContent><DialogActions><Button onClick={onClose}>{t('reservations.close')}</Button></DialogActions></Dialog>;
}

function ReservationDialog({ state, referenceData, canManage, onClose, onSaved }: { state: DialogState; referenceData: ReferenceData; canManage: boolean; onClose: () => void; onSaved: (reservationId: number) => void }) {
  const { t } = useTranslation();
  const notify = useNotify();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const open = state.mode !== 'closed';
  const reservationId = state.mode === 'review' ? state.reservationId : undefined;
  const seed = state.mode === 'create' ? state.seed : undefined;
  const [editMode, setEditMode] = useState(false);
  const [form, setForm] = useState<ReservationFormState>(() => createReservationForm(referenceData, seed));
  const [initialSnapshot, setInitialSnapshot] = useState(() => snapshotForm(form));
  const [serverFields, setServerFields] = useState<Record<string, string>>({});
  const [formApiError, setFormApiError] = useState<ApiError | null>(null);
  const [movementForm, setMovementForm] = useState<MovementFormState>(() => createMovementForm(referenceData));
  const [notificationOptions, setNotificationOptions] = useState<ConfirmationOptions>({ includePaymentInstructions: true, compact: false });
  const [preview, setPreview] = useState<ConfirmationContent | null>(null);
  const detailQuery = useQuery({ queryKey: ['reservation', reservationId], queryFn: () => getReservation(reservationId ?? 0), enabled: open && Boolean(reservationId) });
  useEffect(() => {
    if (!open) return;
    queueMicrotask(() => {
      setServerFields({});
      setFormApiError(null);
      setPreview(null);
      const next = detailQuery.data ? formFromReservation(detailQuery.data) : createReservationForm(referenceData, seed);
      setForm(next);
      setInitialSnapshot(snapshotForm(next));
      setEditMode(!detailQuery.data);
      setMovementForm(createMovementForm(referenceData));
    });
  }, [detailQuery.data, open, referenceData, seed]);
  const selectedRoom = referenceData.rooms.find((room) => room.id === form.roomId) ?? null;
  const validation = useMemo(() => validateReservationForm(form, selectedRoom, t), [form, selectedRoom, t]);
  const fieldErrors = { ...validation.fields, ...serverFields };
  const isDirty = editMode && snapshotForm(form) !== initialSnapshot;
  const detail = detailQuery.data;
  const isCancelled = detail?.status.code === 'Cancelled';
  const saveMutation = useMutation({ mutationFn: () => reservationId ? updateReservation(reservationId, buildReservationRequest(form)) : createReservation(buildReservationRequest(form)), onSuccess: (reservation) => { notify.success(reservationId ? t('reservations.saveSuccess') : t('reservations.createSuccess')); queryClient.setQueryData(['reservation', reservation.id], reservation); onSaved(reservation.id); }, onError: (error) => { if (error instanceof ApiError) { setFormApiError(error); setServerFields(extractFieldErrors(error)); } notify.error(apiMessage(error, t('reservations.unknownError'))); } });
  const cancelReservationMutation = useMutation({ mutationFn: async () => { if (reservationId) await cancelReservation(reservationId); }, onSuccess: () => { notify.success(t('reservations.reservationCancelled')); if (reservationId) { void queryClient.invalidateQueries({ queryKey: ['reservation', reservationId] }); onSaved(reservationId); } }, onError: (error) => notify.error(apiMessage(error, t('reservations.unknownError'))) });
  const movementMutation = useMutation({ mutationFn: () => { if (!reservationId) throw new Error('Reservation id is required.'); return movementForm.id ? updateMovement(reservationId, movementForm.id, buildMovementRequest(movementForm)) : addMovement(reservationId, buildMovementRequest(movementForm)); }, onSuccess: (reservation) => { notify.success(t('reservations.movementSaved')); queryClient.setQueryData(['reservation', reservation.id], reservation); onSaved(reservation.id); setMovementForm(createMovementForm(referenceData)); }, onError: (error) => notify.error(apiMessage(error, t('reservations.unknownError'))) });
  const deleteMovementMutation = useMutation({ mutationFn: async (movementId: number) => { if (reservationId) await deleteMovement(reservationId, movementId); }, onSuccess: () => { notify.success(t('reservations.movementDeleted')); if (reservationId) { void queryClient.invalidateQueries({ queryKey: ['reservation', reservationId] }); onSaved(reservationId); } }, onError: (error) => notify.error(apiMessage(error, t('reservations.unknownError'))) });
  const recomputeMutation = useMutation({ mutationFn: () => { if (!reservationId) throw new Error('Reservation id is required.'); return recomputeBalance(reservationId); }, onSuccess: (reservation) => { notify.success(t('reservations.recomputed')); queryClient.setQueryData(['reservation', reservation.id], reservation); onSaved(reservation.id); }, onError: (error) => notify.error(apiMessage(error, t('reservations.unknownError'))) });
  const previewMutation = useMutation({ mutationFn: () => { if (!reservationId) throw new Error('Reservation id is required.'); return previewConfirmation(reservationId, notificationOptions); }, onSuccess: (content) => { setPreview(content); notify.success(t('reservations.previewReady')); }, onError: (error) => notify.error(apiMessage(error, t('reservations.unknownError'))) });
  const sendMutation = useMutation({ mutationFn: () => { if (!reservationId) throw new Error('Reservation id is required.'); return sendConfirmation(reservationId, notificationOptions); }, onSuccess: (response: SendConfirmationResponse) => notify.success(`${t('reservations.sent')} ${response.messageId}`), onError: (error) => notify.error(apiMessage(error, t('reservations.unknownError'))) });
  const requestClose = async () => { if (isDirty) { const discard = await confirm({ title: t('reservations.discardTitle'), description: t('reservations.discardDescription') }); if (!discard) return; } onClose(); };
  const pending = saveMutation.isPending || cancelReservationMutation.isPending || movementMutation.isPending || deleteMovementMutation.isPending || recomputeMutation.isPending || previewMutation.isPending || sendMutation.isPending;
  const submitDisabled = !canManage || pending || Boolean(validation.message) || Object.keys(validation.fields).length > 0;
  const handleSubmit = () => { setServerFields({}); setFormApiError(null); if (!submitDisabled) saveMutation.mutate(); };
  const handleCancelReservation = async () => { const ok = await confirm({ title: t('reservations.cancelReservationTitle'), description: t('reservations.cancelReservationDescription') }); if (ok) cancelReservationMutation.mutate(); };
  const handleDeleteMovement = async (movementId: number) => { const ok = await confirm({ title: t('reservations.deleteMovementTitle'), description: t('reservations.deleteMovementDescription') }); if (ok) deleteMovementMutation.mutate(movementId); };
  return <Dialog open={open} onClose={() => void requestClose()} fullWidth maxWidth="lg"><DialogTitle>{reservationId ? t('reservations.reviewTitle', { id: reservationId }) : t('reservations.createTitle')}</DialogTitle><DialogContent dividers><Stack spacing={3}>
    {detailQuery.isFetching && <LinearProgress />}
    {!canManage && <Alert severity="info">{t('reservations.cannotManage')}</Alert>}
    {isCancelled && <Alert severity="warning" icon={<EventBusyIcon />}><Typography sx={{ fontWeight: 700 }}>{t('reservations.cancelled')}</Typography>{t('reservations.cancelledHelp')}</Alert>}
    {formApiError && <Alert severity="error"><Typography sx={{ fontWeight: 700 }}>{formErrorTitle(formApiError, t)}</Typography>{formApiError.message}</Alert>}
    {validation.message && <Alert severity="warning">{validation.message}</Alert>}
    {detail && !editMode ? <ReservationReview reservation={detail} promotors={referenceData.promotors} /> : <ReservationForm form={form} fieldErrors={fieldErrors} referenceData={referenceData} disabled={!canManage || pending} onChange={setForm} />}
    {detail && <><Divider /><MovementsSection reservation={detail} referenceData={referenceData} form={movementForm} canManage={canManage} pending={pending} onFormChange={setMovementForm} onEdit={(movement) => setMovementForm(formFromMovement(movement))} onSubmit={() => movementMutation.mutate()} onDelete={(movementId) => void handleDeleteMovement(movementId)} onRecompute={() => recomputeMutation.mutate()} /><Divider /><NotificationSection options={notificationOptions} preview={preview} pending={pending} canManage={canManage} onOptionsChange={setNotificationOptions} onPreview={() => previewMutation.mutate()} onSend={() => sendMutation.mutate()} /></>}
  </Stack></DialogContent><DialogActions>{detail && !editMode && canManage && <Button startIcon={<EditIcon />} onClick={() => setEditMode(true)} disabled={pending}>{t('reservations.edit')}</Button>}{detail && canManage && !isCancelled && <Button color="error" onClick={() => void handleCancelReservation()} disabled={pending}>{t('reservations.cancel')}</Button>}<Box sx={{ flexGrow: 1 }} /><Button onClick={() => void requestClose()} disabled={pending}>{t('reservations.close')}</Button>{editMode && <Button variant="contained" onClick={handleSubmit} disabled={submitDisabled}>{reservationId ? t('reservations.save') : t('reservations.create')}</Button>}</DialogActions></Dialog>;
}

function ReservationForm({ form, fieldErrors, referenceData, disabled, onChange }: { form: ReservationFormState; fieldErrors: Record<string, string>; referenceData: ReferenceData; disabled: boolean; onChange: (form: ReservationFormState) => void }) {
  const { t } = useTranslation();
  const update = <K extends keyof ReservationFormState>(key: K, value: ReservationFormState[K]) => onChange({ ...form, [key]: value });
  return <Stack spacing={3}><Stack spacing={2}><Typography variant="h6">{t('reservations.client')}</Typography><ClientLookup value={form.client} onChange={(client) => update('client', client)} disabled={disabled} />{fieldErrors.clientId && <FormHelperText error>{fieldErrors.clientId}</FormHelperText>}</Stack><Stack spacing={2}><Typography variant="h6">{t('reservations.bookingInfo')}</Typography><Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: 'repeat(3, 1fr)' }, gap: 2 }}>
    <TextField select required label={t('reservations.room')} value={form.roomId} error={Boolean(fieldErrors.roomId)} helperText={fieldErrors.roomId} disabled={disabled} onChange={(event) => update('roomId', Number(event.target.value))}>{referenceData.rooms.filter((room) => room.isActive).map((room) => <MenuItem key={room.id} value={room.id}>{room.name} · {money(room.nightlyFare)}</MenuItem>)}</TextField>
    <TextField select required label={t('reservations.status')} value={form.statusCode} error={Boolean(fieldErrors.statusCode)} helperText={fieldErrors.statusCode} disabled={disabled} onChange={(event) => update('statusCode', event.target.value)}>{referenceData.statuses.map((status) => <MenuItem key={status.code} value={status.code}>{status.label}</MenuItem>)}</TextField>
    <TextField select required label={t('reservations.promotor')} value={form.promotorId} error={Boolean(fieldErrors.promotorId)} helperText={fieldErrors.promotorId} disabled={disabled} onChange={(event) => update('promotorId', Number(event.target.value))}>{referenceData.promotors.map((promotor) => <MenuItem key={promotor.id} value={promotor.id}>{promotor.displayName}</MenuItem>)}</TextField>
    <DateField label={t('reservations.entryDate')} value={form.entryDate} disabled={disabled} error={fieldErrors.entryDate} onChange={(value) => update('entryDate', value)} />
    <TextField label={t('reservations.entryTime')} type="time" value={form.entryTime} error={Boolean(fieldErrors.entryTime)} helperText={fieldErrors.entryTime} disabled={disabled} onChange={(event) => update('entryTime', event.target.value)} />
    <TextField label={t('reservations.fare')} type="number" value={form.fare} error={Boolean(fieldErrors.fare)} helperText={fieldErrors.fare} disabled={disabled} onChange={(event) => update('fare', event.target.value)} />
    <DateField label={t('reservations.exitDate')} value={form.exitDate} disabled={disabled} error={fieldErrors.exitDate} onChange={(value) => update('exitDate', value)} />
    <TextField label={t('reservations.exitTime')} type="time" value={form.exitTime} error={Boolean(fieldErrors.exitTime)} helperText={fieldErrors.exitTime} disabled={disabled} onChange={(event) => update('exitTime', event.target.value)} />
  </Box></Stack><Stack spacing={2}><Typography variant="h6">{t('reservations.occupants')}</Typography><Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: 'repeat(4, 1fr)' }, gap: 2 }}>{(['adults', 'children', 'infants', 'pets'] as const).map((key) => <TextField key={key} label={t(`reservations.${key}`)} type="number" value={form[key]} error={Boolean(fieldErrors[key])} helperText={fieldErrors[key]} disabled={disabled} onChange={(event) => update(key, event.target.value)} />)}</Box></Stack><TextField label={t('reservations.notes')} value={form.notes} multiline minRows={3} disabled={disabled} onChange={(event) => update('notes', event.target.value)} /></Stack>;
}
function DateField({ label, value, disabled, error, onChange }: { label: string; value: Date | null; disabled: boolean; error?: string; onChange: (value: Date | null) => void }) { return <Stack spacing={0.5}><DatePicker label={label} value={value} disabled={disabled} onChange={onChange} />{error && <FormHelperText error>{error}</FormHelperText>}</Stack>; }
function InfoCard({ title, icon, children }: { title: string; icon?: ReactNode; children: ReactNode }) { return <Paper variant="outlined" sx={{ p: 2 }}><Stack spacing={1}><Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>{icon}<Typography variant="subtitle2" color="text.secondary">{title}</Typography></Stack>{children}</Stack></Paper>; }
function ReservationReview({ reservation, promotors }: { reservation: ReservationDetailResponse; promotors: UserLookupDto[] }) {
  const { t } = useTranslation(); const promotor = promotors.find((item) => item.id === reservation.promotorId)?.displayName ?? `#${reservation.promotorId}`; const occupants = reservation.adults + reservation.children + reservation.infants;
  return <Stack spacing={2}><Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', alignItems: 'center' }}><Chip icon={<HotelIcon />} label={reservation.room.name} /><Chip icon={<SyncAltIcon />} label={t('reservations.currentStatus', { status: reservation.status.label })} /><Chip label={t('reservations.reference', { id: reservation.id })} /></Stack><Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: 'repeat(3, 1fr)' }, gap: 2 }}><InfoCard title={t('reservations.client')}><Typography sx={{ fontWeight: 700 }}>{reservation.client.name}</Typography><Typography>{reservation.client.cellphone || reservation.client.phone}</Typography></InfoCard><InfoCard title={t('reservations.bookingInfo')}><Typography>{t('reservations.stayDates', { entry: formatLocal(reservation.entryDate, 'dd/MM/yyyy HH:mm'), exit: formatLocal(reservation.exitDate, 'dd/MM/yyyy HH:mm') })}</Typography><Typography>{t('reservations.staySummary', { nights: reservation.nights, occupants })}</Typography><Typography>{t('reservations.promotor')}: {promotor}</Typography></InfoCard><InfoCard title={t('reservations.financialSummary')} icon={<PaidIcon />}><Typography>{t('reservations.charges')}: {money(reservation.financialSummary.charges)}</Typography><Typography>{t('reservations.payments')}: {money(reservation.financialSummary.payments)}</Typography><Typography sx={{ fontWeight: 700 }}>{t('reservations.balance')}: {money(reservation.financialSummary.outstandingBalance)}</Typography></InfoCard></Box>{reservation.notes && <Alert severity="info"><Typography sx={{ fontWeight: 700 }}>{t('reservations.notes')}</Typography>{reservation.notes}</Alert>}</Stack>;
}
function MovementsSection({ reservation, referenceData, form, canManage, pending, onFormChange, onEdit, onSubmit, onDelete, onRecompute }: { reservation: ReservationDetailResponse; referenceData: ReferenceData; form: MovementFormState; canManage: boolean; pending: boolean; onFormChange: (form: MovementFormState) => void; onEdit: (movement: MovementResponse) => void; onSubmit: () => void; onDelete: (movementId: number) => void; onRecompute: () => void }) {
  const { t } = useTranslation(); const update = <K extends keyof MovementFormState>(key: K, value: MovementFormState[K]) => onFormChange({ ...form, [key]: value }); const invalid = !form.conceptCode || (toNumber(form.charge) <= 0 && toNumber(form.payment) <= 0);
  return <Stack spacing={2}><Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}><Typography variant="h6">{t('reservations.movements')}</Typography><Box sx={{ flexGrow: 1 }} />{canManage && <Button onClick={onRecompute} disabled={pending} size="small">{t('reservations.recompute')}</Button>}</Stack>{reservation.movements.length === 0 ? <Alert severity="info">{t('reservations.noMovements')}</Alert> : <TableContainer component={Paper} variant="outlined"><Table size="small"><TableHead><TableRow><TableCell>{t('reservations.movementDate')}</TableCell><TableCell>{t('reservations.concept')}</TableCell><TableCell>{t('reservations.paymentMethod')}</TableCell><TableCell align="right">{t('reservations.charge')}</TableCell><TableCell align="right">{t('reservations.payment')}</TableCell><TableCell>{t('reservations.movementResponsible', { id: '' })}</TableCell>{canManage && <TableCell align="right">{t('reservations.edit')}</TableCell>}</TableRow></TableHead><TableBody>{reservation.movements.map((movement) => <TableRow key={movement.id} hover><TableCell>{formatLocal(movement.date, 'dd/MM/yyyy HH:mm')}</TableCell><TableCell>{movement.concept.name}</TableCell><TableCell>{movement.paymentMethod?.name ?? '—'}{movement.paymentLocation ? ` · ${movement.paymentLocation.name}` : ''}</TableCell><TableCell align="right">{money(movement.charge)}</TableCell><TableCell align="right">{money(movement.payment)}</TableCell><TableCell>{t('reservations.movementResponsible', { id: movement.responsibleUserId })}</TableCell>{canManage && <TableCell align="right"><IconButton aria-label={t('reservations.editMovement')} onClick={() => onEdit(movement)} disabled={pending}><EditIcon /></IconButton><IconButton aria-label={t('reservations.deleteMovementTitle')} color="error" onClick={() => onDelete(movement.id)} disabled={pending}><DeleteIcon /></IconButton></TableCell>}</TableRow>)}</TableBody></Table></TableContainer>}{canManage && <Paper variant="outlined" sx={{ p: 2 }}><Stack spacing={2}><Typography variant="subtitle1">{form.id ? t('reservations.editMovement') : t('reservations.addMovement')}</Typography><Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: 'repeat(3, 1fr)' }, gap: 2 }}><TextField select required label={t('reservations.concept')} value={form.conceptCode} disabled={pending} onChange={(event) => update('conceptCode', event.target.value)}>{referenceData.concepts.filter((concept) => concept.isActive).map((concept) => <MenuItem key={concept.code} value={concept.code}>{concept.name}</MenuItem>)}</TextField><TextField select label={t('reservations.paymentMethod')} value={form.paymentMethodCode} disabled={pending} onChange={(event) => update('paymentMethodCode', event.target.value)}><MenuItem value="">—</MenuItem>{referenceData.paymentMethods.filter((method) => method.isActive).map((method) => <MenuItem key={method.code} value={method.code}>{method.name}</MenuItem>)}</TextField><TextField select label={t('reservations.paymentLocation')} value={form.paymentLocationCode} disabled={pending} onChange={(event) => update('paymentLocationCode', event.target.value)}><MenuItem value="">—</MenuItem>{referenceData.paymentLocations.filter((location) => location.isActive).map((location) => <MenuItem key={location.code} value={location.code}>{location.name}</MenuItem>)}</TextField><TextField label={t('reservations.charge')} type="number" value={form.charge} disabled={pending} onChange={(event) => update('charge', event.target.value)} /><TextField label={t('reservations.payment')} type="number" value={form.payment} disabled={pending} onChange={(event) => update('payment', event.target.value)} /><DateField label={t('reservations.movementDate')} value={form.date} disabled={pending} onChange={(value) => update('date', value)} /></Box>{invalid && <FormHelperText error>{t('reservations.amountRequired')}</FormHelperText>}<Stack direction="row" spacing={1}><Button variant="contained" onClick={onSubmit} disabled={pending || invalid}>{t('reservations.saveMovement')}</Button>{form.id && <Button onClick={() => onFormChange(createMovementForm(referenceData))} disabled={pending}>{t('reservations.cancelMovementEdit')}</Button>}</Stack></Stack></Paper>}</Stack>;
}
function NotificationSection({ options, preview, pending, canManage, onOptionsChange, onPreview, onSend }: { options: ConfirmationOptions; preview: ConfirmationContent | null; pending: boolean; canManage: boolean; onOptionsChange: (options: ConfirmationOptions) => void; onPreview: () => void; onSend: () => void }) {
  const { t } = useTranslation();
  return <Stack spacing={2}><Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}><MailIcon /><Typography variant="h6">{t('reservations.notificationTitle')}</Typography></Stack><Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ alignItems: 'center' }}><FormControlLabel control={<Checkbox checked={options.includePaymentInstructions} onChange={(event) => onOptionsChange({ ...options, includePaymentInstructions: event.target.checked })} disabled={pending || !canManage} />} label={t('reservations.includePaymentInstructions')} /><FormControlLabel control={<Checkbox checked={options.compact} onChange={(event) => onOptionsChange({ ...options, compact: event.target.checked })} disabled={pending || !canManage} />} label={t('reservations.compact')} /><Button variant="outlined" onClick={onPreview} disabled={pending || !canManage}>{t('reservations.preview')}</Button><Button variant="contained" onClick={onSend} disabled={pending || !canManage}>{t('reservations.send')}</Button></Stack>{preview && <Paper variant="outlined" sx={{ p: 2 }}><Stack spacing={1}><Typography variant="subtitle2">{t('reservations.recipient')}: {preview.recipientEmail}</Typography><Typography variant="subtitle2">{t('reservations.subject')}: {preview.subject}</Typography><TextField label={t('reservations.message')} value={preview.textBody} multiline minRows={8} fullWidth disabled /></Stack></Paper>}</Stack>;
}

function getCalendarCell(room: RoomDto, day: Date, reservations: ReservationSummaryResponse[], t: (key: string, params?: Record<string, unknown>) => string): CalendarCell {
  const dayNumber = localDayNumber(day); const todayNumber = localDayNumber(new Date()); const occupying = reservations.find((reservation) => utcDayNumber(reservation.entryDate) <= dayNumber && dayNumber < utcDayNumber(reservation.exitDate)); const ending = reservations.find((reservation) => utcDayNumber(reservation.exitDate) === dayNumber); const starts = occupying ? utcDayNumber(occupying.entryDate) === dayNumber : false; const ends = Boolean(ending); const reservation = occupying ?? ending ?? null; const past = dayNumber < todayNumber;
  const label = starts && ends ? t('reservations.transitionBoth') : starts ? t('reservations.transitionStart') : ends ? t('reservations.transitionEnd') : reservation ? reservation.status.label : past ? t('reservations.past') : t('reservations.available');
  const base = reservation ? statusBackground(reservation.status.code) : past ? '#f5f5f5' : '#eef7ee'; const background = past && !reservation ? 'repeating-linear-gradient(45deg,#f5f5f5,#f5f5f5 6px,#eeeeee 6px,#eeeeee 12px)' : (starts || ends ? `linear-gradient(135deg,${base} 0 48%,#fff 48% 52%,${past ? '#eee' : '#e8f5e9'} 52% 100%)` : base);
  return { reservation, label, ariaLabel: t('reservations.dayAria', { room: room.name, day: format(day, 'dd/MM/yyyy'), state: label }), icon: reservation ? (starts || ends ? <SyncAltIcon fontSize="small" /> : <HotelIcon fontSize="small" />) : past ? <HistoryIcon fontSize="small" /> : <CheckCircleOutlinedIcon fontSize="small" />, background, borderColor: reservation ? statusBorder(reservation.status.code) : past ? '#bdbdbd' : '#7cb342' };
}

async function listRooms() { return (await apiGet<PagedResult<RoomDto>>('/api/rooms?page=1&pageSize=500')).items; }
async function listReservationStatuses() { return apiGet<ReservationStatusDto[]>('/api/reservation-statuses'); }
async function listPromotors() { return (await apiGet<PagedResult<UserLookupDto>>('/api/users/lookup?page=1&pageSize=500')).items; }
async function listConcepts() { return (await apiGet<PagedResult<ConceptDto>>('/api/concepts?page=1&pageSize=500')).items; }
async function listPaymentMethods() { return (await apiGet<PagedResult<ReferenceItemDto>>('/api/payment-methods?page=1&pageSize=500')).items; }
async function listPaymentLocations() { return (await apiGet<PagedResult<ReferenceItemDto>>('/api/payment-locations?page=1&pageSize=500')).items; }
async function searchReservations(params: { from?: string; to?: string; dateMode: DateMode; clientPhone?: string; clientName?: string; clientId?: number }) { const query = new URLSearchParams(); if (params.from) query.set('from', params.from); if (params.to) query.set('to', params.to); query.set('dateMode', params.dateMode); if (params.clientPhone) query.set('clientPhone', params.clientPhone); if (params.clientName) query.set('clientName', params.clientName); if (params.clientId) query.set('clientId', String(params.clientId)); return apiGet<ReservationSummaryResponse[]>(`/api/reservations?${query.toString()}`); }
async function searchReservationsByTerm(term: string) { const [byName, byPhone, byId] = await Promise.all([searchReservations({ dateMode: 'Calendar', clientName: term }), searchReservations({ dateMode: 'Calendar', clientPhone: term }), searchReservationByNumericReference(term)]); return uniqueReservations([...byId, ...byName, ...byPhone]); }
async function searchReservationByNumericReference(term: string) { if (!/^\d+$/.test(term)) return []; try { return [detailToSummary(await getReservation(Number(term)))]; } catch (error) { if (error instanceof ApiError && error.status === 404) return []; throw error; } }
async function getReservation(id: number) { return apiGet<ReservationDetailResponse>(`/api/reservations/${id}`); }
async function createReservation(body: UpsertReservationRequest) { return apiPost<UpsertReservationRequest, ReservationDetailResponse>('/api/reservations', body); }
async function updateReservation(id: number, body: UpsertReservationRequest) { return apiPut<UpsertReservationRequest, ReservationDetailResponse>(`/api/reservations/${id}`, body); }
async function cancelReservation(id: number) { await apiPost<Record<string, never>, void>(`/api/reservations/${id}/cancel`, {}); }
async function addMovement(id: number, body: UpsertMovementRequest) { return apiPost<UpsertMovementRequest, ReservationDetailResponse>(`/api/reservations/${id}/movements`, body); }
async function updateMovement(id: number, movementId: number, body: UpsertMovementRequest) { return apiPut<UpsertMovementRequest, ReservationDetailResponse>(`/api/reservations/${id}/movements/${movementId}`, body); }
async function deleteMovement(id: number, movementId: number) { await apiDelete<void>(`/api/reservations/${id}/movements/${movementId}`); }
async function recomputeBalance(id: number) { return apiPost<Record<string, never>, ReservationDetailResponse>(`/api/reservations/${id}/movements/recompute-balance`, {}); }
async function previewConfirmation(id: number, body: ConfirmationOptions) { return apiPost<ConfirmationOptions, ConfirmationContent>(`/api/notifications/reservations/${id}/confirmation/preview`, body); }
async function sendConfirmation(id: number, body: ConfirmationOptions) { return apiPost<ConfirmationOptions, SendConfirmationResponse>(`/api/notifications/reservations/${id}/confirmation/send`, body); }
function createReservationForm(referenceData: ReferenceData, seed?: ReservationSeed): ReservationFormState { const room = seed?.roomId ? referenceData.rooms.find((item) => item.id === seed.roomId) : referenceData.rooms.find((item) => item.isActive); const entryDate = startOfDay(seed?.entryDate ?? new Date()); return { roomId: room?.id ?? '', statusCode: referenceData.statuses.find((status) => status.code === 'Pending')?.code ?? referenceData.statuses[0]?.code ?? '', promotorId: referenceData.promotors[0]?.id ?? '', fare: room ? String(room.nightlyFare) : '', entryDate, exitDate: addDays(entryDate, 1), entryTime: '15:00', exitTime: '12:00', adults: '2', children: '0', infants: '0', pets: '0', notes: '', client: null }; }
function formFromReservation(reservation: ReservationDetailResponse): ReservationFormState { return { roomId: reservation.room.id, statusCode: reservation.status.code, promotorId: reservation.promotorId, fare: String(reservation.fare), entryDate: dateOnlyFromApi(reservation.entryDate), exitDate: dateOnlyFromApi(reservation.exitDate), entryTime: '15:00', exitTime: '12:00', adults: String(reservation.adults), children: String(reservation.children), infants: String(reservation.infants), pets: String(reservation.pets), notes: reservation.notes ?? '', client: reservationClientToClientDto(reservation.client) }; }
function validateReservationForm(form: ReservationFormState, room: RoomDto | null, t: (key: string, params?: Record<string, unknown>) => string) { const fields: Record<string, string> = {}; if (!form.client) fields.clientId = t('reservations.clientRequired'); if (!form.roomId) fields.roomId = t('reservations.required'); if (!form.statusCode) fields.statusCode = t('reservations.required'); if (!form.promotorId) fields.promotorId = t('reservations.required'); if (!form.entryDate) fields.entryDate = t('reservations.required'); if (!form.exitDate) fields.exitDate = t('reservations.required'); if (!form.fare.trim()) fields.fare = t('reservations.required'); if (toNumber(form.fare) < 0) fields.fare = t('reservations.nonNegative'); for (const key of ['adults', 'children', 'infants', 'pets'] as const) { if (!form[key].trim()) fields[key] = t('reservations.required'); if (toInteger(form[key]) < 0) fields[key] = t('reservations.nonNegative'); } if (form.entryDate && form.exitDate && combineDateAndTime(form.entryDate, form.entryTime) >= combineDateAndTime(form.exitDate, form.exitTime)) return { fields, message: t('reservations.invalidDates') }; const occupants = toInteger(form.adults) + toInteger(form.children) + toInteger(form.infants); if (room && occupants > room.capacity) return { fields, message: t('reservations.capacityExceeded', { capacity: room.capacity }) }; return { fields }; }
function buildReservationRequest(form: ReservationFormState): UpsertReservationRequest { return { roomId: requiredNumber(form.roomId), clientId: form.client?.id ?? 0, entryDate: toUtcIso(combineDateAndTime(requiredDate(form.entryDate), form.entryTime)), exitDate: toUtcIso(combineDateAndTime(requiredDate(form.exitDate), form.exitTime)), adults: toInteger(form.adults), children: toInteger(form.children), infants: toInteger(form.infants), pets: toInteger(form.pets), fare: form.fare.trim() ? toNumber(form.fare) : null, statusCode: form.statusCode || null, promotorId: requiredNumber(form.promotorId), notes: form.notes.trim() || null }; }
function createMovementForm(referenceData: ReferenceData): MovementFormState { return { id: null, conceptCode: referenceData.concepts.find((concept) => concept.isActive)?.code ?? '', paymentMethodCode: referenceData.paymentMethods.find((method) => method.isActive)?.code ?? '', paymentLocationCode: referenceData.paymentLocations.find((location) => location.isActive)?.code ?? '', charge: '0', payment: '0', date: new Date() }; }
function formFromMovement(movement: MovementResponse): MovementFormState { return { id: movement.id, conceptCode: movement.concept.code, paymentMethodCode: movement.paymentMethod?.code ?? '', paymentLocationCode: movement.paymentLocation?.code ?? '', charge: String(movement.charge), payment: String(movement.payment), date: parseApiDate(movement.date) }; }
function buildMovementRequest(form: MovementFormState): UpsertMovementRequest { return { conceptCode: form.conceptCode, paymentMethodCode: form.paymentMethodCode || null, paymentLocationCode: form.paymentLocationCode || null, charge: toNumber(form.charge), payment: toNumber(form.payment), date: form.date ? toUtcIso(form.date) : null }; }
function combineDateAndTime(date: Date, time: string) { const [hours = 0, minutes = 0] = time.split(':').map((part) => Number(part)); return new Date(date.getFullYear(), date.getMonth(), date.getDate(), hours, minutes, 0, 0); }
function dateOnlyFromApi(iso: string) { const parsed = parseApiDate(iso); return new Date(parsed.getUTCFullYear(), parsed.getUTCMonth(), parsed.getUTCDate()); }
function reservationClientToClientDto(client: ReservationClientResponse): ClientDto { return { id: client.id, name: client.name, taxId: null, address: null, email: null, phone: client.phone, cellphone: client.cellphone, isVip: false, isBlacklisted: false, blacklistReason: null, isActive: true, recentActivityCount: 0 }; }
function detailToSummary(detail: ReservationDetailResponse): ReservationSummaryResponse { const { movements, ...summary } = detail; void movements; return summary; }
function uniqueReservations(reservations: ReservationSummaryResponse[]) { return Array.from(new Map(reservations.map((reservation) => [reservation.id, reservation])).values()); }
function snapshotForm(form: ReservationFormState) { return JSON.stringify({ ...form, client: form.client?.id ?? null, entryDate: form.entryDate?.toISOString() ?? null, exitDate: form.exitDate?.toISOString() ?? null }); }
function toInteger(value: string) { const parsed = Number.parseInt(value, 10); return Number.isFinite(parsed) ? parsed : 0; }
function toNumber(value: string) { const parsed = Number(value); return Number.isFinite(parsed) ? parsed : 0; }
function requiredNumber(value: number | '') { return typeof value === 'number' ? value : 0; }
function requiredDate(value: Date | null) { return value ?? new Date(Number.NaN); }
function localDayNumber(date: Date) { return Math.floor(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()) / dayMs); }
function utcDayNumber(iso: string) { const date = parseApiDate(iso); return Math.floor(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate()) / dayMs); }
function compareRooms(a: RoomDto, b: RoomDto) { return a.displayOrder - b.displayOrder || a.areaName.localeCompare(b.areaName) || a.name.localeCompare(b.name); }
function statusBackground(code: string) { switch (code) { case 'Pending': return '#fff8e1'; case 'Partial': return '#e3f2fd'; case 'Paid': return '#e8f5e9'; case 'Maintenance': return '#f3e5f5'; case 'Courtesy': return '#e0f2f1'; case 'Cancelled': return '#ffebee'; default: return '#eeeeee'; } }
function statusBorder(code: string) { switch (code) { case 'Pending': return '#f9a825'; case 'Partial': return '#1976d2'; case 'Paid': return '#2e7d32'; case 'Maintenance': return '#7b1fa2'; case 'Courtesy': return '#00897b'; case 'Cancelled': return '#c62828'; default: return '#757575'; } }
function money(value: number) { return currencyFormatter.format(value); }
function extractFieldErrors(error: ApiError) { if (!isRecord(error.details)) return {}; const fields: Record<string, string> = {}; for (const [key, value] of Object.entries(error.details)) { const fieldKey = key.charAt(0).toLowerCase() + key.slice(1); if (Array.isArray(value)) fields[fieldKey] = value.map(String).join(' '); else if (typeof value === 'string') fields[fieldKey] = value; else if (value !== null && value !== undefined) fields[fieldKey] = JSON.stringify(value); } return fields; }
function apiMessage(error: unknown, fallback: string) { return error instanceof ApiError ? error.message : fallback; }
function firstError(...errors: unknown[]) { return errors.find(Boolean); }
function isRecord(value: unknown): value is Record<string, unknown> { return typeof value === 'object' && value !== null; }
function formErrorTitle(error: ApiError, t: (key: string) => string) { if (error.status === 409) return t('reservations.conflictTitle'); if (error.status === 403) return t('reservations.forbiddenTitle'); return t('reservations.validationTitle'); }


