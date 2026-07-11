using System.Text.Json;
using A2G.McpWorkbench.Persistence;
using A2G.McpWorkbench.Serialization;
using Microsoft.AspNetCore.DataProtection;

namespace A2G.McpWorkbench.Security;

internal sealed record SecretVaultDocument(int Version, Dictionary<string, string> Secrets)
{
    public static SecretVaultDocument Empty() => new(1, new Dictionary<string, string>(StringComparer.Ordinal));
}

internal sealed class EncryptedFileSecretStore(
    string path,
    IDataProtectionProvider dataProtection,
    IAtomicFileWriter writer) : ISecretStore, IDisposable
{
    private readonly string _path = Path.GetFullPath(path);
    private readonly IDataProtector _protector = dataProtection.CreateProtector("A2G.McpWorkbench.SecretVault.v1");
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SecretVaultDocument? _document;

    public async ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_document is not null) return;
            if (!File.Exists(_path))
            {
                _document = SecretVaultDocument.Empty();
                await PersistAsync(_document, cancellationToken);
                return;
            }
            var protectedBytes = await File.ReadAllBytesAsync(_path, cancellationToken);
            var json = _protector.Unprotect(protectedBytes);
            _document = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.SecretVaultDocument)
                ?? throw new InvalidOperationException("Secret vault is empty.");
            if (_document.Version != 1) throw new InvalidOperationException("Secret vault version is unsupported.");
        }
        finally { _gate.Release(); }
    }

    public bool TryGet(string id, out string value)
    {
        var document = _document ?? throw new InvalidOperationException("Secret vault is not initialized.");
        return document.Secrets.TryGetValue(id, out value!);
    }

    public async ValueTask SetAsync(string id, string value, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = _document ?? throw new InvalidOperationException("Secret vault is not initialized.");
            var secrets = new Dictionary<string, string>(current.Secrets, StringComparer.Ordinal) { [id] = value };
            var next = current with { Secrets = secrets };
            await PersistAsync(next, cancellationToken);
            _document = next;
        }
        finally { _gate.Release(); }
    }

    public async ValueTask DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = _document ?? throw new InvalidOperationException("Secret vault is not initialized.");
            var secrets = new Dictionary<string, string>(current.Secrets, StringComparer.Ordinal);
            if (!secrets.Remove(id)) return;
            var next = current with { Secrets = secrets };
            await PersistAsync(next, cancellationToken);
            _document = next;
        }
        finally { _gate.Release(); }
    }

    private ValueTask PersistAsync(SecretVaultDocument document, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(document, AppJsonSerializerContext.Default.SecretVaultDocument);
        return writer.WriteAsync(_path, _protector.Protect(json), cancellationToken);
    }

    public void Dispose() => _gate.Dispose();
}
