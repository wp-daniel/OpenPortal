import { api, invalidateAntiforgeryToken } from './client'
import type {
  AccountDeletion,
  AccountExternalLogins,
  AccountSessions,
  AuthorizedApplication,
  ExternalProviderKey,
  CreatedPersonalAccessToken,
  PersonalAccessToken,
  PersonalAccessTokenScope,
  AccountProfile,
  ChangePasswordRequest,
  Passkey,
  RecoveryCodes,
  Session,
  TwoFactorLoginRequest,
  TwoFactorSetup,
  TwoFactorStatus,
  UpdateProfileRequest,
} from './types'
import { avatarForm } from './users'

export const authApi = {
  session: (signal?: AbortSignal) => api.get<Session>('/api/auth/session', signal),

  /**
   * Signing in changes the identity the antiforgery token is bound to, so the cached token is discarded on
   * both success and failure. Keeping the pre-sign-in token would make the first write after sign-in fail.
   */
  async login(email: string, password: string, rememberMe: boolean): Promise<Session> {
    try {
      return await api.post<Session>('/api/auth/login', { email, password, rememberMe })
    } finally {
      invalidateAntiforgeryToken()
    }
  },

  /** The second sign-in step, after `login` failed with `identity.two_factor_required`. */
  async loginTwoFactor(body: TwoFactorLoginRequest): Promise<Session> {
    try {
      return await api.post<Session>('/api/auth/login/two-factor', body)
    } finally {
      invalidateAntiforgeryToken()
    }
  },

  /** A challenge any passkey of this portal can answer (WebAuthn's JSON form). */
  passkeyOptions: () => api.post<PublicKeyCredentialRequestOptionsJSON>('/api/auth/passkey/options'),

  /** Signs in with the credential the browser returned; the identity changes, so the token goes. */
  async loginPasskey(credential: string, rememberMe: boolean): Promise<Session> {
    try {
      return await api.post<Session>('/api/auth/login/passkey', { credential, rememberMe })
    } finally {
      invalidateAntiforgeryToken()
    }
  },

  async logout(): Promise<void> {
    try {
      await api.post<void>('/api/auth/logout')
    } finally {
      invalidateAntiforgeryToken()
    }
  },

  /** Starts a support session as another account (administrators only). The identity changes: a new token follows. */
  async impersonate(userId: string, reason: string): Promise<void> {
    try {
      await api.post<void>(`/api/admin/users/${userId}/impersonate`, { reason })
    } finally {
      invalidateAntiforgeryToken()
    }
  },

  /** Ends the support session: back to the administrator's own account. */
  async stopImpersonation(): Promise<void> {
    try {
      await api.post<void>('/api/auth/impersonation/stop')
    } finally {
      invalidateAntiforgeryToken()
    }
  },
}

export const accountApi = {
  profile: (signal?: AbortSignal) => api.get<AccountProfile>('/api/account/profile', signal),
  updateProfile: (body: UpdateProfileRequest) => api.put<AccountProfile>('/api/account/profile', body),
  /** Saves the preferred UI language (null clears it). */
  updateLanguage: (language: string | null) => api.put<AccountProfile>('/api/account/language', { language }),
  changePassword: (body: ChangePasswordRequest) => api.post<void>('/api/account/change-password', body),
  /** For an account without a password (created through Google or Microsoft). */
  setPassword: (newPassword: string) => api.post<void>('/api/account/set-password', { newPassword }),

  externalLogins: (signal?: AbortSignal) => api.get<AccountExternalLogins>('/api/account/external-logins', signal),

  sessions: (signal?: AbortSignal) => api.get<AccountSessions>('/api/account/sessions', signal),

  authorizedApplications: (signal?: AbortSignal) =>
    api.get<AuthorizedApplication[]>('/api/account/authorized-applications', signal),
  revokeAuthorizedApplication: (clientId: string) =>
    api.delete<void>(`/api/account/authorized-applications/${encodeURIComponent(clientId)}`),
  revokeSession: (sessionId: string) => api.delete<void>(`/api/account/sessions/${encodeURIComponent(sessionId)}`),
  revokeOtherSessions: () => api.post<{ count: number }>('/api/account/sessions/revoke-others'),
  removeExternalLogin: (provider: ExternalProviderKey) => api.delete<void>(`/api/account/external-logins/${provider}`),
  uploadAvatar: (image: Blob) => api.put<void>('/api/account/avatar', avatarForm(image)),
  removeAvatar: () => api.delete<void>('/api/account/avatar'),

  twoFactor: (signal?: AbortSignal) => api.get<TwoFactorStatus>('/api/account/two-factor', signal),
  /** The key to scan; the same key every time until two-factor is turned on. */
  setupTwoFactor: () => api.post<TwoFactorSetup>('/api/account/two-factor/setup'),
  enableTwoFactor: (code: string) => api.post<RecoveryCodes>('/api/account/two-factor/enable', { code }),
  disableTwoFactor: (password: string) => api.post<void>('/api/account/two-factor/disable', { password }),
  regenerateRecoveryCodes: (password: string) =>
    api.post<RecoveryCodes>('/api/account/two-factor/recovery-codes', { password }),

  passkeys: (signal?: AbortSignal) => api.get<Passkey[]>('/api/account/passkeys', signal),
  passkeyCreationOptions: () => api.post<PublicKeyCredentialCreationOptionsJSON>('/api/account/passkeys/creation-options'),
  registerPasskey: (credential: string, name: string) => api.post<Passkey>('/api/account/passkeys', { credential, name }),
  renamePasskey: (id: string, name: string) => api.put<Passkey>(`/api/account/passkeys/${encodeURIComponent(id)}`, { name }),
  deletePasskey: (id: string) => api.delete<void>(`/api/account/passkeys/${encodeURIComponent(id)}`),

  /** A ZIP with everything the portal holds about the caller. */
  exportData: (password: string) => api.postForFile('/api/account/data-export', { password }),
  /** Schedules the deletion of the caller's account and signs them out (the antiforgery token goes with the identity). */
  async requestDeletion(password: string): Promise<AccountDeletion> {
    try {
      return await api.post<AccountDeletion>('/api/account/deletion', { password })
    } finally {
      invalidateAntiforgeryToken()
    }
  },
  cancelDeletion: () => api.delete<void>('/api/account/deletion'),

  tokens: (signal?: AbortSignal) => api.get<PersonalAccessToken[]>('/api/account/tokens', signal),
  createToken: (body: { name: string; scopes: PersonalAccessTokenScope[]; lifetimeDays: number }) =>
    api.post<CreatedPersonalAccessToken>('/api/account/tokens', body),
  revokeToken: (tokenId: string) => api.delete<void>(`/api/account/tokens/${tokenId}`),
}

/** The error code the password step answers with when the account still needs its second factor. */
export const TWO_FACTOR_REQUIRED = 'identity.two_factor_required'

/** The pending sign-in expired between the two steps; the password has to be entered again. */
export const TWO_FACTOR_SESSION_EXPIRED = 'identity.two_factor_session_expired'