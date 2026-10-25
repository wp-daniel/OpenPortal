# Deploying OpenPortal

This guide covers running the portal for real: the database, the container, the reverse proxy in front of it,
the keys it keeps, and how to tell from outside that it is healthy.

## Choosing the database

| | SQLite (default) | PostgreSQL |
| --- | --- | --- |
| Instances | one | several |
| Setup | none: a file in `/data` | a server, a database and a user |
| Migrations | at startup (`Database:MigrateOnStartup=true`) is fine | a separate step before the new version starts |
| Backups | copy the file while the portal is stopped, or `sqlite3 .backup` | `pg_dump`, or the provider's managed backups |

Switch with two settings:

```text
Database__Provider=PostgreSql
ConnectionStrings__OpenPortal=Host=db;Database=openportal;Username=openportal;Password=…
```

Each provider has its own migrations, because a migration is written for one engine's column types: SQLite's
are in `src/OpenPortal.Web/Persistence/Migrations`, PostgreSQL's in `src/OpenPortal.Migrations.PostgreSql`.
Both are generated from the same model, and CI fails when either set lags behind it.

There is no data migration from SQLite to PostgreSQL built in. Start a new installation on PostgreSQL, or move
the rows with a tool such as `pgloader` once both schemas exist.

## Applying migrations

Production keeps `Database:MigrateOnStartup` off, because several instances migrating at once is a race. Run
the published host once with `--migrate`: it brings every module's schema up to date, provisions the roles and
the bootstrap administrator (when `BootstrapAdmin:Enabled`), and exits.

```bash
dotnet OpenPortal.Web.dll --migrate                  # published build
docker compose run --rm migrate                      # with the compose file
```

`docker-compose.yml` does this by itself: the `migrate` service runs first and the portal starts only once it
has succeeded.

## Docker

```bash
cp .env.example .env          # set POSTGRES_PASSWORD, PROVISIONING_KEY and the bootstrap administrator
docker compose up -d --build
```

The image (`Dockerfile`) builds the client and the host and runs as the unprivileged `app` user on port 8080.
Everything it must keep lives in `/data`; mount a volume there:

| Path | What | If it is lost |
| --- | --- | --- |
| `/data/keys` | Data Protection key ring: session cookies, antiforgery tokens | everybody is signed out of the portal |
| `/data/oidc` | token signing and encryption certificates, created on first start (`Oidc:CertificatesPath`) | every application's tokens stop working; users sign in again |
| `/data/openportal.db` | the SQLite database (SQLite only) | everything |

**Signing key rollover.** With `Oidc:CertificatesPath` the portal renews its own signing certificate: two months
before one expires it writes a successor that becomes valid a week later and is published in the JWKS at once,
so applications already trust it when it starts signing. Keys are read at startup: restart the instances (a
rolling restart is enough) some time in that week. The **System** page lists the keys with their validity and
which one signs. Certificates configured by path (below) are never renewed by the portal.

Protect `/data` like a password store: whoever reads `/data/oidc` can mint tokens for every application. To
use certificates of your own instead, set `Oidc:SigningCertificatePath` and `Oidc:EncryptionCertificatePath`
(PKCS#12, with their passwords); they take precedence. Several instances must share `/data/keys` and use the
same certificates.

## Behind a reverse proxy

Terminate TLS in a proxy (Caddy, Traefik, nginx, a cloud load balancer) and forward to port 8080. Then:

- `ReverseProxy:Enabled=true`, so the portal reads the client's address and the `https` scheme from
  `X-Forwarded-For` / `X-Forwarded-Proto`. The address appears in the audit log and keys the rate limits; the
  scheme makes the OpenID Connect issuer and redirects `https`. List the proxy in `ReverseProxy:KnownProxies`
  (or its network in `KnownNetworks`) unless only the proxy can reach the container: a trusted header from
  anyone else would let them pick their own address.
- keep `Identity:Cookie:RequireSecure=true`.

A minimal Caddyfile:

```text
portal.example.com {
    reverse_proxy localhost:8080
}
```

## Health checks

| Endpoint | Meaning | Use it for |
| --- | --- | --- |
| `GET /health/live` | the process answers | liveness: restart when it fails |
| `GET /health/ready` | every module's database answers (503 otherwise); `Degraded` when migrations are pending | readiness: route traffic only when it passes |

Both are anonymous, exempt from rate limiting, and report only check names and states.

## Rate limits

Defaults per minute: 10 password checks (sign-in, change of password, account deletion) per address, 120 token and
announcement requests per address, 600 API requests per signed-in user or anonymous address; and 5 personal data
downloads per account per hour (`RateLimiting:Export`). Calls with a personal access token count as their owner. Over a limit the answer is 429
with `Retry-After`. Counters live in each instance's memory, or in Redis when it is configured. Tune them under `RateLimiting`; set
`RateLimiting:Enabled=false` only when a gateway in front already limits.

## Audit log

Every module records its changes and every sign-in, successful or not, to the audit log (**Security → Audit
log**, or `GET /api/admin/audit`). Entries older than `Audit:RetentionDays` (365) are deleted once a day; `0`
keeps everything. Reading the log is the `audit` page: grant it to a group of auditors without making them
administrators. The CSV export (`/api/admin/audit/export`, at most 10,000 rows per download) is meant for
archiving beyond the retention period.

The statistics page (**Security → Statistics**, also shown on administrators' dashboards) is computed from the
same log, so it covers what the retention keeps: a period older than `Audit:RetentionDays` is reported as
partial.

## Email

Invitations, address confirmation for self-registration and "your account was approved" messages are sent by
email once delivery is configured; until then the portal works without it (invitation links are shown to the
administrator to pass on, and self-registration cannot require a confirmed address).

```json
"Email": {
  "PublicUrl": "https://portal.example.com",
  "From": "portal@example.com",
  "FromName": "OpenPortal",
  "Smtp": { "Host": "smtp.example.com", "Port": 587, "Security": "Auto", "Username": "portal@example.com" }
}
```

The SMTP server, login, password and sender can instead be set from the portal (**Security → Portal settings →
Email**, administrators only): those settings win over the configuration, the password is stored encrypted with the
Data Protection keys, and "Send a test" checks them before saving. Keep `Email:Smtp:Password` in user secrets or the environment (`Email__Smtp__Password`). Set `PublicUrl`: links in
emails otherwise use the address of the request that caused them, which behind a proxy (or with a forged `Host`
header) is not the portal's. For development, `Email:PickupDirectory` writes each message as an `.eml` file
instead of sending it. Registration itself (off, open or with approval, allowed domains, confirmation, invitation
lifetime) is set by administrators under **Security → Portal settings**.

### Delivery queue

Emails, webhooks and back-channel logout messages are written to a durable queue (the Notifications schema's
`OutboxMessages`) and delivered by a background worker in every instance; several instances share the queue
safely (each message is leased). A failed delivery is retried with growing delays up to `Outbox:MaxAttempts`
(10), then kept as failed. **Security → Portal settings → Email** lists the queue, retries or deletes messages, and
`/health/ready` reports the `delivery-queue` check as degraded while failed messages wait.

```json
"Outbox": { "PollSeconds": 10, "BatchSize": 20, "MaxAttempts": 10, "LeaseSeconds": 120, "DeliveredRetentionDays": 7, "FailedRetentionDays": 30 }
```

Audit entries and in-app notifications are still written right after the change they describe, not through the
queue: an instance that crashes in between can lose one.

## Sign-in security

- **Password recovery** works once email is configured (`/forgot-password`; links last one hour, and other
  sessions end within `Identity:SignIn:SessionValidationSeconds`, 60 by default).
- **Passkeys** (WebAuthn) are on by default (**Security → Portal settings**). They are bound to the host name:
  set `Identity:Passkeys:ServerDomain` (e.g. `portal.example.com`) before users register any, since changing it
  later invalidates them, and list `Identity:Passkeys:AllowedOrigins` if the portal is reached under several
  names.
- **Second factor requirement**: none, administrators or everyone. Accounts concerned are sent to set up an
  authenticator app or a passkey before anything else, and applications receive them only afterwards.

## Sessions

Portal sessions are stored in the database (Identity's `UserSessions`); the cookie only carries the session's key.
Ending a session, from the user's account or by an administrator, takes effect at the next request on every
instance. **The first deployment of this version signs everybody out once**, because older cookies carried the
whole ticket. An idle timeout (5 minutes to 24 hours) can be set under **Security → Portal settings**; ended
sessions are kept 30 days as sign-in history.

## Sign-in with Google and Microsoft

Administrators configure both under **Security → Portal settings → Sign-in providers**; nothing goes in
appsettings, and the client secret is stored encrypted with the Data Protection keys (keep `/data/keys`).
Register the portal with the provider using these redirect URIs:

| Provider | Redirect URI |
| --- | --- |
| Google (Google Cloud console, OAuth client of type *Web application*) | `https://portal.example.com/signin-google` |
| Microsoft (Entra ID app registration, platform *Web*) | `https://portal.example.com/signin-microsoft` |

The host name must be the one browsers use, so behind a proxy set `ReverseProxy:Enabled`. For Microsoft choose the
tenant: a directory id (one organization), `organizations` (any work account), `common` or `consumers`. An
existing account is linked only when the provider vouches for the address (Google's `email_verified`; for Entra
ID a personal account, the `xms_edov` claim, or a single tenant you named, since a directory's administrators can
put any address on their accounts), and the allowed domains limit who may use the provider at all.

## Organization domains

An organization can claim an email domain by publishing a TXT record `openportal-verification=<token>` (the token
is on the organization's **Domains** tab). The portal looks it up over DNS-over-HTTPS, by default
`https://dns.google/resolve`; point `Organizations:DnsResolver` at another resolver with the same JSON API (for
example `https://cloudflare-dns.com/dns-query`), or leave verification to an administrator if outbound calls are
not allowed.

## Background jobs

Every instance runs the same sweeps (access expiry each minute, account deletion and session retention each hour,
the notification digest every 15 minutes, audit and notification retention once a day); each is safe to run on several
instances at once. **Security → System** shows, for the instance that answered, each job's last run,
duration and outcome, and runs one now on request (audited). It also shows the readiness checks, the delivery
queue, which optional parts are configured and the signing keys.

## Several instances (Redis)

Without Redis each instance keeps its own rate-limit counters, cache and notification push. With
`Redis:ConnectionString` set, the instances share a distributed cache, rate-limit partitions and the
notification stream (pub/sub), and `/health/ready` checks Redis too. `Redis:DataProtectionKeys=true` also keeps
the Data Protection keys there (otherwise share `DataProtection:KeysPath` between instances). Sticky sessions are
not needed.

## Observability

Traces, metrics and logs are exported over OTLP when `Observability:OtlpEndpoint` is set (e.g.
`http://otel-collector:4317`; `Protocol` `grpc` or `http`, `ServiceName`, `ExportLogs`). They cover
ASP.NET Core, HttpClient, EF Core/Npgsql and the runtime, plus the portal's own meters: `OpenPortal.Identity`
(sign-ins by method and outcome), `OpenPortal.Oidc` (tokens and authorizations) and `OpenPortal.Outbox`
(deliveries, failures, queue age). The standard `OTEL_*` environment variables work as well.

## Privacy

- **Legal documents.** The privacy notice and the terms of use are written and published from **Security →
  Privacy and terms** (the `legal` page, delegable). Publishing a version that requires acceptance asks every
  account to accept it at its next visit and before any application sign-in (`/connect/authorize` redirects to
  `/legal/accept`, or answers `consent_required` with `prompt=none`). Published versions never change.
- **Account deletion.** Self-service deletion is on by default with a 30-day grace period (**Security → Portal
  settings → Privacy**). A request ends the account's sessions and tokens at once; a background job in every
  instance deletes the accounts whose grace period ended, once an hour (several instances are safe). The audit log
  keeps its entries about the account until `Audit:RetentionDays`.
- **Data export.** `POST /api/account/data-export` builds a ZIP from every module (`IPersonalDataSource`);
  nothing secret (password hash, second-factor keys, token hashes) is included.

## Support sessions and tokens

- **Impersonation** is for administrators only and ends by itself after `Identity:ImpersonationMinutes` (60).
- **Personal access tokens** are off until an administrator turns them on (**Security → Portal settings**);
  turning them off stops every token at once. See [personal-access-tokens.md](personal-access-tokens.md).

## Notifications

Notifications are stored per recipient and shown in the bell and on `/notifications`; the client polls the unread
count through a server-sent event stream (`/api/notifications/stream`), falling back to polling every 30 seconds. Read ones are deleted after `Notifications:ReadRetentionDays`
(30), all after `Notifications:RetentionDays` (180).

Once email is configured, notifications can also go by email: each user picks security notices only (the
default), every notification, a digest every `Notifications:DigestIntervalHours` (24), or nothing, on the
`/notifications` page. Emails go through the delivery queue in the user's language.

## Adding a migration

After changing a module's model, add the migration for **both** providers:

```bash
# SQLite (contexts: IdentityDbContext, AccessDbContext, AuditDbContext, NotificationsDbContext)
dotnet ef migrations add <Name> --project src/OpenPortal.Web --context AccessDbContext \
  --output-dir Persistence/Migrations/Access

# PostgreSQL (no server is needed to generate one)
dotnet ef migrations add <Name> --project src/OpenPortal.Migrations.PostgreSql \
  --startup-project src/OpenPortal.Web --context AccessDbContext --output-dir Access \
  -- --Database:Provider=PostgreSql "--ConnectionStrings:OpenPortal=Host=localhost;Database=design;Username=design;Password=design"
```

The contexts are `IdentityDbContext`, `AccessDbContext`, `AuditDbContext` and `NotificationsDbContext`. Check the
generated defaults when you add a required column to a table that already has rows (an enum stored as text,
for example, needs a real member name as its default, not an empty string). Check that nothing is left behind:

```bash
dotnet ef migrations has-pending-model-changes --project src/OpenPortal.Web --context AccessDbContext
```
