# Administrator's guide

This guide is for whoever runs the portal day to day: the first steps after installing it, how people,
applications and access fit together, and what to look at when something goes wrong. Installing and operating the
server (database, Docker, proxy, keys) is in [deployment.md](deployment.md); connecting an application's code is in
the [README](../README.md#connecting-another-application) and [apps-api.md](apps-api.md).

Menu names below are those of the English interface; press **Ctrl/Cmd+K** anywhere to jump to a page or find a
person, group or organization by name.

## 1. First steps

1. **Create the first administrator** with `BootstrapAdmin:*` in user secrets or the environment (see the README),
   sign in, then turn the bootstrap off.
2. **Secure your own account**: Account → Security → set up an authenticator app or a passkey.
3. **Email** (Security → Portal settings → Email): enter the SMTP server and send a test. Without email the portal
   works, but invitations must be passed on by hand and nobody can recover a password.
4. **Security policy** (Portal settings → Security): require a second factor (for administrators at least),
   decide whether passkeys and personal access tokens are allowed, and set an idle timeout if your users share
   computers.
5. **How people get an account** (Portal settings → Registration): keep registration *disabled* and invite people,
   or open it (optionally with approval and limited to your email domains).
6. **Google or Microsoft sign-in** (Portal settings → Sign-in providers), if your users have those accounts: see
   [section 6](#6-sign-in-with-google-and-microsoft).
7. **Privacy notice and terms** (Security → Privacy and terms): write and publish them before opening the portal to
   others; a version that requires acceptance is asked of everyone at their next visit.
8. Check **Security → System**: every check should be *Healthy*.

## 2. People

- **Users** (Identity & access → Users): search, filter by status (active, disabled, pending approval, inactive for
  90 days), edit details, reset a second factor or passkeys, see and end a person's **sessions**, unlink external
  accounts, change the sign-in **email** (for someone whose old mailbox is gone: both addresses are told and the
  account is signed out; people with a working mailbox change it themselves under Account → Security), start a **support session** (you act as the person for up to an hour, with a reason; everything is
  audited in your name), delete. Select several rows for bulk actions; export or import CSV from the toolbar
  (import always offers a dry run first).
- **Invitations**: an invitation is a single-use link that creates the account already in the chosen groups and
  organizations. Resending issues a new link and invalidates the old one.
- **Administrator flag**: only administrators see and set it, and nobody changes their own. Keep the number of
  administrators small and delegate pages instead (section 4).
- **Pending approval**: with registration *with approval*, new accounts wait in the Users list (filter *pending*);
  reviewers are notified.

## 3. Applications and access

An application is anything that signs in through the portal (OpenID Connect).

1. **Register it**: it announces itself on startup and appears in Applications as *waiting for approval*, or you add
   it by hand. Approving shows the client secret **once**; give it to whoever deploys the application.
2. **Decide who may open it**: grant it to groups (best) or to single users under Access. The access tree shows
   application → groups → members and application → direct users. Grants can expire.
3. **Roles**: on the application's *Roles and claims* tab list the roles the application understands, then choose
   the roles on each grant. A user's roles are the union over their grants.
4. **Options** worth knowing in the application dialog:
   - *Access*: let users **request access** (requests wait under Access requests) and choose how the application
     uses **organizations** (none, optional, required).
   - *Ask users for consent*: for third-party applications; users see what is shared and can withdraw it later.
   - *Allow the device flow*: for command-line tools and TVs.
   - *API*: what the application's backend may do on its own (read users, manage access, create users).
   - *Webhook*: where to send change events.
   - **Regenerate secret** can keep the previous one valid for hours or days, so the application can be redeployed
     without downtime.
5. **Withdrawing access** revokes the application's tokens at once; the user cannot sign in again, and the
   application loses them at the next token refresh (minutes).

**Access reviews** (Identity & access → Access reviews) take a snapshot of who can open an application; reviewers
mark each person *keep* or *revoke*, and completing the review applies the revocations. Run one periodically for
sensitive applications.

## 4. Delegating administration

Every administration page except the page permissions, the portal settings and the system page can be handed to a
group: Identity & access → Page permissions, choose the group, tick the pages. Members of that group use those
pages fully, without being administrators. Typical groups: *helpdesk* (users, invitations), *access managers*
(groups, access, access requests, access reviews), *auditors* (audit log, statistics), *communication*
(announcements), *legal* (privacy and terms).

Only an administrator can grant the administrator flag or change an administrator's account, whatever pages
someone holds.

## 5. Organizations (multi-tenant applications)

Use organizations when an application serves several customer companies and must know which one a user works for.

- Create the organization (its *slug* cannot change later: applications may store it) and add members with a role,
  *member* or *admin*. Organization admins manage their own members from their account (Account → Organizations),
  including invitations, without any portal page.
- **Domains**: add a domain, publish the TXT record shown on the Domains tab in that domain's DNS, then verify it.
  A verified domain can let people with a matching, confirmed address join by themselves (*auto-join*, with the
  role you choose).
- **Application roles within the organization**: on the member's row choose the roles they hold in each
  organization-aware application; they are added to the user's roles only when signing in for that organization.
- **Require a second factor** for the organization's members: they must verify one before any of its applications
  lets them in.
- Removing a member or deleting the organization ends their sessions in the organization-aware applications.

## 6. Sign-in with Google and Microsoft

Portal settings → Sign-in providers. For each provider:

1. Create the OAuth client at the provider with the redirect URI shown in [deployment.md](deployment.md#sign-in-with-google-and-microsoft)
   (`https://<portal>/signin-google` or `/signin-microsoft`).
2. Paste the client id and secret; for Microsoft choose the tenant (your directory id to admit only your
   organization).
3. **Allowed domains**: leave empty to accept any address, or list yours.
4. **New people**: *disabled* (only existing accounts, linked by their owner or by a verified address), *open*
   (account created at once), or *approval* (created and waiting for you).

Users link and unlink providers in Account → Security. An account created through a provider has no password; its
owner can set one there. Two-factor requirements still apply after an external sign-in.

## 7. Sessions and security incidents

- A person reports a lost laptop: Users → the person → **Sessions** → end the session on that device (or all).
  Ask them to change their password, which also ends every other session.
- A suspected account takeover: end all sessions, reset the second factor and passkeys, revoke personal access
  tokens, then check the account's history (Users → *history*, which opens the audit log filtered on the account).
- Each user sees their own sessions and authorized applications in Account → Security and is notified of a sign-in
  from a new device.
- **Audit log** (Security → Audit log): every sign-in (successful or not) and every change, with who, from which
  address and the outcome. Export to CSV for archiving beyond the retention period.

## 8. Communication

- **Announcements** (Communication → Announcements): banners for news, warnings and maintenance, per language and
  with a time window; those for *everyone* also appear on the sign-in page.
- **Notifications** reach people in the bell and, depending on each user's choice on the Notifications page, by
  email (security notices only by default, each one, a daily digest, or none).

## 9. Daily operations: the System page

Security → System (administrators only) shows the instance that answered:

| Section | What to look at |
| --- | --- |
| This instance | version, environment, database, uptime; which optional parts are on (email, Redis, telemetry, sign-in providers) |
| Checks | every database and dependency; *Degraded* usually means pending migrations or failed deliveries |
| Background jobs | last run, duration and outcome of each sweep; **Run now** runs one immediately (audited) |
| Delivery queue | emails, webhooks and logout messages waiting or failed; manage them in Portal settings → Email |
| Token signing keys | which key signs and until when; a successor appears two months before the end: restart the portal within the following week |

When a job fails, its error is shown next to it; the same error is in the server logs. Running it again from the
page is safe: every job is idempotent.

## 10. Checklist

**Weekly**: pending approvals and access requests (sidebar badges), failed deliveries, the System page.
**Monthly**: inactive accounts (Users → inactive for 90 days), administrators and page-permission groups, access
reviews for sensitive applications.
**Yearly or on change**: privacy notice and terms, application secrets (rotate with overlap), the signing key
rollover, a test restore of the database backup.
