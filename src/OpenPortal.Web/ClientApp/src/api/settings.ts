import { api } from './client'
import type {
  RegistrationSettings,
  SecuritySettings,
  UpdateRegistrationSettingsRequest,
  UpdateSecuritySettingsRequest,
} from './types'

/** Portal settings kept in the database and edited by administrators. */
export const settingsApi = {
  security: (signal?: AbortSignal) => api.get<SecuritySettings>('/api/admin/settings/security', signal),
  updateSecurity: (body: UpdateSecuritySettingsRequest) =>
    api.put<SecuritySettings>('/api/admin/settings/security', body),
  registration: (signal?: AbortSignal) => api.get<RegistrationSettings>('/api/admin/settings/registration', signal),
  updateRegistration: (body: UpdateRegistrationSettingsRequest) =>
    api.put<RegistrationSettings>('/api/admin/settings/registration', body),
}
