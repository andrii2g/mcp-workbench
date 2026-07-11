using A2G.McpWorkbench.Domain;

namespace A2G.McpWorkbench.Mcp;

internal sealed class BoundedExecutionHistory
{
    private readonly ToolExecutionRecord?[] _entries;
    private readonly object _gate = new();
    private int _count;
    private int _next;

    public BoundedExecutionHistory(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _entries = new ToolExecutionRecord[capacity];
    }

    public void Add(ToolExecutionRecord entry)
    {
        lock (_gate)
        {
            _entries[_next] = entry;
            _next = (_next + 1) % _entries.Length;
            _count = Math.Min(_count + 1, _entries.Length);
        }
    }

    public IReadOnlyList<ToolExecutionRecord> Snapshot()
    {
        lock (_gate)
        {
            var result = new ToolExecutionRecord[_count];
            var start = (_next - _count + _entries.Length) % _entries.Length;
            for (var index = 0; index < _count; index++)
            {
                result[index] = _entries[(start + index) % _entries.Length]!;
            }

            return result;
        }
    }
}
