 # PluginHost

Secure plugin host with encrypted transport, isolated plugin loading, and deterministic testable crypto.

## Build & Run

- Build:
	- `dotnet build`
- Run API:
	- `dotnet run --project src/PluginHost.Api`
- Run tests:
	- `dotnet test`

### Trust the HTTPS development certificate (one-time)

To trust the localhost SSL certificate for HTTPS during development, run the one-time step:

`dotnet dev-certs https --trust`

## Cryptography

- Key exchange: **ECDiffieHellman** (NIST P-256 / `secp256r1`)
	- Client and server public keys are exchanged as base64-encoded DER **SubjectPublicKeyInfo (SPKI)** blobs.
- Key derivation: **HKDF-SHA256**
	- Output key material: 32 bytes (AES-256 key)
	- HKDF `info`: `PluginHost-AES-256-GCM`
	- HKDF `salt`: `SHA-256(clientPublicKeyDer || serverPublicKeyDer)`
- Transport encryption: **AES-256-GCM**
	- Nonce/IV: 12 bytes
	- Tag: 16 bytes (128-bit)
	- Additional Authenticated Data (AAD): `${sessionId}|${clientFingerprint}`

## Handshake Flow (Textual Diagram)

```
Client                                    Server
	| POST /handshake/init (client public key, fingerprint)
	|----------------------------------------------------->
	|                             derives shared secret
	|                             HKDF -> AES-256-GCM key
	|<-----------------------------------------------------
	| 200 OK (server public key, sessionId, expiresAtUtc)
	|  both sides derive identical AES key
```

## Encrypted Payload Format

All encrypted endpoints require a JSON envelope:

```json
{
	"sessionId": "...",
	"clientFingerprint": "...",
	"nonceBase64": "...",
	"ciphertextBase64": "...",
	"tagBase64": "...",
	"version": 1
}
```

The envelope fields map to:

- `nonceBase64`: 12-byte AES-GCM nonce
- `ciphertextBase64`: encrypted bytes of the plaintext request JSON
- `tagBase64`: 16-byte AES-GCM authentication tag

## API Specification

### JSON naming

ASP.NET Core uses `JsonSerializerDefaults.Web` which means JSON property names are `camelCase`.

Example: `ExecuteRequest.TargetOS` is sent as `targetOS`.

### Transport rules

- `POST /handshake/init` and `GET /plugins/` are plaintext (no envelope).
- All other endpoints are protected by `EncryptionMiddleware` and expect the encrypted envelope as the HTTP body.
  - The *ciphertext* decrypts to the plaintext DTO JSON shown below.
  - Responses from protected endpoints are also returned as an encrypted envelope.

### Endpoints

#### POST /handshake/init (plaintext)

Request body (matches `HandshakeRequest`):

```json
{
	"clientPublicKeyBase64": "<base64 SPKI DER>",
	"clientFingerprint": "<string>"
}
```

Response body (matches `HandshakeResponse`):

```json
{
	"sessionId": "<string>",
	"serverPublicKeyBase64": "<base64 SPKI DER>",
	"expiresAtUtc": "2026-01-27T00:00:00Z"
}
```

#### POST /plugins (protected)

Wire request/response body: `EncryptedPayload` envelope.

Plaintext DTO inside the ciphertext (matches `PluginUploadRequest`):

```json
{
	"pluginName": "string",
	"assemblyBase64": "<base64 DLL>"
}
```

#### GET /plugins (plaintext)

Returns the plugin metadata list.

#### DELETE /plugins (protected)

Wire request/response body: `EncryptedPayload` envelope.

Plaintext DTO inside the ciphertext (matches `PluginUnloadRequest`):

```json
{
	"name": "string",
	"targetOs": "string",
	"version": "string"
}
```

#### POST /execute (protected)

Wire request/response body: `EncryptedPayload` envelope.

Plaintext DTO inside the ciphertext (matches `ExecuteRequest`):

```json
{
	"targetOS": "string",
	"supportedVersion": "string",
	"command": "string"
}
```

## Session Storage (In-Memory)

- `ConcurrentDictionary<string, SessionContext>` only
- TTL enforced, expired sessions are disposed and zeroed
- No secrets persisted or serialized

## Disposal Strategy (Explicit)

- `ECDiffieHellman`, `AesGcm`, and all streams are disposed deterministically
- Session keys are zeroed on disposal
- Plugin load contexts are **collectible** and are requested to unload via `AssemblyLoadContext.Unload()`
	- Actual memory reclamation happens when the GC runs; the runtime does not force `GC.Collect()`.

## Plugin Loading / Unloading Strategy

- Each plugin is loaded into an isolated, collectible `AssemblyLoadContext`.
- The plugin DLL is persisted under `src/PluginHost.Api/plugin_storage/` so dependencies can be resolved.
- The main plugin assembly is loaded via `LoadFromStream(...)` to avoid holding an OS file lock on the DLL.
- Unload disposes the plugin instance (sync or async), then calls `Unload()` on the plugin load context.

## Plugin Lifecycle

1. Upload via `POST /plugins` (encrypted envelope; plaintext payload contains `pluginName` + `assemblyBase64`)
2. Plugin loads into isolated `AssemblyLoadContext`
3. Metadata available via `GET /plugins`
4. Execute via `POST /execute` (encrypted envelope; plaintext payload contains `targetOS`, `supportedVersion`, `command`)
5. Unload via `DELETE /plugins` (encrypted body)

## Testing Endpoints

### Swagger (recommended for quick exploration)

1. Run the API: `dotnet run --project src/PluginHost.Api`
2. Open: `https://localhost:5001/swagger`

Important:

- Swagger shows the **plaintext** DTOs (`ExecuteRequest`, `PluginUploadRequest`, etc.).
- The API enforces encrypted transport for all endpoints except `POST /handshake/init`.
- To call protected endpoints successfully, the client must send the encrypted envelope shown above.

If you send plaintext JSON to a protected endpoint while encryption is enabled, you will typically see:

- `401` when the request body is not a valid encrypted envelope, or when the session is missing/expired.
- `403` when the encrypted envelope exists but AES-GCM authentication fails.

If you want to test endpoints with **plaintext DTOs** (matching Swagger models) during local development, you have two options:

1. Set `Security:RequireEncryption` to `false` in `src/PluginHost.Api/appsettings.Development.json`.
2. Or, mark specific controller actions with `[AllowUnencrypted]`.

Do not use either option in production.

### Handshake (plaintext)

```bash
curl -X POST https://localhost:5001/handshake/init \
	-H "Content-Type: application/json" \
	-d '{"clientPublicKeyBase64":"<base64>","clientFingerprint":"client-a"}'
```

### Curl and encrypted endpoints

The examples below show the **wire format** only. Generating valid `nonceBase64` / `ciphertextBase64` / `tagBase64` requires performing ECDH + HKDF and AES-GCM on the client side.

### Encrypted Upload (format only)

```bash
curl -X POST https://localhost:5001/plugins \
	-H "Content-Type: application/json" \
	-d '{"sessionId":"...","clientFingerprint":"client-a","nonceBase64":"...","ciphertextBase64":"...","tagBase64":"...","version":1}'
```

### Encrypted Execute (format only)

```bash
curl -X POST https://localhost:5001/execute \
	-H "Content-Type: application/json" \
	-d '{"sessionId":"...","clientFingerprint":"client-a","nonceBase64":"...","ciphertextBase64":"...","tagBase64":"...","version":1}'
```

### Encrypted Unload (format only)

```bash
curl -X DELETE https://localhost:5001/plugins \
	-H "Content-Type: application/json" \
	-d '{"sessionId":"...","clientFingerprint":"client-a","nonceBase64":"...","ciphertextBase64":"...","tagBase64":"...","version":1}'
```

Note: Swagger shows plaintext DTOs for encrypted endpoints. Only `/handshake/init` accepts plaintext.

## Postman Collection

Use the collection at [postman/PluginHost.postman_collection.json](postman/PluginHost.postman_collection.json). It:

- Performs handshake
- Stores `sessionId`
- Includes request bodies that match the plaintext API DTOs

Note: If `EncryptionMiddleware` is enabled (default), protected endpoints still require the encrypted envelope and these plaintext DTO requests will be rejected.

Required environment variables:

- `baseUrl` (e.g. `https://localhost:5001`)
- `clientFingerprint` (any stable client identifier)
- `pluginName` (e.g. `WindowsEcho`)
- `pluginAssemblyBase64` (base64-encoded plugin DLL)
- `targetOS` (e.g. `windows` or `linux`)
- `supportedVersion` (e.g. `1.0.0`)
- `command` (string command payload)

## Security Assumptions & Trade-offs

- Single-node runtime with in-memory session storage
- No secret persistence or logging
- Clear rejection paths on malformed or unauthenticated payloads
