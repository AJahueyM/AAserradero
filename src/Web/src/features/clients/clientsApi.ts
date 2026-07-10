import { apiGet, apiPost } from '../../api/httpClient';

export interface ClientDto {
  id: number;
  name: string;
  taxId: string | null;
  address: string | null;
  email: string | null;
  phone: string | null;
  cellphone: string;
  isVip: boolean;
  isBlacklisted: boolean;
  blacklistReason: string | null;
  isActive: boolean;
  recentActivityCount: number;
}

export interface ClientListResponse {
  items: ClientDto[];
  total: number;
  page: number;
  pageSize: number;
}

export interface CreateClientRequest {
  name: string;
  taxId?: string | null;
  address?: string | null;
  email?: string | null;
  phone?: string | null;
  cellphone: string;
}

export interface SearchClientsParams {
  name?: string;
  isVip?: boolean;
  page?: number;
  pageSize?: number;
}

export async function searchClients(params: SearchClientsParams): Promise<ClientListResponse> {
  const query = new URLSearchParams();
  if (params.name) query.set('name', params.name);
  if (params.isVip !== undefined) query.set('isVip', String(params.isVip));
  query.set('page', String(params.page ?? 1));
  query.set('pageSize', String(params.pageSize ?? 20));
  return apiGet<ClientListResponse>(`/api/clients?${query.toString()}`);
}

export async function getClient(id: number): Promise<ClientDto> {
  return apiGet<ClientDto>(`/api/clients/${id}`);
}

export async function createClient(body: CreateClientRequest): Promise<ClientDto> {
  return apiPost<CreateClientRequest, ClientDto>('/api/clients', body);
}
