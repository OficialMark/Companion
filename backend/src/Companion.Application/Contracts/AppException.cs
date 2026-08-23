namespace Companion.Application.Contracts;

public enum AppErrorKind
{
    Validation,
    NotFound,
    Unauthorized,
    Forbidden,
    Conflict
}

public sealed class AppException(AppErrorKind kind, string message) : Exception(message)
{
    public AppErrorKind Kind { get; } = kind;
}
