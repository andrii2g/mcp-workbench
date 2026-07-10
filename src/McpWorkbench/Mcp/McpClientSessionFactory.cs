using McpWorkbench.Domain;
using McpWorkbench.Options;
using McpWorkbench.Security;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace McpWorkbench.Mcp;

internal sealed class McpClientSessionFactory(
    ILoggerFactory loggerFactory,
    IOptions<WorkbenchOptions> options) : IMcpClientSessionFactory
{
    public async ValueTask<IMcpClientSession> CreateAsync(
        McpServerDefinition definition,
        ResolvedTransportSettings resolvedSettings,
        CancellationToken cancellationToken)
    {
        IClientTransport? transport = null;
        try
        {
            transport = CreateTransport(definition, resolvedSettings);
            var clientOptions = new McpClientOptions
            {
                ClientInfo = new Implementation
                {
                    Name = "mcp-workbench",
                    Title = "MCP Workbench",
                    Version = "0.1.0"
                },
                InitializationTimeout = TimeSpan.FromSeconds(options.Value.ConnectTimeoutSeconds)
            };
            var client = await McpClient.CreateAsync(transport, clientOptions, loggerFactory, cancellationToken);
            transport = null;
            return new McpClientSession(
                new McpSdkClient(client),
                MapSessionInfo(client, resolvedSettings),
                options.Value.MaximumResultBytes);
        }
        catch (Exception exception)
        {
            if (transport is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }

            throw McpSdkErrorNormalizer.Normalize(exception, "initialization");
        }
    }

    private IClientTransport CreateTransport(
        McpServerDefinition definition,
        ResolvedTransportSettings resolvedSettings) => resolvedSettings.Transport switch
        {
            McpTransportKind.Stdio when resolvedSettings.Stdio is not null => CreateStdioTransport(definition, resolvedSettings.Stdio),
            McpTransportKind.Http when resolvedSettings.Http is not null => CreateHttpTransport(definition, resolvedSettings.Http),
            _ => throw new McpSessionException("server_definition_invalid", "Resolved transport settings do not match the server definition.")
        };

    private StdioClientTransport CreateStdioTransport(
        McpServerDefinition definition,
        ResolvedStdioTransportSettings settings) => new(
            new StdioClientTransportOptions
            {
                Name = definition.Name,
                Command = settings.Command,
                Arguments = settings.Arguments.ToList(),
                WorkingDirectory = settings.WorkingDirectory,
                InheritEnvironmentVariables = true,
                EnvironmentVariables = settings.Environment.ToDictionary(
                    pair => pair.Key,
                    pair => (string?)pair.Value,
                    StringComparer.Ordinal),
                ShutdownTimeout = TimeSpan.FromSeconds(settings.ShutdownTimeoutSeconds)
            },
            loggerFactory);

    private HttpClientTransport CreateHttpTransport(
        McpServerDefinition definition,
        ResolvedHttpTransportSettings settings)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        };
        var httpClient = new HttpClient(handler, disposeHandler: true);
        var transportOptions = new HttpClientTransportOptions
        {
            Name = definition.Name,
            Endpoint = settings.Endpoint,
            TransportMode = settings.Mode switch
            {
                McpHttpMode.Auto => HttpTransportMode.AutoDetect,
                McpHttpMode.StreamableHttp => HttpTransportMode.StreamableHttp,
                McpHttpMode.LegacySse => HttpTransportMode.Sse,
                _ => throw new McpSessionException("server_definition_invalid", "HTTP transport mode is unsupported.")
            },
            ConnectionTimeout = TimeSpan.FromSeconds(options.Value.ConnectTimeoutSeconds),
            AdditionalHeaders = new Dictionary<string, string>(settings.Headers, StringComparer.OrdinalIgnoreCase),
            MaxReconnectionAttempts = 0
        };
        return new HttpClientTransport(transportOptions, httpClient, loggerFactory, ownsHttpClient: true);
    }

    private static McpSessionInfo MapSessionInfo(McpClient client, ResolvedTransportSettings settings)
    {
        var capabilities = client.ServerCapabilities;
        return new McpSessionInfo(
            client.NegotiatedProtocolVersion,
            new McpRemoteIdentity(client.ServerInfo.Name, client.ServerInfo.Version, client.ServerInfo.Title),
            new McpCapabilitySnapshot(capabilities.Tools is not null, capabilities.Tools?.ListChanged ?? false),
            settings.Transport == McpTransportKind.Http ? settings.Http?.Mode : null);
    }
}
