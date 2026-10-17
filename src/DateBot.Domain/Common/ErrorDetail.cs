namespace DateBot.Domain.Common;

/// <summary>One validation violation: which property, which rule, human-readable message.</summary>
public sealed record ErrorDetail(string PropertyName, string ErrorCode, string Message);
