import { format, isValid, parseISO } from 'date-fns';
import { es } from 'date-fns/locale/es';
const utcIsoPattern = /Z$/;
export function parseApiDate(utcIso: string): Date {
  if (!utcIsoPattern.test(utcIso))
    throw new Error('API date must be a UTC ISO-8601 string ending in Z.');
  const parsed = parseISO(utcIso);
  if (!isValid(parsed)) throw new Error('API date is not a valid ISO-8601 timestamp.');
  return parsed;
}
export function formatLocal(utcIso: string, fmt: string): string {
  return format(parseApiDate(utcIso), fmt, { locale: es });
}
export function toUtcIso(localDate: Date): string {
  if (!isValid(localDate)) throw new Error('Local date is invalid.');
  return localDate.toISOString();
}
