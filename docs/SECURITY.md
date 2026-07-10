# Security Model

MCP Workbench is an administrative developer tool. Registering a stdio MCP server grants
the application permission to start a configured local process. Registering an HTTP MCP
server grants it permission to send configured requests and credentials to that endpoint.
Treat access to the dashboard as privileged.

## Trust boundaries

```text
Browser
  -> MCP Workbench HTTP boundary
      -> persisted registry
      -> host process boundary for stdio servers
      -> network boundary for HTTP servers
      -> untrusted MCP response boundary
```

Untrusted inputs include:

- all API request bodies and path values;
- registry content on disk;
- environment-variable values;
- MCP server identity and capability metadata;
- tool names, descriptions, schemas, annotations, and results;
- child-process stderr;
- remote HTTP error bodies.

## Deployment default

The default listener must be loopback only. A typical development URL is:

```text
http://127.0.0.1:5070
```

Remote exposure requires all of:

1. explicit non-loopback binding;
2. configured API key or an external authenticated reverse proxy;
3. HTTPS at the proxy or application boundary;
4. firewall restrictions;
5. review of stdio command and HTTP host allowlists.

Never present this service as safe for unauthenticated public internet exposure.

## API authentication

Version 1 supports an optional static API key:

```http
X-Mcp-Workbench-Key: <secret>
```

Implementation requirements:

- read from application configuration, preferably an environment variable or secret
  manager;
- never write it to appsettings committed to source control;
- compare UTF-8 bytes using a constant-time comparison;
- reject missing/invalid key with the same response shape;
- never include the supplied key in logs;
- do not accept the key in query strings;
- support key rotation through process restart in version 1.

This is adequate for a local/single-operator tool. Multi-user identities, RBAC, sessions,
and OAuth login are intentionally out of scope.

## Cross-site request risks

When API-key authentication is disabled, bind to loopback and reject unexpected `Host`
headers according to ASP.NET Core host filtering.

When API-key authentication is enabled:

- require the custom header on mutations and reads;
- do not enable permissive CORS;
- the bundled UI calls same-origin endpoints;
- do not use cookie authentication;
- reject cross-origin requests by default.

## Stdio process security

### No shell interpretation

A definition contains one command and an argument array. The application must not create
a single shell string or invoke a shell implicitly.

Good:

```text
command: dotnet
arguments: ["server.dll", "--root", "/data"]
```

Forbidden:

```text
command: /bin/sh
arguments: ["-c", "download-and-run ..."]
```

A deployment may intentionally allow shell executables, but the application does not add
special handling or claim this is safe.

### Command allowlist

`AllowedStdioCommands` may restrict exact normalized executable paths/names. Empty means
the administrator accepts unrestricted registered commands.

Document platform normalization carefully:

- Windows comparison is case-insensitive after full-path resolution when applicable;
- Linux comparison is ordinal case-sensitive;
- do not resolve through a shell;
- symbolic-link policy must be documented if full paths are enforced.

### Working directory and environment

- reject NUL/control characters;
- do not expose the parent process's entire environment through the API;
- allow explicit child environment overrides;
- recommend secret references in environment values;
- never persist resolved values;
- do not let a tool call modify the registered process command.

### Process lifetime

Only terminate processes created and owned by the corresponding runtime. On disconnect,
use graceful disposal then bounded forced termination. Avoid orphan processes.

## HTTP server security

### SSRF boundary

An HTTP MCP endpoint can reach network resources visible to the host. Mitigations:

- HTTPS by default;
- optional exact host allowlist;
- block URI user-info and fragments;
- reject malformed IP literals;
- treat redirects as untrusted and disable them unless every redirect target is
  revalidated;
- never forward MCP credentials to a different host;
- provide an operator option to disallow loopback, link-local, private, or metadata
  address ranges in a later hardened deployment mode.

Version 1's primary protection is trusted administrative access plus an allowlist. The
README must state that registering arbitrary endpoints is equivalent to granting outbound
network access.

### Headers

Reject runtime-controlled headers and validate all names/values. Redact authentication,
cookie, token, password, and secret values.

### TLS

Never add a "skip certificate validation" feature. Local development can use a trusted
development certificate or loopback HTTP.

## Secret handling

Supported persisted reference:

```text
${ENV:VARIABLE_NAME}
```

Resolution is ephemeral and limited to:

- stdio environment values;
- HTTP header values.

Resolved values must not enter:

- server-definition response DTOs;
- runtime snapshots;
- tool execution history;
- exception messages;
- structured logs;
- tracing tags;
- diagnostic dumps created by the app.

The resolver returns disposable/short-lived data where practical. Managed strings cannot
be reliably zeroed; the key control is minimizing copies and lifetime.

## MCP content safety

Remote MCP data is untrusted.

### Browser rendering

- use `textContent`, never `innerHTML`, for names, descriptions, schemas, tool text, and
  errors;
- render JSON through text nodes;
- do not execute HTML, Markdown HTML, scripts, SVG scripts, or data-provided JavaScript;
- image previews accept only supported encoded image content and enforce size/MIME checks;
- resource links are displayed but not automatically fetched;
- external links use safe `rel` attributes and require an explicit user action.

### Size and depth

Enforce limits on:

- catalog count;
- aggregate schema bytes;
- result bytes;
- JSON depth;
- text block length;
- image encoded/decoded size;
- stderr diagnostic capture.

Reject or clearly report bounded truncation. Never allocate based solely on remote length
claims.

## Tool invocation

The tool description's annotations are advisory, not authorization. A
`destructiveHint=false` value does not make a tool safe.

The UI must:

- display destructive/read-only/idempotent hints when present;
- require an explicit click for every invocation;
- never invoke a tool automatically on selection or page load;
- make the target server and tool visible near the Run action;
- show raw arguments before submission.

No server-side allow/deny policy by tool name is required for initial scope, but the
architecture must permit one later.

## Logging

Log:

- server ID, not secret configuration;
- transport kind;
- lifecycle transition;
- operation name;
- duration;
- safe outcome/error code;
- request/trace ID.

Do not log by default:

- tool arguments;
- tool results;
- HTTP headers;
- child environment;
- resolved endpoint credentials;
- full child stderr;
- raw MCP frames.

Use source-generated logging methods and centralized redaction.

## Static UI headers

Recommended headers:

```text
Content-Security-Policy:
  default-src 'self';
  script-src 'self';
  style-src 'self';
  img-src 'self' data:;
  connect-src 'self';
  object-src 'none';
  base-uri 'none';
  frame-ancestors 'none';
  form-action 'self'

X-Content-Type-Options: nosniff
Referrer-Policy: no-referrer
Permissions-Policy: camera=(), microphone=(), geolocation=()
Cross-Origin-Opener-Policy: same-origin
```

Avoid inline scripts and styles so CSP does not require unsafe directives.

## Registry file protection

- create parent directory with host-appropriate restricted permissions when possible;
- document that OS account permissions protect the file;
- never store resolved secrets;
- use atomic writes;
- do not follow an attacker-controlled registry symlink in hardened deployments;
- do not serve the data directory as static files.

## Denial-of-service controls

Initial controls:

- request body limit;
- tool argument/result limits;
- one invocation per server;
- bounded operation timeouts;
- bounded history;
- bounded stderr capture;
- bounded registry/server/catalog sizes;
- graceful cancellation on client disconnect and application shutdown.

A global invocation limit may be added before multi-user or remote deployment.

## Security tests

Required tests include:

- API key accepted/rejected and not logged;
- constant-time comparison utility behavior;
- secret references resolve only in permitted fields;
- unresolved secret fails safely;
- sensitive headers are redacted;
- child command arguments preserve boundaries;
- no shell is introduced;
- malicious tool text is rendered as text;
- oversized arguments/results are rejected;
- HTTP redirects do not leak headers;
- invalid endpoint schemes and user-info are rejected;
- registry traversal/static exposure is impossible;
- tool annotations do not bypass explicit invocation.

## Security non-goals

Initial scope does not provide:

- sandboxing of stdio processes;
- malware scanning;
- per-tool authorization;
- tenant isolation;
- multi-user RBAC;
- secrets vault integration;
- comprehensive egress firewalling;
- supply-chain verification of arbitrary MCP server packages.

The documentation must state these limitations plainly.
