# Personal access tokens

A personal access token lets a script or a command-line tool call the portal's own API (`/api/…`) as the person who
created it, without a browser session. Applications that sign their users in through the portal use OpenID Connect
instead, and an application's backend uses the application API ([apps-api.md](apps-api.md)).

## Turning them on

Tokens are off by default. An administrator turns them on under **Security → Portal settings → Personal access
tokens**. Turning the setting off stops every existing token at once (they are kept, and work again when it is
turned back on).

## Creating one

Each account holder creates tokens under **Account → Tokens**:

- **Name**: what uses it ("Nightly sync").
- **Scope**: `read` allows GET requests only; `write` allows everything the account may do through the API.
- **Expiry**: 7, 30, 90 or 365 days. Every token expires.

The value (`opp_…`) is shown once. The portal stores only its SHA-256 hash; a lost token cannot be recovered, only
replaced. An account holds at most 20 active tokens.

## Using one

Send it in the `Authorization` header:

```bash
curl -H "Authorization: Bearer opp_..." https://portal.example.com/api/account/profile
```

- No antiforgery token is needed: the header cannot be set by another site, and while a token is presented the
  session cookie is ignored.
- A `read` token gets 403 `auth.token_scope_insufficient` on anything but GET and HEAD.
- The caller is the token's owner, with their pages and roles: an administrator's token can do what the
  administrator can, so give such tokens the narrowest scope and shortest life that work.
- Rate limits count the owner, like their browser session.
- The OpenAPI document (`/openapi/v1.json`) describes both ways in (`cookieAuth`, `personalAccessToken`).

## What a token never does

Whatever its scope, a token is refused (403 `auth.interactive_session_required`) on the account's sensitive
operations: changing the password, the second factor, managing tokens, downloading the personal data, asking for
the account's deletion, accepting the privacy notice or terms, and starting a support session. It never signs in to
the applications (`/connect/*`), and never reaches the application API (`/api/apps/*`).

## When a token stops working

- It expires, or its owner revokes it (**Account → Tokens**).
- An administrator revokes it (users page → the key icon), or signs the account out everywhere (bulk action
  *Sign out everywhere*).
- The account is locked out, waits for approval or for its email to be confirmed, or is scheduled for deletion.
- The account is deleted (its tokens go with it).

Every creation and revocation is in the audit log; the last use of each token is shown to its owner and to
administrators (updated at most every five minutes).
