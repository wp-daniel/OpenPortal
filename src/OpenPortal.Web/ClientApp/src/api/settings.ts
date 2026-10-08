import { api } from './client'
import type { SecuritySettings, UpdateSecuritySettingsRequest } from './types'

/** Portal settings kept in the database and edited by administrators. */
export const settingsApi = {
  security: (signal?: AbortSignal) => api.get<SecuritySettings>('/api/admin/settings/security', signal),
  updateSecurity: (body: UpdateSecuritySettingsRequest) =>
    api.put<SecuritySettings>('/api/admin/settings/security', body),
}
