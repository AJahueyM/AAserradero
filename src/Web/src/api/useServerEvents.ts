import { useEffect, useMemo, useState } from 'react';
import { getAccessTokenForApi, toApiUrl } from './httpClient';

export type ServerEventStatus = 'idle' | 'connecting' | 'open' | 'error';
export interface ServerEventMessage<TPayload = unknown> {
  type: string;
  payload: TPayload;
  receivedAt: Date;
}
interface UseServerEventsOptions<TPayload> {
  enabled?: boolean;
  onMessage?: (message: ServerEventMessage<TPayload>) => void;
}

export function useServerEvents<TPayload = unknown>({
  enabled = true,
  onMessage,
}: UseServerEventsOptions<TPayload> = {}) {
  const [status, setStatus] = useState<ServerEventStatus>('idle');
  const [lastMessage, setLastMessage] = useState<ServerEventMessage<TPayload> | null>(null);
  useEffect(() => {
    if (!enabled) return;
    let eventSource: EventSource | null = null;
    let cancelled = false;
    async function connect() {
      setStatus('connecting');
      const token = await getAccessTokenForApi();
      if (cancelled) return;
      const url = new URL(toApiUrl('/api/events'), window.location.origin);
      url.searchParams.set('access_token', token);
      eventSource = new EventSource(url.toString());
      eventSource.onopen = () => setStatus('open');
      eventSource.onerror = () => setStatus('error');
      eventSource.onmessage = (event) => {
        const message: ServerEventMessage<TPayload> = {
          type: event.type,
          payload: parseEventPayload<TPayload>(event.data),
          receivedAt: new Date(),
        };
        setLastMessage(message);
        onMessage?.(message);
      };
    }
    void connect().catch(() => setStatus('error'));
    return () => {
      cancelled = true;
      eventSource?.close();
    };
  }, [enabled, onMessage]);
  return useMemo(() => ({ status, lastMessage }), [lastMessage, status]);
}
function parseEventPayload<TPayload>(data: string): TPayload {
  try {
    return JSON.parse(data) as TPayload;
  } catch {
    return data as TPayload;
  }
}
