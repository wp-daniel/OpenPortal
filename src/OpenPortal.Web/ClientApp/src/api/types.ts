/**
 * Every API type the client exchanges with the server.
 *
 * These are hand-written mirrors of the backend contracts rather than a generated copy. The generator would
 * need the compiled server on every build; this file costs one manual edit per contract change, which the
 * type checker then verifies in both directions the moment either side drifts.
 *
 * All properties are camelCase because the API serializes with the Web defaults.
 */

// ---------------------------------------------------------------------------
// Problem details. Every non-2xx response carries one of these.
// ---------------------------------------------------------------------------

export interface ValidationProblemDetails {
  readonly type?: string
  readonly title?: string
  readonly status?: number
  readonly detail?: string
  readonly instance?: string
  readonly traceId?: string
  readonly errorCode?: string
  readonly errors?: Record<string, readonly string[]>
}

// ---------------------------------------------------------------------------
// Session and account.
// ---------------------------------------------------------------------------

export interface SessionUser {
  readonly id: string
  readonly email: string
  readonly displayName: string
  readonly emailConfirmed: boolean
  readonly roles: readonly string[]
  readonly language: string | null
}

/**
 * The password policy the server enforces, published through the session endpoint.
 *
 * Sent even to anonymous callers: the sign-in and change-password forms need it before anyone is signed in.
 * It describes configuration, not account data, so there is nothing here to protect.
 */
export interface PasswordPolicy {
  readonly requiredLength: number
  readonly requiredUniqueChars: number
  readonly requireLowercase: boolean
  readonly requireUppercase: boolean
  readonly requireDigit: boolean
  readonly requireNonAlphanumeric: boolean
}

export interface Session {
  readonly isAuthenticated: boolean
  readonly user: SessionUser | null
  readonly passwordPolicy: PasswordPolicy
}

export interface AccountProfile {
  readonly id: string
  readonly email: string
  readonly displayName: string
  readonly emailConfirmed: boolean
  readonly createdAtUtc: string
  readonly updatedAtUtc: string | null
  readonly roles: readonly string[]
  readonly language: string | null
}

export interface UpdateProfileRequest {
  readonly displayName: string
}

export interface ChangePasswordRequest {
  readonly currentPassword: string
  readonly newPassword: string
}

export interface AntiforgeryToken {
  readonly requestToken: string
  readonly headerName: string
}

// ---------------------------------------------------------------------------
// User administration.
// ---------------------------------------------------------------------------

export interface UserSummary {
  readonly id: string
  readonly email: string
  readonly displayName: string
  readonly emailConfirmed: boolean
  readonly isLockedOut: boolean
  readonly lockoutEndUtc: string | null
  readonly createdAtUtc: string
  readonly roles: readonly string[]
}

export interface PagedResult<T> {
  readonly items: readonly T[]
  readonly page: number
  readonly pageSize: number
  readonly totalCount: number
  readonly totalPages: number
  readonly hasPrevious: boolean
  readonly hasNext: boolean
}

export interface CreateUserRequest {
  readonly email: string
  readonly password: string
  readonly displayName?: string | null
  readonly roles: readonly string[]
}

export interface UpdateUserRequest {
  readonly displayName: string
  readonly roles: readonly string[]
}

// ---------------------------------------------------------------------------
// Content.
// ---------------------------------------------------------------------------

export interface Technology {
  readonly name: string
}

export interface SocialLink {
  readonly platform: string
  readonly url: string
  readonly label: string | null
}

/**
 * A project as the management screens receive it.
 *
 * A distinct shape from the public `Project`: the editor needs the row id to publish or delete, plus the
 * published flag, the position and the updated timestamp, none of which are reader-facing.
 */
export interface ManagedProject {
  readonly id: string
  readonly slug: string
  readonly name: string
  readonly summary: string | null
  readonly description: string | null
  readonly url: string | null
  readonly repositoryUrl: string | null
  readonly position: number | null
  readonly isPublished: boolean
  readonly startedOn: string | null
  readonly completedOn: string | null
  readonly updatedAtUtc: string
  readonly technologies: readonly Technology[]
}

export interface Project {
  readonly slug: string
  readonly name: string
  readonly summary: string | null
  readonly description: string | null
  readonly url: string | null
  readonly repositoryUrl: string | null
  readonly startedOn: string | null
  readonly completedOn: string | null
  readonly technologies: readonly Technology[]
}

export interface Profile {
  readonly displayName: string
  readonly headline: string | null
  readonly summary: string | null
  readonly location: string | null
  readonly email: string | null
  readonly avatarUrl: string | null
  readonly socialLinks: readonly SocialLink[]
  readonly projects: readonly Project[]
}

export interface PublicContent {
  readonly profile: Profile | null
  readonly projects: readonly Project[]
}

export interface ProfileRequest {
  readonly id: string | null
  readonly displayName: string
  readonly headline: string | null
  readonly summary: string | null
  readonly location: string | null
  readonly email: string | null
  readonly avatarUrl: string | null
  readonly socialLinks: readonly SocialLinkRequest[]
}

export interface SocialLinkRequest {
  readonly platform: string
  readonly url: string
  readonly label: string | null
}

export interface ProjectRequest {
  readonly id: string | null
  readonly name: string
  readonly slug: string
  readonly summary: string | null
  readonly description: string | null
  readonly url: string | null
  readonly repositoryUrl: string | null
  readonly position: number | null
  readonly isPublished: boolean
  readonly startedOn: string | null
  readonly completedOn: string | null
  readonly technologies: readonly string[]
}