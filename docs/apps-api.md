# The application API

An application that signs its users in through OpenPortal can also talk to the portal **from its own backend**,
without a user and without the portal's screens: list the people who can open it, grant or withdraw their
access (with the application's roles), and add new colleagues.

The reference, with every endpoint, schema and a "try it" client, is at **`/scalar`** on the portal (the
document behind it is `/openapi/apps.json`). It shows only what applications use: OpenID Connect sign-in,
the application API and the announcement endpoint. The portal's own administration API is not in it.

## 1. Enable it for the application

Nothing is allowed by default. In **Administration → Applications**, edit the application and open the **API**
tab:

| Scope | Allows |
| --- | --- |
| `portal.users.read` | `GET /api/apps/me/users`, `GET /api/apps/me/users/{id}` |
| `portal.access.manage` | `PUT` / `DELETE /api/apps/me/users/{id}/access`, `POST /api/apps/me/users` for an existing account |
| `portal.users.create` | `POST /api/apps/me/users` for a new account (ordinary, never an administrator). Needs `portal.access.manage`. |

Changing the scopes or disabling the application takes effect on the next request, even for tokens already
issued: every call re-reads the application.

## 2. Get a token

The application's client id and secret (shown once at approval, or regenerated in the portal):

```bash
curl -s https://portal.example.com/connect/token \
  -d grant_type=client_credentials \
  -d client_id=orders \
  -d client_secret="$OPENPORTAL_CLIENT_SECRET"
```

Without `scope` the token carries every enabled scope; ask for fewer with `-d "scope=portal.users.read"`. The
answer is an `access_token` valid for a few minutes (`expires_in`); ask for a new one when it expires. There is
no refresh token and no user in it.

## 3. Call the API

```bash
curl -s https://portal.example.com/api/apps/me/users?search=rossi \
  -H "Authorization: Bearer $TOKEN"
```

| Method | Path | What it does |
| --- | --- | --- |
| `GET` | `/api/apps/me` | The application: roles, groups claim setting, enabled scopes. |
| `GET` | `/api/apps/me/users?search=&page=&pageSize=` | Users who can open the application (directly or through a group), with their roles and groups. |
| `GET` | `/api/apps/me/users/{id}` | One of them; 404 for anyone without access. |
| `POST` | `/api/apps/me/users` | Add a person by `email`: an existing account is granted the application; a new one also needs `password`, `firstName`, `lastName`. Optional `roles`. 201 when an account was created. |
| `PUT` | `/api/apps/me/users/{id}/access` | Grant the application to the user personally. `{ "roles": ["sales"] }` sets exactly those roles; no body keeps the existing ones. |
| `DELETE` | `/api/apps/me/users/{id}/access` | Withdraw the personal grant and end the user's sessions in the application. `stillHasAccess` is true when a group still grants it (groups are managed in the portal). |

Errors are `application/problem+json` with an `errorCode`, like the rest of the portal: `access.api_scope_missing`
(403, the token lacks the scope or it was switched off), `access.api_caller_unknown` (403, not an active
application's own token), `access.role_not_found` (400), `identity.duplicate_email` (409), password policy
errors (400).

Every change is in the portal's audit log with the application as the actor. A user's own access token, or a
portal session, never opens these endpoints.

## 4. Single sign-out, your own API and public clients

- **Back-channel logout.** Give the application a *back-channel logout URI* (application dialog; the
  `OpenPortal.Client` package announces `/signout-backchannel` and maps it with
  `app.MapOpenPortalBackChannelLogout()`). When a user signs out of the portal, an administrator signs them out
  or their access ends, the portal posts a signed `logout_token` (OpenID Connect Back-Channel Logout 1.0, `typ`
  `logout+jwt`) to it; delivery is queued and retried. The package then rejects that user's cookie on the next
  request and, with `RevokeTokensOnSignOut`, revokes its refresh token when the user signs out locally.
- **Sign-out from the application** goes to `/connect/endsession` with `id_token_hint`; without the hint the
  portal asks the user to confirm, so a link elsewhere cannot sign anyone out.
- **Revocation and introspection**: `/connect/revoke` (any client) and `/connect/introspect` (confidential
  clients). Access tokens are signed JWTs (not encrypted): an application's own API validates them with
  `services.AddAuthentication().AddOpenPortalApiAuthentication(configuration)` against the keys published in
  `/.well-known/openid-configuration`.
- **Step-up.** Tokens carry `amr` (`pwd`, `mfa`, `hwk`, `user`). Ask for `acr_values=mfa` to require a second
  factor: a user signed in with a password alone is sent to verify one, or the request fails with
  `unmet_authentication_requirements` under `prompt=none`.
- **Public clients** (single-page or mobile apps) are chosen when the application is created: PKCE, no secret, no
  application API.

## 5. Webhooks

Instead of polling, an application can be told when something that concerns it changes. In the dialog's
**Webhook** tab set the URL (HTTPS), pick the events and generate the signing secret (`whsec_…`, shown once).

| Event | When |
| --- | --- |
| `user.updated`, `user.deleted` | a user who can open the application changed or was deleted |
| `access.granted`, `access.revoked`, `access.roles_changed` | access to this application, directly or through a group |
| `group.member_added`, `group.member_removed` | a member of a group granted the application |
| `organization.member_added`, `organization.member_removed`, `organization.member_role_changed` | for applications that use organizations |
| `webhook.test` | the "Send a test" button |

Each delivery is a JSON `POST` signed with [Standard Webhooks](https://www.standardwebhooks.com/): headers
`webhook-id`, `webhook-timestamp`, `webhook-signature` (`v1,<base64 HMAC-SHA256 of "id.timestamp.body">`). Answer
2xx quickly; anything else is retried with growing delays (from 30 seconds up to 3 hours, 10 attempts by default), and the same `webhook-id` may arrive
twice, so handle it idempotently. With `OpenPortal.Client`:

```csharp
app.MapOpenPortalWebhooks("/hooks/openportal", builder.Configuration["OpenPortal:WebhookSecret"]!,
    async (evt, http) => { /* evt.Type, evt.Data */ });
```

`OpenPortalWebhooks.Verify(secret, id, timestamp, signature, body)` does the check alone for other frameworks.
