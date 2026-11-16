namespace SavorReader.Core.Models;

public enum CleanupTargetKind
{
    Immediate,
    AfterRestart,
}

public sealed record CleanupTarget(
    string Key,
    string Name,
    long Bytes,
    int FileCount,
    CleanupTargetKind Kind);

public sealed record CleanupReport(
    IReadOnlyList<CleanupTarget> Targets,
    long ImmediateBytes,
    long DeferredBytes)
{
    public long TotalBytes => ImmediateBytes + DeferredBytes;

    public bool HasWork => TotalBytes > 0;
}

public sealed record CleanupResult(
    long RemovedBytes,
    int RemovedFiles,
    long ScheduledBytes,
    IReadOnlyList<string> Errors);
