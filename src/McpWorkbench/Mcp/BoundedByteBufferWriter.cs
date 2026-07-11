using System.Buffers;

namespace McpWorkbench.Mcp;

internal sealed class BoundedByteBufferWriter(int maximumBytes) : IBufferWriter<byte>
{
    private byte[] _buffer = new byte[Math.Min(maximumBytes, 4_096)];
    private int _written;

    public ReadOnlyMemory<byte> WrittenMemory => _buffer.AsMemory(0, _written);

    public void Advance(int count)
    {
        if (count < 0 || count > _buffer.Length - _written)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        _written += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsMemory(_written);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsSpan(_written);
    }

    private void EnsureCapacity(int sizeHint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
        sizeHint = Math.Max(sizeHint, 1);
        if (sizeHint > maximumBytes - _written)
        {
            throw new McpSessionException("result_too_large", "MCP tool result exceeds the configured size limit.");
        }

        var required = _written + sizeHint;
        if (required <= _buffer.Length)
        {
            return;
        }

        var capacity = Math.Min(maximumBytes, Math.Max(required, _buffer.Length * 2));
        Array.Resize(ref _buffer, capacity);
    }
}
