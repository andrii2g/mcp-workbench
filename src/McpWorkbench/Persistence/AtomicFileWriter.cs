namespace A2G.McpWorkbench.Persistence;

internal interface IAtomicFileWriter
{
    ValueTask WriteAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);
}

internal sealed class AtomicFileWriter : IAtomicFileWriter
{
    public async ValueTask WriteAsync(
        string path,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Registry path has no parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = path + ".tmp";
        var ownsTemporaryFile = false;
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16_384,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                ownsTemporaryFile = true;
                await stream.WriteAsync(content, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        catch
        {
            if (ownsTemporaryFile)
            {
                TryDeleteTemporaryFile(temporaryPath);
            }

            throw;
        }
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
