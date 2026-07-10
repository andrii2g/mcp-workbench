# Reference Baseline

This file records the primary references used to prepare the implementation plan.
Codex must prefer official specifications, official SDK documentation, official .NET
documentation, and official package registries.

Last reviewed: 2026-07-11.

## Model Context Protocol

### Specification

- Model Context Protocol specification:
  `https://modelcontextprotocol.io/specification`
- Transports:
  `https://modelcontextprotocol.io/specification/latest/basic/transports`
- Tools:
  `https://modelcontextprotocol.io/specification/latest/server/tools`
- Lifecycle:
  `https://modelcontextprotocol.io/specification/latest/basic/lifecycle`

Always verify the negotiated protocol version and do not hard-code behavior that conflicts
with the official SDK.

### Official C# SDK

- Repository:
  `https://github.com/modelcontextprotocol/csharp-sdk`
- Client quick start:
  `https://csharp.sdk.modelcontextprotocol.io/quickstart/client`
- Transport documentation:
  `https://csharp.sdk.modelcontextprotocol.io/concepts/transports/transports.html`
- API documentation:
  `https://csharp.sdk.modelcontextprotocol.io/api/`

Planning baseline:

```text
Stable SDK release line: 1.4.1
Selected package: ModelContextProtocol.Core
Preview SDKs: prohibited for version 0.1.0
```

Phase 0 must verify package availability with `dotnet restore` and inspect the resolved
dependency graph. Do not silently substitute a preview or third-party MCP package.

## .NET 10 and Native AOT

- ASP.NET Core Native AOT:
  `https://learn.microsoft.com/aspnet/core/fundamentals/native-aot`
- Native AOT deployment:
  `https://learn.microsoft.com/dotnet/core/deploying/native-aot/`
- System.Text.Json source generation:
  `https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/source-generation`
- Trimming:
  `https://learn.microsoft.com/dotnet/core/deploying/trimming/`
- `LoggerMessage` source generation:
  `https://learn.microsoft.com/dotnet/core/extensions/logger-message-generator`

The implementation must use documentation for .NET 10, not older framework behavior when
the versions differ.

## ASP.NET Core

- Minimal APIs:
  `https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis`
- Static files:
  `https://learn.microsoft.com/aspnet/core/fundamentals/static-files`
- Integration tests:
  `https://learn.microsoft.com/aspnet/core/test/integration-tests`
- Health checks:
  `https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks`
- Configuration:
  `https://learn.microsoft.com/aspnet/core/fundamentals/configuration/`

## Package registry

- ModelContextProtocol:
  `https://www.nuget.org/packages/ModelContextProtocol`
- ModelContextProtocol.Core:
  `https://www.nuget.org/packages/ModelContextProtocol.Core`
- Microsoft.AspNetCore.Mvc.Testing:
  `https://www.nuget.org/packages/Microsoft.AspNetCore.Mvc.Testing`
- Microsoft.NET.Test.Sdk:
  `https://www.nuget.org/packages/Microsoft.NET.Test.Sdk`
- xunit.v3:
  `https://www.nuget.org/packages/xunit.v3`
- coverlet.collector:
  `https://www.nuget.org/packages/coverlet.collector`

## Security

- OWASP SSRF prevention:
  `https://cheatsheetseries.owasp.org/cheatsheets/Server_Side_Request_Forgery_Prevention_Cheat_Sheet.html`
- OWASP logging:
  `https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html`
- Content Security Policy:
  `https://developer.mozilla.org/docs/Web/HTTP/CSP`

External security guidance informs the threat model, but repository decisions in
`SECURITY.md` define the required initial implementation.

## Reference update policy

When a reference materially changes:

1. record the review date;
2. identify whether it changes protocol or SDK behavior;
3. add/update an ADR;
4. update affected tests;
5. rerun Native AOT publication;
6. do not alter scope merely because the SDK added a new feature.
