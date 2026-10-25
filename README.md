<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/brand/openportal-logo-white.svg">
    <img src="docs/brand/openportal-logo.svg" alt="OpenPortal" width="420">
  </picture>
</p>

<p align="center">
  <strong>The application shell your next ASP.NET Core app signs in through.</strong><br>
  Users, sessions, a role-gated admin area and an OpenID Connect provider with per-app access — ready to build on.
</p>

<p align="center">
  <a href="LICENSE"><img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-black"></a>
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-black">
  <img alt="React 19" src="https://img.shields.io/badge/React-19-black">
  <img alt="OpenID Connect" src="https://img.shields.io/badge/OpenID%20Connect-provider-black">
  <a href="https://github.com/wp-daniel/OpenPortal/wiki"><img alt="Wiki" src="https://img.shields.io/badge/docs-wiki-black"></a>
</p>

<p align="center">
  <a href="#quick-start">Quick start</a> ·
  <a href="#features">Features</a> ·
  <a href="#connecting-another-application">Single sign-on</a> ·
  <a href="ARCHITECTURE.md">Architecture</a> ·
  <a href="https://github.com/wp-daniel/OpenPortal/wiki">Wiki</a>
</p>

---

OpenPortal is a reusable base for any ASP.NET Core application — a back office, an internal tool, a line-of-business app. It
gives you the parts every such application needs and nobody wants to write twice: accounts and sessions, an
administration area, and a single place where other applications send their users to sign in and where you
decide who may open what.

It is an ASP.NET Core 10 API with a React 19 client (Vite, Tailwind 4, shadcn/ui), ASP.NET Core Identity and
OpenIddict, on SQLite by default, organised as a modular monolith whose boundaries are enforced by tests.

## Features

**Identity**
- Sign-in with lockout, sessions in an `HttpOnly` cookie, antiforgery on every unsafe request (sign-in too)
- Sign-in with Google and Microsoft (Entra ID), configured by an administrator in the portal settings: client id and
  secret, allowed tenant and email domains, whether an unknown person gets an account; users link and unlink
  their own external accounts
- Sessions kept on the server: every user sees their devices and signs them out one by one, administrators see and
  end anyone's, an optional idle timeout signs out inactive sessions, and a sign-in from a new device is notified
- Two-factor authentication (authenticator app, recovery codes) and passkeys, with a policy that can require them
- Password recovery by email
- User administration with server-side search and filters: names, phone, job title, company, department, address
- Profile pictures, cropped and scaled in the browser, validated by content on the server
- A password policy defined once on the server and published to the client, so forms never disagree with it
- Last sign-in per account and an "inactive for 90 days" filter, to find dormant accounts
- Self-registration, off by default: open, or waiting for an administrator's approval, limited to some email
  domains, with address confirmation by email
- Invitations by email: a single-use link (emailed over SMTP, or shown to the administrator) that creates the
  account already in the chosen groups and organizations
- User list as CSV: export the filter (or a selection), import a file that invites new addresses (with names,
  groups and organizations) and optionally updates existing accounts, with a dry run first
- Bulk actions on the selected users: approve, add to or remove from a group, sign out everywhere, delete
- Support sessions: an administrator acts as an ordinary account for up to an hour, with a banner, every change
  audited as the administrator "on behalf of" the user, the user notified, and no access to the account's secrets
  or to the applications
- Personal access tokens for scripts and command-line tools (`Authorization: Bearer opp_…`): read or write
  scope, expiry, last use, revocation by the owner or an administrator; switched on in the portal settings

**Privacy (GDPR)**
- Privacy notice and terms of use written in the portal (Markdown, per language) and published as versions;
  a new version is accepted at registration, at the next visit, and before signing in to any application
- Every account holder downloads a ZIP of everything the portal holds about them (password confirmed)
- Account deletion on request, after a grace period the owner can cancel by signing in again; then a background
  job erases the account and what every module holds about it

**Single sign-on**
- OpenID Connect provider (authorization code + PKCE + refresh tokens; device authorization for TVs and command-line
  tools; client credentials for an application's own API; revocation, introspection, back-channel logout)
- Optional consent screen per application, and an *Authorized applications* list where each user withdraws an
  application's access to their account
- Client secret rotation with an overlap during which the previous secret still works, and token signing keys that
  roll over by themselves (a successor is published before it signs)
- Per-application access for users and for groups; withdrawing access revokes tokens and is re-checked on every refresh
- Applications announce themselves and wait for approval; a NuGet package (`OpenPortal.Client`) does the wiring
- A launchpad on the dashboard with the applications each user may open
- Application roles: each application declares the roles it checks, administrators assign them on grants (to
  users or groups), and they reach the application as `role` claims (`User.IsInRole("sales")` just works); an
  application may also receive the user's groups as `groups` claims, if its setting allows
- Access requests: an application can let users who cannot open it ask for it (from their dashboard or the
  access-denied page); reviewers approve with roles or decline with a note, and both sides are notified
- Organizations (multi-tenancy for the applications): users belong to organizations with a role; an application
  that works per organization gets `org_id`, `org_slug`, `org_name` and `org_role` in its tokens, members of several
  choose one at sign-in, and leaving an organization ends the session at the next refresh
- Organizations go further: verified email domains (a DNS TXT record) that let colleagues join by themselves,
  application roles a member holds only within that organization, a two-factor requirement per organization, and
  organization admins who invite and manage their own members
- Webhooks (Standard Webhooks signatures) and an outbox that retries deliveries

**Notifications and overview**
- Announcements: banners for news, warnings or planned maintenance, per language, with a time window, shown in
  the portal and (if meant for everyone) on the sign-in page
- In-app notification center: a bell with the unread count, a page with every notification, and notices for
  access requests and decisions, new grants, group and organization memberships, registrations to approve,
  accepted invitations and account security changes
- The same notifications by email, as each user prefers: security notices only (default), each one, a daily digest,
  or none
- Statistics for administrators (a delegable page): sign-ins per day, failed attempts by reason, account and
  address, most used applications, active users against the previous period, and what waits for someone

**Security and operations**
- Audit log of sign-ins and of account, session, group, organization, access and application changes: who, what, on what, from
  which address, with what outcome; filters, per-user history, CSV export, retention
- Rate limits on sign-in, password changes, the token endpoint and the whole API (429 with `Retry-After`)
- Content-Security-Policy, anti-framing and the other browser security headers on every response
- `/health/live` and `/health/ready` for load balancers and orchestrators
- A **System** page for administrators: version, readiness checks, the background jobs with their last run (and a
  button to run one now), the delivery queue and the token signing keys with their expiry
- Lists that page and search on the server (users, groups, organizations), so they stay fast with many rows
- SQLite or PostgreSQL; a Docker image, a compose file with PostgreSQL, and CI that tests on both databases

**Experience**
- English, Italian, Spanish and French, with the language saved per user; one `.resx` per language translates
  both server and client
- Light and dark themes, collapsible sidebar, toasts for every message, accessible shadcn/ui components
- A command palette (Ctrl/Cmd+K) that opens any page you may use and finds people, groups and organizations
- Every error is `application/problem+json` with a stable `errorCode`

**Engineering**
- Modular monolith (Identity, Access, Audit, Notifications) with dependency rules checked by architecture tests
- Operations return `Result` instead of throwing; bad input is a 400, never a 500
- Integration tests over the real host on a temporary SQLite file, or a throwaway PostgreSQL database

## Quick start

You need the **.NET SDK 10.0.400** (pinned in `global.json`) and **Node.js 22** or newer.

```bash
dotnet tool restore
dotnet run --project src/OpenPortal.Web
```

That single command builds the client, builds the host, applies migrations and starts listening on
`https://localhost:7137`. In development the Vite dev server is proxied through the ASP.NET origin, so the
browser only ever talks to one host and the session cookie stays first-party.

Every page except `/sign-in` requires an account, so create the first administrator:

```bash
dotnet user-secrets --project src/OpenPortal.Web set "BootstrapAdmin:Enabled" "true"
dotnet user-secrets --project src/OpenPortal.Web set "BootstrapAdmin:Email" "you@example.com"
dotnet user-secrets --project src/OpenPortal.Web set "BootstrapAdmin:Password" "<a strong password>"
```

Restart and sign in. Users, groups, applications and access are under **Administration** in the sidebar.
Turn the bootstrap back off once the account exists; it only creates a missing account, so it is harmless,
but it should not stay enabled in a deployment.

Credentials belong in user secrets, never in a committed `appsettings*.json`.

### Useful variations

```bash
# Backend only, without building the client.
dotnet run --project src/OpenPortal.Web -p:SkipClientBuild=true

# Work on the client alone with fast refresh.
cd src/OpenPortal.Web/ClientApp && npm run dev

# Type-check and build the client; lint it (also checks that every language has every string).
cd src/OpenPortal.Web/ClientApp && npm run build
cd src/OpenPortal.Web/ClientApp && npm run lint
```

## Connecting another application

The portal is an OpenID Connect provider. Another ASP.NET Core application signs its users in through it, and
the portal decides who may:

1. Reference `src/OpenPortal.Client` and call `builder.Services.AddOpenPortalAuthentication(builder.Configuration)`
   plus `app.MapOpenPortalSignOut()`. Configure the `OpenPortal` section (`Authority`, `ClientId`, `BaseUrl`,
   and in user secrets `ProvisioningKey`).
2. On start-up the application announces itself and appears under **Administration → Applications** as
   *waiting for approval*. (Or register it by hand there.)
3. Approve it: the portal shows the client secret once. Put it in the application's `OpenPortal:ClientSecret`.
4. Under **Groups** put users in groups and switch the application on for the group, or give it to single
   users under **Access**, which shows the whole tree: application → groups → members, application → direct
   users.
5. Roles: list them in the application's `OpenPortal:Roles` (or on the application's **Roles and claims** tab)
   and assign them on each grant under **Access**. The application receives the user's roles as `role` claims
   and, if its setting allows, their groups as `groups` claims; both are recomputed at every token refresh.
6. Organizations (for an application that serves several tenants): on the application's **Access**
   tab choose *optional* or *required*, and put users in organizations under **Organizations**. The tokens then
   carry `org_id`, `org_slug`, `org_name` and `org_role` (`User.GetOpenPortalOrganization()` in `OpenPortal.Client`);
   a member of several organizations picks one at sign-in (or the application sends `organization=<slug>`), and
   leaving the organization ends the session at the next refresh. Key the application's data by `org_id`.
7. Optionally let users ask for the application (**Access** tab, *users can request access*): it appears on their
   dashboard and on the access-denied page, and requests wait under **Access requests**.

A user without access is stopped at the portal. Withdrawing access revokes the application's tokens, and the
token endpoint re-checks access on every refresh, so the user cannot sign in again and the tokens stop working
within the access token lifetime (10 minutes by default). The application's own session is a local cookie that
`OpenPortal.Client` does not refresh by itself: a user already inside keeps it until it expires or they sign out.
[`samples/OpenPortal.SampleApp`](samples/OpenPortal.SampleApp) shows the whole thing; the
[wiki](https://github.com/wp-daniel/OpenPortal/wiki/Connecting-an-Application) walks through connecting any app.

The application's backend can also call the portal on its own (client credentials, no user): list who can open
it, grant or withdraw access with roles, add colleagues. An administrator enables that per application on its
**API** tab; [`docs/apps-api.md`](docs/apps-api.md) explains it, and the portal's **`/scalar`** page is the
interactive reference for everything an application uses.

## Tests

```bash
dotnet test                                  # everything
dotnet test tests/OpenPortal.Tests.Unit
dotnet test tests/OpenPortal.Tests.Architecture
dotnet test tests/OpenPortal.Tests.Integration
```

Integration tests boot the real host against a temporary SQLite file. Nothing binds a fixed port and no
process is left behind. To run them on PostgreSQL instead, point `OPENPORTAL_TEST_POSTGRES` at a server; each
test class gets its own database, dropped afterwards:

```bash
OPENPORTAL_TEST_POSTGRES="Host=localhost;Username=postgres;Password=postgres" dotnet test tests/OpenPortal.Tests.Integration
```

CI ([`.github/workflows/ci.yml`](.github/workflows/ci.yml)) runs the client checks, every suite on SQLite, the
integration suite on PostgreSQL, and a check that the migrations of both providers match the model. The Docker
image is not built by CI for now; [`docker-publish.yml`](.github/workflows/docker-publish.yml) builds and publishes
it when run by hand.

## Publishing

With Docker and PostgreSQL:

```bash
cp .env.example .env        # set the passwords and keys
docker compose up -d --build
```

The compose file runs PostgreSQL, a one-shot `migrate` step (the image started with `--migrate`) and the portal
on port 8080, which expects a TLS-terminating reverse proxy in front. The image alone runs one instance on SQLite
(`docker run -p 8080:8080 -v openportal:/data openportal`). Without Docker:

```bash
dotnet publish src/OpenPortal.Web -c Release -o publish
```

The client is built as part of this, so `publish/wwwroot` contains the SPA. In production:

- keep `Database:MigrateOnStartup` at `false` and apply migrations as a separate ordered step
  (`dotnet OpenPortal.Web.dll --migrate`), because several instances migrating at once is a race;
- keep `Identity:Cookie:RequireSecure` at `true`, terminate TLS in front of the app and set
  `ReverseProxy:Enabled` so the client's address and scheme are the real ones;
- provide the token signing and encryption certificates (`Oidc:*`, or `Oidc:CertificatesPath` for a folder the
  portal fills itself) and a shared `DataProtection:KeysPath`.

[`docs/deployment.md`](docs/deployment.md) covers PostgreSQL, Docker, the reverse proxy, health checks and
adding migrations.

## Layout

| Path | Contents |
| --- | --- |
| `src/OpenPortal.SharedKernel` | `Result`, `Error`, `IClock`, `TextRules`, `IAuditTrail`, `INotifier`, `IOutbox`, `IJobRegistry` |
| `src/Modules/Identity` | accounts, sessions, sign-in factors, external providers, registration, legal documents — Domain / Application / Infrastructure |
| `src/Modules/Access` | applications, groups, organizations and access grants, plus the OpenID Connect client store |
| `src/Modules/Audit` | the audit log: storage, queries and retention of the events every module records |
| `src/Modules/Notifications` | notifications (in-app and email), announcements, the delivery outbox, SMTP settings |
| `src/OpenPortal.Migrations.PostgreSql` | the PostgreSQL migrations (SQLite's are in `OpenPortal.Web`) |
| `src/OpenPortal.Client` | NuGet package an application references to sign in through the portal |
| `samples/OpenPortal.SampleApp` | a minimal application that uses `OpenPortal.Client` |
| `src/OpenPortal.Web` | composition root, controllers, OIDC endpoints, middleware, migrations, the client |
| `tests` | unit, architecture and integration suites |
| `docs/brand` | logo, symbol and app icon |

[`ARCHITECTURE.md`](ARCHITECTURE.md) explains why it is arranged this way, what each boundary buys, and which
test enforces it.

## How it is put together

Three things are worth knowing before changing anything.

**Operations return `Result`, they do not throw.** A failure carries a stable error code that reaches the
client as the `errorCode` extension of a problem+json response. Domain entities validate before constructing
rather than throwing, because a rejected input must be a 400 and an `ArgumentException` escaping a service
becomes a 500.

**Every unsafe request needs an antiforgery token, including sign-in.** The session cookie is `HttpOnly`;
the separate `XSRF-TOKEN` cookie is deliberately readable by script and is echoed in the `X-XSRF-TOKEN`
header. The token is bound to the signed-in identity, so the client discards it after login and logout.

**Access is checked on every token request, not just at sign-in.** The rule (the application is active and
the user holds a direct or group grant) lives in one place and is applied by the authorize endpoint and by
every code exchange and refresh, so taking access away takes effect without waiting for a session to expire.

## Configuration

| Section | Purpose |
| --- | --- |
| `ConnectionStrings:OpenPortal` | database connection string |
| `Database:Provider` | `Sqlite` (default) or `PostgreSql` |
| `Database:MigrateOnStartup` | apply migrations during startup; development and single-instance SQLite only |
| `Identity:Password` | complexity policy, published to the client so forms can match it |
| `Identity:Lockout` | failed-attempt thresholds |
| `Identity:SignIn` | cookie name, lifetime, whether confirmation is required |
| `Identity:Cookie:RequireSecure` | `false` only for the plain-http localhost dev server |
| `Localization` | default language and the languages offered (add one with a line here and a `Messages.<lang>.resx`) |
| `BootstrapAdmin` | one-time first administrator |
| `Access:ProvisioningKey` | shared key applications present when they announce themselves (user secrets); blank disables announcements |
| `Oidc` | token signing/encryption certificates (`SigningCertificatePath`, `EncryptionCertificatePath` + passwords, or `CertificatesPath` for self-managed ones), token lifetimes; required outside Development |
| `Audit:RetentionDays` | how long audit entries are kept (default 365; 0 keeps everything) |
| `Notifications` | `ReadRetentionDays` (default 30) and `RetentionDays` (default 180): how long notifications are kept; `MaxRecipientsPerNotice`; `DigestIntervalHours` (default 24) for the email digest |
| `Organizations:DnsResolver` | DNS-over-HTTPS endpoint used to verify organization domains (default `https://dns.google/resolve`) |
| `Email` | account email: `From`, `FromName`, `PublicUrl` (the portal's address in links; set it in production), `Smtp:Host`/`Port`/`Security`/`Username`/`Password` (password in user secrets), or `PickupDirectory` to write `.eml` files instead; off until configured |
| `RateLimiting` | `Enabled`, and `PermitLimit`/`WindowSeconds` for `General` (per user or address), `SignIn` and `Machine` (per address) |
| `ReverseProxy` | `Enabled`, `KnownProxies`, `KnownNetworks`, `ForwardLimit`: trust a proxy's `X-Forwarded-*` headers |
| `SecurityHeaders:ContentSecurityPolicy` | replaces the default policy (this origin only) |
| `DataProtection:KeysPath` | where the key ring lives (default `App_Data/keys`); share it between instances |
| `Geo` | address suggestions: `Enabled`, `PostalCodeBaseUrl` ([Zippopotam.us](https://zippopotam.us)), `PlacesBaseUrl` ([Photon](https://photon.komoot.io)) |

Address suggestions use two free public services by default, called from the server and credited in the UI
(OpenStreetMap and GeoNames data). They are meant for fair use with no guaranteed availability: for a busy
installation point `Geo:PlacesBaseUrl` at your own [Photon](https://github.com/komoot/photon) instance, or
turn the lookups off with `Geo:Enabled = false`. Country flags are bundled with the client, so browsers never
contact a third party. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md#address-data-and-online-services).

## Documentation

- [Wiki](https://github.com/wp-daniel/OpenPortal/wiki) — getting started, single sign-on, localization,
  architecture, testing, publishing, contributing
- [`ARCHITECTURE.md`](ARCHITECTURE.md) — the long form, with the reasoning behind each decision
- [`docs/admin-guide.md`](docs/admin-guide.md) — the administrator's guide: first steps, people, access, organizations, settings, daily operations
- [`docs/apps-api.md`](docs/apps-api.md) — the application API (client credentials) and the `/scalar` reference
- [`docs/deployment.md`](docs/deployment.md) — Docker, PostgreSQL, reverse proxy, keys, health checks
- [`docs/personal-access-tokens.md`](docs/personal-access-tokens.md) — calling the portal's own API from scripts
- [`docs/brand`](docs/brand) — the logo and how it is used

## License

[MIT](LICENSE). Use it, change it, ship it. Third-party licenses are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
