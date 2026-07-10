export const CAPABILITIES = ['Catalog.Manage', 'Reservations.Manage'] as const;
export type Capability = (typeof CAPABILITIES)[number];
export function isCapability(value: string): value is Capability {
  return CAPABILITIES.includes(value as Capability);
}
export function normalizeCapabilities(values: string[]): Capability[] {
  return values.filter(isCapability);
}
