import { ApiError, isApiErrorBody } from './apiError';

interface HttpClientConfig {
  getAccessToken: () => Promise<string>;
  onUnauthorized: () => Promise<void>;
}
let config: HttpClientConfig | null = null;
export function configureHttpClient(nextConfig: HttpClientConfig) {
  config = nextConfig;
}
export async function apiGet<TResponse>(path: string, init?: Omit<RequestInit, 'method'>) {
  return apiRequest<TResponse>(path, { ...init, method: 'GET' });
}
export async function apiPost<TRequest, TResponse>(
  path: string,
  body: TRequest,
  init?: Omit<RequestInit, 'method' | 'body'>,
) {
  return apiRequest<TResponse>(path, { ...init, method: 'POST', body: JSON.stringify(body) });
}

export async function apiRequest<TResponse>(
  path: string,
  init: RequestInit = {},
): Promise<TResponse> {
  if (!config) throw new Error('HTTP client has not been configured.');
  const token = await config.getAccessToken();
  const headers = new Headers(init.headers);
  headers.set('Authorization', `Bearer ${token}`);
  if (init.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');
  const response = await fetch(toApiUrl(path), { ...init, headers });
  if (!response.ok) {
    const apiError = await parseError(response);
    if (response.status === 401) await config.onUnauthorized();
    throw apiError;
  }
  if (response.status === 204) return undefined as TResponse;
  return (await response.json()) as TResponse;
}

export async function getAccessTokenForApi() {
  if (!config) throw new Error('HTTP client has not been configured.');
  return config.getAccessToken();
}
export function toApiUrl(path: string) {
  const baseUrl = import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, '') ?? '';
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  return `${baseUrl}${normalizedPath}`;
}
async function parseError(response: Response): Promise<ApiError> {
  const payload = await safeReadJson(response);
  if (isApiErrorBody(payload))
    return new ApiError({
      code: payload.error.code,
      message: payload.error.message,
      details: payload.error.details,
      status: response.status,
    });
  return new ApiError({
    code: 'UnexpectedError',
    message: response.statusText || 'Unexpected API error',
    status: response.status,
  });
}
async function safeReadJson(response: Response): Promise<unknown> {
  try {
    return await response.json();
  } catch {
    return null;
  }
}
