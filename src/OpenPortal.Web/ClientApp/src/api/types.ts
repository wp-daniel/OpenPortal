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

/** Whether the sign-in page offers to create an account. Public, like the password policy. */
export interface RegistrationInfo {
  readonly enabled: boolean
  readonly requiresApproval: boolean
}

export interface Session {
  readonly isAuthenticated: boolean
  readonly user: SessionUser | null
  readonly passwordPolicy: PasswordPolicy
  readonly registration: RegistrationInfo
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
  /** The last portal sign-in with a password; null if the account never signed in. */
  readonly lastSignInAtUtc: string | null
  /** Whether the account has an authenticator app enrolled. */
  readonly twoFactorEnabled: boolean
  /** Self-registered and waiting for an administrator; it cannot sign in until approved. */
  readonly pendingApproval: boolean
  /** How the account came to exist. */
  readonly origin: AccountOrigin
}

export type AccountOrigin = 'created' | 'registered' | 'invited' | 'provisioned'

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
  /** The roles the application understands, assigned on grants and sent as `role` claims. */
  readonly roles: readonly ApplicationRole[]
  /** Which of a user's groups the application receives in the `groups` claim. */
  readonly groupClaims: GroupClaimMode
  /** What the application's backend may do with a token of its own (client credentials); empty for nothing. */
  readonly apiScopes: readonly ApplicationApiScope[]
  /** Users who cannot open it see it in their catalog and may ask for it. */
  readonly allowAccessRequests: boolean
  /** Whether tokens name the organization the user signs in for. */
  readonly organizations: OrganizationMode
}

export type GroupClaimMode = 'none' | 'granted' | 'all'

export type OrganizationMode = 'none' | 'optional' | 'required'

export type ApplicationApiScope = 'portal.users.read' | 'portal.access.manage' | 'portal.users.create'

/** A role an application understands. The key is what the application checks. */
export interface ApplicationRole {
  readonly key: string
  readonly displayName: string | null
  readonly description: string | null
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
  /** The complete list of roles; a role left out is removed with its assignments. */
  readonly roles: readonly ApplicationRole[]
  readonly groupClaims: GroupClaimMode
  /** The complete list of application API scopes. */
  readonly apiScopes: readonly ApplicationApiScope[]
  readonly allowAccessRequests: boolean
  readonly organizations: OrganizationMode
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
  /** Role keys that come with the group's grant. */
  readonly roles: readonly string[]
}

/** A user granted an application directly, with the roles of that grant. */
export interface AccessTreeUser extends UserReference {
  readonly roles: readonly string[]
}

export interface AccessTreeApplication {
  readonly id: string
  readonly clientId: string
  readonly displayName: string
  readonly status: ApplicationStatus
  readonly lastSeenAtUtc: string | null
  readonly groups: readonly AccessTreeGroup[]
  readonly users: readonly AccessTreeUser[]
  readonly roles: readonly ApplicationRole[]
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
  /** The user's roles in the application, from every grant together. */
  readonly roles: readonly string[]
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

// ---------------------------------------------------------------------------
// Audit log.
// ---------------------------------------------------------------------------

export type AuditOutcome = 'success' | 'failure'

/** An account, application or other subject named in an audit entry, as it was at the time. */
export interface AuditSubject {
  readonly type: string
  readonly id: string
  readonly label: string | null
}

export interface AuditEntry {
  readonly id: string
  readonly occurredAtUtc: string
  /** Dotted action code, translated as `audit.action.<code>`. */
  readonly action: string
  readonly outcome: AuditOutcome
  readonly actor: AuditSubject | null
  readonly target: AuditSubject | null
  readonly details: Readonly<Record<string, string | null>>
  readonly ipAddress: string | null
  readonly userAgent: string | null
  readonly correlationId: string | null
}

export interface AuditFilter {
  readonly search?: string
  readonly category?: string
  readonly outcome?: string
  /** Only entries where this id is the actor or the target. */
  readonly subject?: string
  readonly from?: string
  readonly to?: string
}

// ---------------------------------------------------------------------------
// Two-factor authentication (authenticator app) and the portal's security settings.
// ---------------------------------------------------------------------------

export interface TwoFactorStatus {
  /** Whether the portal offers two-factor authentication (an administrator's setting). */
  readonly available: boolean
  /** Whether this account has an authenticator app enrolled. */
  readonly enabled: boolean
  readonly recoveryCodesLeft: number
}

export interface TwoFactorSetup {
  /** The key in groups of four, for typing into the app. */
  readonly sharedKey: string
  /** The same key as an otpauth URI, rendered as a QR code. */
  readonly authenticatorUri: string
}

export interface RecoveryCodes {
  readonly codes: readonly string[]
}

export interface TwoFactorLoginRequest {
  readonly code: string
  readonly useRecoveryCode: boolean
  readonly rememberMe: boolean
  readonly rememberBrowser: boolean
}

export interface SecuritySettings {
  readonly twoFactorEnabled: boolean
  readonly twoFactorIssuer: string
  readonly updatedAtUtc: string | null
}

export interface UpdateSecuritySettingsRequest {
  readonly twoFactorEnabled: boolean
  readonly twoFactorIssuer: string
}

// ---------------------------------------------------------------------------
// Notifications.
// ---------------------------------------------------------------------------

/** One notification. Its text is `notification.<type>.title` / `.body`, filled with `parameters`. */
export interface PortalNotification {
  readonly id: string
  readonly type: string
  readonly parameters: Readonly<Record<string, string | null>>
  /** A local path of the portal to open, or null. */
  readonly link: string | null
  readonly createdAtUtc: string
  readonly readAtUtc: string | null
}

export interface NotificationPage {
  readonly items: readonly PortalNotification[]
  readonly page: number
  readonly pageSize: number
  readonly totalCount: number
  readonly totalPages: number
  readonly hasPrevious: boolean
  readonly hasNext: boolean
  /** Unread notifications in total, whatever the filter. */
  readonly unreadCount: number
}

export interface NotificationCount {
  readonly unreadCount: number
  readonly latestAtUtc: string | null
}

// ---------------------------------------------------------------------------
// Access requests.
// ---------------------------------------------------------------------------

export type AccessRequestStatus = 'pending' | 'approved' | 'rejected' | 'cancelled'

/** One of the signed-in user's own requests. */
export interface MyAccessRequest {
  readonly id: string
  readonly applicationId: string
  readonly applicationName: string
  readonly reason: string | null
  readonly status: AccessRequestStatus
  readonly createdAtUtc: string
  readonly decidedAtUtc: string | null
  readonly decisionNote: string | null
}

/** An application the user cannot open but may ask for, with their latest request. */
export interface RequestableApplication {
  readonly id: string
  readonly clientId: string
  readonly displayName: string
  readonly description: string | null
  readonly latestRequest: MyAccessRequest | null
}

/** A request as reviewers see it. */
export interface AccessRequest {
  readonly id: string
  readonly application: ApplicationReference
  readonly user: UserReference
  readonly reason: string | null
  readonly status: AccessRequestStatus
  readonly createdAtUtc: string
  readonly decidedAtUtc: string | null
  readonly decidedBy: UserReference | null
  readonly decisionNote: string | null
  /** Role keys the approval granted. */
  readonly roles: readonly string[]
  /** The roles the application defines, to choose from when approving. */
  readonly applicationRoles: readonly ApplicationRole[]
  /** The user can open the application already (granted some other way meanwhile). */
  readonly hasAccess: boolean
}

export interface AccessRequestPage {
  readonly items: readonly AccessRequest[]
  readonly page: number
  readonly pageSize: number
  readonly totalCount: number
  readonly pendingCount: number
}

// ---------------------------------------------------------------------------
// Organizations.
// ---------------------------------------------------------------------------

export type OrganizationRole = 'member' | 'admin'

export interface OrganizationSummary {
  readonly id: string
  readonly name: string
  readonly slug: string
  readonly description: string | null
  readonly memberCount: number
  readonly createdAtUtc: string
}

export interface OrganizationMember extends UserReference {
  readonly role: OrganizationRole
  readonly addedAtUtc: string
}

export interface OrganizationDetail {
  readonly id: string
  readonly name: string
  readonly slug: string
  readonly description: string | null
  readonly members: readonly OrganizationMember[]
  readonly createdAtUtc: string
  readonly updatedAtUtc: string | null
}

/** An organization a user belongs to, as applications are told about it. */
export interface OrganizationMembership {
  readonly id: string
  readonly slug: string
  readonly name: string
  readonly role: OrganizationRole
}

// ---------------------------------------------------------------------------
// Registration and invitations.
// ---------------------------------------------------------------------------

export type RegistrationMode = 'disabled' | 'open' | 'approval'

export interface RegistrationSettings {
  readonly mode: RegistrationMode
  readonly allowedDomains: readonly string[]
  readonly requireEmailConfirmation: boolean
  readonly invitationLifetimeDays: number
  /** Whether the portal can send email (confirmations, invitations). */
  readonly emailDeliveryAvailable: boolean
  readonly updatedAtUtc: string | null
}

export interface UpdateRegistrationSettingsRequest {
  readonly mode: RegistrationMode
  readonly allowedDomains: readonly string[]
  readonly requireEmailConfirmation: boolean
  readonly invitationLifetimeDays: number
}

export interface RegisterRequest {
  readonly email: string
  readonly password: string
  readonly firstName: string
  readonly lastName: string
}

export type RegistrationOutcome = 'signed_in' | 'confirmation_sent' | 'approval_pending' | 'confirmed'

export interface RegistrationResult {
  readonly outcome: RegistrationOutcome
}

export type InvitationStatus = 'pending' | 'accepted' | 'revoked' | 'expired'

export interface Invitation {
  readonly id: string
  readonly email: string
  readonly firstName: string | null
  readonly lastName: string | null
  readonly message: string | null
  readonly status: InvitationStatus
  readonly invitedById: string
  readonly invitedByName: string | null
  readonly createdAtUtc: string
  readonly expiresAtUtc: string
  readonly lastSentAtUtc: string
  readonly acceptedAtUtc: string | null
  readonly acceptedUserId: string | null
  readonly groupIds: readonly string[]
  readonly organizationIds: readonly string[]
}

/** An invitation just issued, with its link (shown once: the portal keeps no copy of the token). */
export interface InvitationIssued {
  readonly invitation: Invitation
  readonly link: string
  readonly emailSent: boolean
}

export interface CreateInvitationRequest {
  readonly email: string
  readonly firstName: string | null
  readonly lastName: string | null
  readonly message: string | null
  readonly groupIds: readonly string[]
  readonly organizationIds: readonly string[]
}

export interface InvitationPreview {
  readonly email: string
  readonly firstName: string | null
  readonly lastName: string | null
  readonly invitedByName: string | null
  readonly message: string | null
  readonly expiresAtUtc: string
}

// ---------------------------------------------------------------------------
// Statistics.
// ---------------------------------------------------------------------------

export interface ActivityTotals {
  readonly signIns: number
  readonly failedSignIns: number
  readonly applicationSignIns: number
  readonly activeUsers: number
}

export interface DailyActivity extends ActivityTotals {
  /** `yyyy-mm-dd`, a day of the requested time zone. */
  readonly date: string
}

export interface ApplicationUsage {
  readonly clientId: string
  readonly name: string | null
  readonly signIns: number
  readonly users: number
}

export interface CountedValue {
  readonly value: string
  readonly count: number
  readonly lastAtUtc: string
}

export interface ActivityStatistics {
  readonly from: string
  readonly to: string
  readonly timeZone: string
  readonly current: ActivityTotals
  readonly previous: ActivityTotals
  readonly days: readonly DailyActivity[]
  readonly topApplications: readonly ApplicationUsage[]
  readonly failureReasons: readonly CountedValue[]
  readonly topFailedAccounts: readonly CountedValue[]
  readonly topFailedAddresses: readonly CountedValue[]
  /** False when the audit retention removed part of the period (or of the one before). */
  readonly isComplete: boolean
}

export interface UserStatistics {
  readonly total: number
  readonly administrators: number
  readonly locked: number
  readonly pendingApproval: number
  readonly twoFactorEnabled: number
  readonly newInPeriod: number
  readonly signedInInPeriod: number
  readonly neverSignedIn: number
  readonly pendingInvitations: number
}

export interface AccessStatistics {
  readonly applications: number
  readonly activeApplications: number
  readonly pendingApplications: number
  readonly disabledApplications: number
  readonly groups: number
  readonly organizations: number
  readonly userGrants: number
  readonly groupGrants: number
  readonly pendingAccessRequests: number
}

export interface PortalStatistics {
  readonly activity: ActivityStatistics
  readonly users: UserStatistics
  readonly access: AccessStatistics
}
