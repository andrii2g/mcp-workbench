namespace McpWorkbench.Domain;

internal enum ToolExecutionStatus
{
    Succeeded,
    ToolError,
    Failed,
    Cancelled,
    TimedOut
}

internal sealed record ToolExecutionRecord(
    Guid Id,
    Guid ServerId,
    string ToolName,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    long DurationMilliseconds,
    ToolExecutionStatus Outcome,
    bool? IsError,
    string? SafeErrorCode);
