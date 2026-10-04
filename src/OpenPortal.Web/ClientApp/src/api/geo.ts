import { api, withQuery } from './client'

/** A town or city, its region, and a postal code when the provider knows one. */
export interface GeoPlace {
  readonly city: string
  readonly region: string | null
  readonly postalCode: string | null
}

/** `available` is false when the provider could not be reached: no suggestions, but no error either. */
export interface GeoLookup {
  readonly places: readonly GeoPlace[]
  readonly available: boolean
}

export const geoKeys = {
  postalCode: (country: string, postalCode: string) => ['geo', 'postal-code', country, postalCode] as const,
  cities: (country: string, query: string) => ['geo', 'cities', country, query] as const,
}

/** Address suggestions, looked up by the server (which caches them and keeps the user's input private). */
export const geoApi = {
  postalCode: (country: string, postalCode: string, signal?: AbortSignal) =>
    api.get<GeoLookup>(`/api/geo/postal-codes/${country}/${encodeURIComponent(postalCode)}`, signal),

  cities: (country: string, query: string, signal?: AbortSignal) =>
    api.get<GeoLookup>(withQuery('/api/geo/cities', { country, q: query }), signal),
}
