# Threat review

This review covers the local MCP Workbench host, its registry, MCP transports, API, and static UI.

| Threat | Control | Residual risk |
| --- | --- | --- |
| Untrusted clients call the API | Optional API key is compared in constant time; API routes can be protected independently of health probes. | An unset key is intentional for loopback-only development. Operators allowing remote binding must configure authentication and network controls. |
| Accidental remote exposure | Loopback is the default. Remote URLs are rejected while `BindToLoopbackOnly` is enabled and produce a warning without an API key when explicitly enabled. | The API key is a shared secret, not user identity or authorization. |
| Arbitrary local command execution | Stdio transport can be disabled and commands can be restricted by an exact allowlist. Commands are passed directly to the SDK transport without a shell. | A permitted MCP server executes with the Workbench OS identity and must be trusted. |
| SSRF or credential forwarding | HTTP transport can be disabled, hosts can be allowlisted, redirects are disabled, and runtime-controlled headers are rejected. Secret values support environment references. | DNS and permitted hosts remain part of the operator trust boundary. |
| Secret disclosure | API responses redact configured environment/header secrets. Protocol payloads and API keys are not logged by application code at the default information level. | A connected MCP server receives secrets explicitly configured for that server. Host-level diagnostics may observe process environment or traffic. |
| Malicious MCP content | UI rendering uses `textContent`; CSP blocks third-party scripts, objects, framing, and base-URI changes. Responses are serialized as data, not HTML. | Users can still copy or act on deceptive text returned by an MCP server. |
| Resource exhaustion | Request bodies, invocation arguments/results, tool catalog count, operation duration, and history are bounded. Oversized requests return a predictable `413`. | The MCP SDK owns stdio reads, including stderr. Workbench does not retain or render stderr, so there is no application stderr buffer to bound. OS-level output behavior remains SDK-dependent. |
| Orphaned child processes | Sessions dispose SDK clients on disconnect, replacement, failure, and hosted-service shutdown. Integration tests verify that disposing a real stdio session terminates its owned process. | Forced host termination can bypass graceful shutdown; external process supervision remains responsible for cleanup after a hard kill. |
| Registry disclosure or corruption | Registry writes are atomic, schema-validated, and secrets are represented by references where configured. Static files cannot serve the data directory. | Filesystem permissions and backups remain operator responsibilities. |

The security boundary assumes the machine account, configured MCP servers, command/host allowlists, and environment secret source are administered by a trusted operator.
