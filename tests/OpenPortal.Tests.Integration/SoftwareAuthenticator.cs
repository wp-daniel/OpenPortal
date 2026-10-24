using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// A passkey authenticator in software: what a phone or a security key does for a browser, enough to drive the
/// portal's WebAuthn ceremonies end to end (ES256 key, "none" attestation, user present and verified).
/// </summary>
internal sealed class SoftwareAuthenticator : IDisposable
{
    public const string Origin = "http://localhost";

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private uint _signCount;

    public byte[] CredentialId { get; } = RandomNumberGenerator.GetBytes(32);

    /// <summary>The user handle the portal put in the creation options (the account id).</summary>
    public byte[] UserHandle { get; private set; } = [];

    /// <summary>The JSON <c>navigator.credentials.create()</c> would produce for these options.</summary>
    public string Create(string optionsJson)
    {
        using var options = JsonDocument.Parse(optionsJson);
        var root = options.RootElement;
        var challenge = root.GetProperty("challenge").GetString()!;
        var rpId = root.GetProperty("rp").GetProperty("id").GetString()!;
        UserHandle = Base64Url.DecodeFromChars(root.GetProperty("user").GetProperty("id").GetString()!);

        var clientData = ClientData("webauthn.create", challenge);
        var parameters = _key.ExportParameters(includePrivateParameters: false);

        var coseKey = Cbor.Map(
            (Cbor.Int(1), Cbor.Int(2)),
            (Cbor.Int(3), Cbor.Int(-7)),
            (Cbor.Int(-1), Cbor.Int(1)),
            (Cbor.Int(-2), Cbor.Bytes(parameters.Q.X!)),
            (Cbor.Int(-3), Cbor.Bytes(parameters.Q.Y!)));

        var authData = new List<byte>();
        authData.AddRange(SHA256.HashData(Encoding.UTF8.GetBytes(rpId)));
        authData.Add(0x01 | 0x04 | 0x40); // user present, user verified, attested credential data
        authData.AddRange(Counter(_signCount));
        authData.AddRange(new byte[16]); // AAGUID
        authData.Add((byte)(CredentialId.Length >> 8));
        authData.Add((byte)CredentialId.Length);
        authData.AddRange(CredentialId);
        authData.AddRange(coseKey);

        var attestationObject = Cbor.Map(
            (Cbor.Text("fmt"), Cbor.Text("none")),
            (Cbor.Text("attStmt"), Cbor.Map()),
            (Cbor.Text("authData"), Cbor.Bytes([.. authData])));

        return JsonSerializer.Serialize(new
        {
            id = Base64Url.EncodeToString(CredentialId),
            rawId = Base64Url.EncodeToString(CredentialId),
            type = "public-key",
            authenticatorAttachment = "platform",
            clientExtensionResults = new { },
            response = new
            {
                clientDataJSON = Base64Url.EncodeToString(clientData),
                attestationObject = Base64Url.EncodeToString(attestationObject),
                transports = new[] { "internal" },
            },
        });
    }

    /// <summary>The JSON <c>navigator.credentials.get()</c> would produce; <paramref name="tamper"/> breaks the signature.</summary>
    public string Get(string optionsJson, bool tamper = false)
    {
        using var options = JsonDocument.Parse(optionsJson);
        var root = options.RootElement;
        var challenge = root.GetProperty("challenge").GetString()!;
        var rpId = root.TryGetProperty("rpId", out var id) ? id.GetString()! : "localhost";

        var clientData = ClientData("webauthn.get", challenge);

        _signCount++;
        var authData = new List<byte>();
        authData.AddRange(SHA256.HashData(Encoding.UTF8.GetBytes(rpId)));
        authData.Add(0x01 | 0x04);
        authData.AddRange(Counter(_signCount));

        var signed = authData.Concat(SHA256.HashData(clientData)).ToArray();
        var signature = _key.SignData(signed, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        if (tamper)
        {
            signature[^1] ^= 0xFF;
        }

        return JsonSerializer.Serialize(new
        {
            id = Base64Url.EncodeToString(CredentialId),
            rawId = Base64Url.EncodeToString(CredentialId),
            type = "public-key",
            authenticatorAttachment = "platform",
            clientExtensionResults = new { },
            response = new
            {
                clientDataJSON = Base64Url.EncodeToString(clientData),
                authenticatorData = Base64Url.EncodeToString([.. authData]),
                signature = Base64Url.EncodeToString(signature),
                userHandle = Base64Url.EncodeToString(UserHandle),
            },
        });
    }

    public void Dispose() => _key.Dispose();

    private static byte[] ClientData(string type, string challenge) =>
        JsonSerializer.SerializeToUtf8Bytes(new { type, challenge, origin = Origin, crossOrigin = false });

    private static byte[] Counter(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }

    /// <summary>The few CBOR items WebAuthn needs (RFC 8949): integers, byte and text strings, maps.</summary>
    private static class Cbor
    {
        public static byte[] Int(long value) =>
            value >= 0 ? Head(0, (ulong)value) : Head(1, (ulong)(-1 - value));

        public static byte[] Bytes(byte[] value) => [.. Head(2, (ulong)value.Length), .. value];

        public static byte[] Text(string value)
        {
            var utf8 = Encoding.UTF8.GetBytes(value);
            return [.. Head(3, (ulong)utf8.Length), .. utf8];
        }

        public static byte[] Map(params (byte[] Key, byte[] Value)[] entries) =>
            [.. Head(5, (ulong)entries.Length), .. entries.SelectMany(entry => entry.Key.Concat(entry.Value))];

        private static byte[] Head(int major, ulong value)
        {
            var type = (byte)(major << 5);

            return value switch
            {
                < 24 => [(byte)(type | (byte)value)],
                <= byte.MaxValue => [(byte)(type | 24), (byte)value],
                <= ushort.MaxValue => [(byte)(type | 25), (byte)(value >> 8), (byte)value],
                _ => [(byte)(type | 26), (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value],
            };
        }
    }
}
