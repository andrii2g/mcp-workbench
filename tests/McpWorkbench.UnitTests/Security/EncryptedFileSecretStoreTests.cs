using A2G.McpWorkbench.Persistence;
using A2G.McpWorkbench.Security;
using Microsoft.AspNetCore.DataProtection;

namespace A2G.McpWorkbench.UnitTests.Security;

public sealed class EncryptedFileSecretStoreTests
{
    [Fact]
    public async Task Store_PersistsEncryptedValueAndCanReloadIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-workbench-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var provider = DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(root, "keys")));
            var path = Path.Combine(root, "secrets.vault");
            const string secret = "vault-sentinel-secret";
            using (var store = new EncryptedFileSecretStore(path, provider, new AtomicFileWriter()))
            {
                await store.InitializeAsync(TestContext.Current.CancellationToken);
                await store.SetAsync("secret-id", secret, TestContext.Current.CancellationToken);
            }

            Assert.DoesNotContain(secret, Convert.ToBase64String(await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken)), StringComparison.Ordinal);

            using var reloaded = new EncryptedFileSecretStore(path, provider, new AtomicFileWriter());
            await reloaded.InitializeAsync(TestContext.Current.CancellationToken);
            Assert.True(reloaded.TryGet("secret-id", out var value));
            Assert.Equal(secret, value);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
