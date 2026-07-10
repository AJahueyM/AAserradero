import RefreshIcon from '@mui/icons-material/Refresh';
import {
  Alert,
  Box,
  Button,
  Chip,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TableSortLabel,
  Typography,
} from '@mui/material';
import { DatePicker } from '@mui/x-date-pickers';
import { useQuery } from '@tanstack/react-query';
import { endOfDay, startOfDay } from 'date-fns';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ApiError } from '../../api/apiError';
import { apiGet } from '../../api/httpClient';
import { formatLocal, toUtcIso } from '../../lib/datetime';
import { useNotify } from '../../ui/NotifyProvider';
import './i18n';

interface ReservationClientResponse {
  id: number;
  name: string;
  phone: string | null;
  cellphone: string;
}
interface ReservationRoomResponse {
  id: number;
  name: string;
  capacity: number;
  nightlyFare: number;
}
interface ReservationFinancialSummary {
  charges: number;
  payments: number;
  outstandingBalance: number;
}
interface ReservationSummaryResponse {
  id: number;
  client: ReservationClientResponse;
  room: ReservationRoomResponse;
  entryDate: string;
  exitDate: string;
  nights: number;
  adults: number;
  children: number;
  infants: number;
  pets: number;
  fare: number;
  financialSummary: ReservationFinancialSummary;
}
interface RoomLookupDto {
  id: number;
  areaName: string;
}
interface PagedResult<T> {
  items: T[];
  totalCount: number;
}
type DateMode = 'Arrivals' | 'Departures';
type SortKey = 'reference' | 'area' | 'room' | 'client' | 'occupants' | 'nights' | 'arrival' | 'departure' | 'balance' | 'contact';
type SortDirection = 'asc' | 'desc';
interface SortState {
  key: SortKey;
  direction: SortDirection;
}

const moneyFormatter = new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN' });

function searchReservations(mode: DateMode, start: Date, end: Date) {
  const query = new URLSearchParams({
    dateMode: mode,
    from: toUtcIso(startOfDay(start)),
    to: toUtcIso(endOfDay(end)),
  });
  return apiGet<ReservationSummaryResponse[]>(`/api/reservations?${query.toString()}`);
}
function fetchRooms() {
  return apiGet<PagedResult<RoomLookupDto>>('/api/rooms?page=1&pageSize=500');
}
function errorMessage(error: unknown, fallback: string) {
  return error instanceof ApiError ? error.message : fallback;
}

export default function CheckInOutPage() {
  const { t } = useTranslation();
  const notify = useNotify();
  const today = useMemo(() => new Date(), []);
  const [startDate, setStartDate] = useState<Date | null>(today);
  const [endDate, setEndDate] = useState<Date | null>(today);
  const [submitted, setSubmitted] = useState({ start: today, end: today });
  const rangeValid = Boolean(startDate && endDate && startOfDay(startDate) <= startOfDay(endDate));
  const arrivals = useQuery({ queryKey: ['checkinout', 'Arrivals', submitted.start, submitted.end], queryFn: () => searchReservations('Arrivals', submitted.start, submitted.end) });
  const departures = useQuery({ queryKey: ['checkinout', 'Departures', submitted.start, submitted.end], queryFn: () => searchReservations('Departures', submitted.start, submitted.end) });
  const rooms = useQuery({ queryKey: ['checkinout', 'rooms'], queryFn: fetchRooms, staleTime: 5 * 60 * 1000 });
  const roomAreas = useMemo(() => new Map((rooms.data?.items ?? []).map((room) => [room.id, room.areaName])), [rooms.data]);
  const refresh = () => {
    if (!startDate || !endDate || startOfDay(startDate) > startOfDay(endDate)) {
      notify.error(t('checkinout.invalidRange'));
      return;
    }
    setSubmitted({ start: startDate, end: endDate });
  };

  return (
    <Stack spacing={3}>
      <Box>
        <Typography variant="h4" component="h1">{t('checkinout.title')}</Typography>
        <Typography color="text.secondary">{t('checkinout.subtitle')}</Typography>
      </Box>
      <Paper sx={{ p: 2 }}>
        <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ alignItems: { md: 'center' } }}>
          <DatePicker label={t('checkinout.startDate')} value={startDate} onChange={setStartDate} />
          <DatePicker label={t('checkinout.endDate')} value={endDate} onChange={setEndDate} />
          <Button variant="contained" startIcon={<RefreshIcon />} onClick={refresh} disabled={!rangeValid || arrivals.isFetching || departures.isFetching}>
            {t('checkinout.refresh')}
          </Button>
        </Stack>
        {!rangeValid && <Alert severity="warning" sx={{ mt: 2 }}>{t('checkinout.invalidRange')}</Alert>}
      </Paper>
      <ReservationWindow title={t('checkinout.arrivals')} reservations={arrivals.data ?? []} isLoading={arrivals.isLoading || rooms.isLoading} error={arrivals.error} roomAreas={roomAreas} defaultSort={{ key: 'arrival', direction: 'asc' }} />
      <ReservationWindow title={t('checkinout.departures')} reservations={departures.data ?? []} isLoading={departures.isLoading || rooms.isLoading} error={departures.error} roomAreas={roomAreas} defaultSort={{ key: 'departure', direction: 'asc' }} />
    </Stack>
  );
}

function ReservationWindow({ title, reservations, isLoading, error, roomAreas, defaultSort }: { title: string; reservations: ReservationSummaryResponse[]; isLoading: boolean; error: unknown; roomAreas: Map<number, string>; defaultSort: SortState }) {
  const { t } = useTranslation();
  const [sort, setSort] = useState<SortState>(defaultSort);
  const sorted = useMemo(() => [...reservations].sort((a, b) => compareReservations(a, b, sort, roomAreas)), [reservations, roomAreas, sort]);
  const requestSort = (key: SortKey) => setSort((current) => ({ key, direction: current.key === key && current.direction === 'asc' ? 'desc' : 'asc' }));
  return (
    <Paper sx={{ p: 2 }}>
      <Stack spacing={2}>
        <Stack direction={{ xs: 'column', sm: 'row' }} sx={{ justifyContent: 'space-between' }} spacing={1}>
          <Typography variant="h5" component="h2">{title}</Typography>
          <Stack direction="row" spacing={1}><Chip label={t('checkinout.count', { count: reservations.length })} /><Chip variant="outlined" label={t('checkinout.capped')} /></Stack>
        </Stack>
        {isLoading && <Alert severity="info">{t('checkinout.loading')}</Alert>}
        {Boolean(error) && <Alert severity="error">{errorMessage(error, t('checkinout.loadError'))}</Alert>}
        {!isLoading && !error && reservations.length === 0 && <Alert severity="info">{t('checkinout.empty')}</Alert>}
        {sorted.length > 0 && (
          <TableContainer sx={{ maxHeight: 560 }}>
            <Table stickyHeader size="small" aria-label={title}>
              <TableHead><TableRow>{(['reference', 'area', 'room', 'client', 'occupants', 'nights', 'arrival', 'departure', 'balance', 'contact'] as SortKey[]).map((key) => <TableCell key={key} sortDirection={sort.key === key ? sort.direction : false} align={key === 'nights' || key === 'balance' ? 'right' : 'left'}><TableSortLabel active={sort.key === key} direction={sort.key === key ? sort.direction : 'asc'} onClick={() => requestSort(key)}>{t(`checkinout.${key}`)}</TableSortLabel></TableCell>)}</TableRow></TableHead>
              <TableBody>{sorted.map((reservation) => <TableRow key={reservation.id} hover><TableCell>{reservation.id}</TableCell><TableCell>{roomAreas.get(reservation.room.id) ?? t('checkinout.unavailable')}</TableCell><TableCell>{reservation.room.name}</TableCell><TableCell>{reservation.client.name}</TableCell><TableCell>{occupantsLabel(reservation, t)}</TableCell><TableCell align="right">{reservation.nights}</TableCell><TableCell>{formatLocal(reservation.entryDate, 'Pp')}</TableCell><TableCell>{formatLocal(reservation.exitDate, 'Pp')}</TableCell><TableCell align="right">{moneyFormatter.format(reservation.financialSummary.outstandingBalance)}</TableCell><TableCell>{reservation.client.cellphone || reservation.client.phone || t('checkinout.unavailable')}</TableCell></TableRow>)}</TableBody>
            </Table>
          </TableContainer>
        )}
      </Stack>
    </Paper>
  );
}

function occupantsLabel(reservation: ReservationSummaryResponse, t: (key: string, options?: Record<string, number>) => string) {
  const parts = [
    t('checkinout.adults', { count: reservation.adults }),
    reservation.children ? t('checkinout.children', { count: reservation.children }) : '',
    reservation.infants ? t('checkinout.infants', { count: reservation.infants }) : '',
    reservation.pets ? t('checkinout.pets', { count: reservation.pets }) : '',
  ].filter(Boolean);
  return parts.join(', ');
}
function compareReservations(a: ReservationSummaryResponse, b: ReservationSummaryResponse, sort: SortState, roomAreas: Map<number, string>) {
  const direction = sort.direction === 'asc' ? 1 : -1;
  const av = sortValue(a, sort.key, roomAreas);
  const bv = sortValue(b, sort.key, roomAreas);
  if (typeof av === 'number' && typeof bv === 'number') return (av - bv) * direction;
  return String(av).localeCompare(String(bv), 'es-MX', { numeric: true }) * direction;
}
function sortValue(reservation: ReservationSummaryResponse, key: SortKey, roomAreas: Map<number, string>): string | number {
  switch (key) {
    case 'reference': return reservation.id;
    case 'area': return roomAreas.get(reservation.room.id) ?? '';
    case 'room': return reservation.room.name;
    case 'client': return reservation.client.name;
    case 'occupants': return reservation.adults + reservation.children + reservation.infants;
    case 'nights': return reservation.nights;
    case 'arrival': return new Date(reservation.entryDate).getTime();
    case 'departure': return new Date(reservation.exitDate).getTime();
    case 'balance': return reservation.financialSummary.outstandingBalance;
    case 'contact': return reservation.client.cellphone || reservation.client.phone || '';
  }
}
