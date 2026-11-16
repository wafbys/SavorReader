namespace SavorReader.Core.Models;

public sealed record ImportSuccess(Book Book);

public sealed record ImportDuplicate(Book Book);

public sealed record ImportFailure(string FilePath, string Message);

// C# 15 union: callers get an exhaustive switch over all import outcomes.
public union ImportOutcome(ImportSuccess, ImportDuplicate, ImportFailure);
