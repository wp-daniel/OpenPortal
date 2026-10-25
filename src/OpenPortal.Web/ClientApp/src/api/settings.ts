import { api } from './client'
import type {
  MailServerRequest,
  MailSettings,
  MailTestResult,
  OutboxMessage,
  OutboxPage,
  OutboxStatus,
  ExternalProviderKey,
  ExternalProviderSettings,
  PrivacySettings,
  RegistrationSettings,
  UpdateExternalProviderRequest,
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
  privacy: (signal?: AbortSignal) => api.get<PrivacySettings>('/api/admin/settings/privacy', signal),
  updatePrivacy: (body: { selfDeletionEnabled: boolean; deletionGraceDays: number }) =>
    api.put<PrivacySettings>('/api/admin/settings/privacy', body),
  externalLogins: (signal?: AbortSignal) => api.get<ExternalProviderSettings[]>('/api/admin/settings/external-logins', signal),
  updateExternalLogin: (provider: ExternalProviderKey, body: UpdateExternalProviderRequest) =>
    api.put<ExternalProviderSettings>(`/api/admin/settings/external-logins/${provider}`, body),
  email: (signal?: AbortSignal) => api.get<MailSettings>('/api/admin/settings/email', signal),
  updateEmail: (body: MailServerRequest) => api.put<MailSettings>('/api/admin/settings/email', body),
  testEmail: (body: MailServerRequest & { to: string }) => api.post<MailTestResult>('/api/admin/settings/email/test', body),
}

/** The delivery queue: emails and calls to applications waiting, delivered or failed. */
export const outboxApi = {
  list: (status: OutboxStatus | '', signal?: AbortSignal) =>
    api.get<OutboxPage>(`/api/admin/outbox?pageSize=50${status ? `&status=${status}` : ''}`, signal),
  retry: (id: string) => api.post<OutboxMessage>(`/api/admin/outbox/${id}/retry`),
  retryFailed: () => api.post<{ count: number }>('/api/admin/outbox/retry-failed'),
  remove: (id: string) => api.delete<void>(`/api/admin/outbox/${id}`),
}
