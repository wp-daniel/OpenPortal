/**
 * The browser side of passkeys (WebAuthn). The portal hands over the options as WebAuthn's JSON form and takes the
 * credential back as JSON; the browser and the authenticator (phone, laptop, security key) do the cryptography.
 *
 * Browsers that support WebAuthn level 3 parse and serialise that JSON themselves; older ones get the same result
 * from the base64url conversions below.
 */

/** Whether this browser can use passkeys at all. */
export function passkeysSupported(): boolean {
  return typeof window !== 'undefined' && typeof window.PublicKeyCredential === 'function' && !!navigator.credentials
}

/** Asks the authenticator for a new passkey; returns the credential's JSON for the portal. */
export async function createPasskey(options: PublicKeyCredentialCreationOptionsJSON): Promise<string> {
  const publicKey = canParseJson()
    ? PublicKeyCredential.parseCreationOptionsFromJSON(options)
    : creationOptionsFromJson(options)

  const credential = (await navigator.credentials.create({ publicKey })) as PublicKeyCredential | null
  if (!credential) {
    throw new DOMException('No passkey was created.', 'NotAllowedError')
  }

  return JSON.stringify(credentialToJson(credential))
}

/** Asks the authenticator to sign in with one of its passkeys for this portal; returns the credential's JSON. */
export async function getPasskey(options: PublicKeyCredentialRequestOptionsJSON, signal?: AbortSignal): Promise<string> {
  const publicKey = canParseJson()
    ? PublicKeyCredential.parseRequestOptionsFromJSON(options)
    : requestOptionsFromJson(options)

  const credential = (await navigator.credentials.get({ publicKey, signal })) as PublicKeyCredential | null
  if (!credential) {
    throw new DOMException('No passkey was chosen.', 'NotAllowedError')
  }

  return JSON.stringify(credentialToJson(credential))
}

/** True when the person closed the browser's passkey prompt or it timed out: nothing to report as an error. */
export function isPasskeyCancelled(error: unknown): boolean {
  return error instanceof DOMException && (error.name === 'NotAllowedError' || error.name === 'AbortError')
}

function canParseJson(): boolean {
  return typeof PublicKeyCredential.parseCreationOptionsFromJSON === 'function'
}

function credentialToJson(credential: PublicKeyCredential): unknown {
  if (typeof credential.toJSON === 'function') {
    return credential.toJSON()
  }

  const response = credential.response
  const json: Record<string, unknown> = {
    id: credential.id,
    rawId: toBase64Url(credential.rawId),
    type: credential.type,
    authenticatorAttachment: credential.authenticatorAttachment ?? undefined,
    clientExtensionResults: credential.getClientExtensionResults(),
  }

  if (response instanceof AuthenticatorAttestationResponse) {
    json.response = {
      clientDataJSON: toBase64Url(response.clientDataJSON),
      attestationObject: toBase64Url(response.attestationObject),
      transports: typeof response.getTransports === 'function' ? response.getTransports() : [],
    }
  } else {
    const assertion = response as AuthenticatorAssertionResponse
    json.response = {
      clientDataJSON: toBase64Url(assertion.clientDataJSON),
      authenticatorData: toBase64Url(assertion.authenticatorData),
      signature: toBase64Url(assertion.signature),
      userHandle: assertion.userHandle ? toBase64Url(assertion.userHandle) : undefined,
    }
  }

  return json
}

function creationOptionsFromJson(options: PublicKeyCredentialCreationOptionsJSON): PublicKeyCredentialCreationOptions {
  return {
    ...options,
    challenge: fromBase64Url(options.challenge),
    user: { ...options.user, id: fromBase64Url(options.user.id) },
    excludeCredentials: options.excludeCredentials?.map((credential) => ({
      ...credential,
      id: fromBase64Url(credential.id),
      transports: credential.transports as AuthenticatorTransport[] | undefined,
    })),
  } as PublicKeyCredentialCreationOptions
}

function requestOptionsFromJson(options: PublicKeyCredentialRequestOptionsJSON): PublicKeyCredentialRequestOptions {
  return {
    ...options,
    challenge: fromBase64Url(options.challenge),
    allowCredentials: options.allowCredentials?.map((credential) => ({
      ...credential,
      id: fromBase64Url(credential.id),
      transports: credential.transports as AuthenticatorTransport[] | undefined,
    })),
  } as PublicKeyCredentialRequestOptions
}

function toBase64Url(buffer: ArrayBuffer): string {
  let binary = ''
  for (const byte of new Uint8Array(buffer)) {
    binary += String.fromCharCode(byte)
  }

  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

function fromBase64Url(value: string): ArrayBuffer {
  const base64 = value.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(value.length / 4) * 4, '=')
  const binary = atob(base64)
  const bytes = new Uint8Array(binary.length)
  for (let index = 0; index < binary.length; index++) {
    bytes[index] = binary.charCodeAt(index)
  }

  return bytes.buffer
}
