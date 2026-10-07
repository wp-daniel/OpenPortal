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
  /** When the profile picture last changed; null when there is none. Versions the avatar URL. */
  readonly avatarUpdatedAtUtc: string | null
  /** Keys of the portal pages the user may open (every page for an administrator). */
  readonly pages: readonly string[]
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

export interface AccountProfile extends UserDetails {
  readonly id: string
  readonly email: string
  readonly displayName: string
  readonly emailConfirmed: boolean
  readonly createdAtUtc: string
  readonly updatedAtUtc: string | null
  readonly roles: readonly string[]
  readonly language: string | null
  readonly avatarUpdatedAtUtc: string | null
}

/** Personal, work and address details, shared by every request that creates or edits an account. */
export interface UserDetails {
  readonly firstName: string
  readonly lastName: string
  readonly phoneNumber: string | null
  readonly jobTitle: string | null
  readonly company: string | null
  readonly department: string | null
  readonly addressLine: string | null
  readonly city: string | null
  readonly postalCode: string | null
  readonly country: string | null
}

export type UpdateProfileRequest = UserDetails

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

export interface UserSummary extends UserDetails {
  readonly id: string
  readonly email: string
  readonly displayName: string
  readonly emailConfirmed: boolean
  readonly isLockedOut: boolean
  readonly lockoutEndUtc: string | null
  readonly createdAtUtc: string
  readonly isAdministrator: boolean
  readonly avatarUpdatedAtUtc: string | null
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

export interface CreateUserRequest extends UserDetails {
  readonly email: string
  readonly password: string
  /** Only an administrator may set it; the server refuses it otherwise. */
  readonly isAdministrator: boolean
}

export interface UpdateUserRequest extends UserDetails {
  readonly isAdministrator: boolean
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
// ---------------------------------------------------------------------------
// Applications, groups and access.
// ---------------------------------------------------------------------------

export type ApplicationStatus = 'pending' | 'active' | 'disabled'

export interface ApplicationSummary {
  readonly id: string
  readonly clientId: string
  readonly displayName: string
  readonly description: string | null
  readonly baseUrl: string
  readonly redirectUris: readonly string[]
  readonly postLogoutRedirectUris: readonly string[]
  readonly status: ApplicationStatus
  readonly source: 'manual' | 'announced'
  readonly version: string | null
  readonly announcedRedirectUris: readonly string[]
  readonly announcedPostLogoutRedirectUris: readonly string[]
  readonly hasManifestChanges: boolean
  readonly createdAtUtc: string
  readonly updatedAtUtc: string | null
  readonly lastSeenAtUtc: string | null
  readonly userCount: number
  readonly groupCount: number
}

/** Returned when a client secret is issued. The secret cannot be read again afterwards. */
export interface ApplicationSecret {
  readonly application: ApplicationSummary
  readonly clientSecret: string
}

export interface UpdateApplicationRequest {
  readonly displayName: string
  readonly description: string | null
  readonly baseUrl: string
  readonly redirectUris: readonly string[]
  readonly postLogoutRedirectUris: readonly string[]
}

export interface CreateApplicationRequest extends UpdateApplicationRequest {
  readonly clientId: string
}

export interface UserReference {
  readonly id: string
  readonly email: string
  readonly displayName: string
  /** False when the account no longer exists. */
  readonly isKnown: boolean
}

export interface ApplicationReference {
  readonly id: string
  readonly clientId: string
  readonly displayName: string
  readonly status: ApplicationStatus
}

export interface GroupReference {
  readonly id: string
  readonly name: string
}

export interface GroupSummary {
  readonly id: string
  readonly name: string
  readonly description: string | null
  readonly memberCount: number
  readonly applicationCount: number
  readonly createdAtUtc: string
}

export interface GroupDetail {
  readonly id: string
  readonly name: string
  readonly description: string | null
  readonly members: readonly UserReference[]
  readonly applications: readonly ApplicationReference[]
  /** Keys of the portal pages granted to the group. */
  readonly pages: readonly string[]
  readonly createdAtUtc: string
}

export interface SaveGroupRequest {
  readonly name: string
  readonly description: string | null
}

export interface AccessTreeGroup {
  readonly id: string
  readonly name: string
  readonly members: readonly UserReference[]
}

export interface AccessTreeApplication {
  readonly id: string
  readonly clientId: string
  readonly displayName: string
  readonly status: ApplicationStatus
  readonly lastSeenAtUtc: string | null
  readonly groups: readonly AccessTreeGroup[]
  readonly users: readonly UserReference[]
}

export interface AccessTree {
  readonly applications: readonly AccessTreeApplication[]
}

export interface UserApplicationAccess {
  readonly applicationId: string
  readonly clientId: string
  readonly displayName: string
  readonly status: ApplicationStatus
  readonly direct: boolean
  readonly viaGroups: readonly GroupReference[]
}

export interface UserAccess {
  readonly userId: string
  readonly groups: readonly GroupReference[]
  readonly applications: readonly UserApplicationAccess[]
}

export interface LaunchpadItem {
  readonly id: string
  readonly clientId: string
  readonly displayName: string
  readonly description: string | null
  readonly baseUrl: string
}

/** A page of the portal that can be granted to groups. Labels are translation keys. */
export interface PortalPage {
  readonly key: string
  readonly labelKey: string
  readonly areaKey: string
}

export interface PageGrant {
  readonly pageKey: string
  readonly groupId: string
}

/** Grantable pages × groups, and which group may open which page. */
export interface PagePermissionMatrix {
  readonly pages: readonly PortalPage[]
  readonly groups: readonly GroupReference[]
  readonly grants: readonly PageGrant[]
}
