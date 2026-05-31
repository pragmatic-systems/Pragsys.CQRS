using System.Diagnostics.CodeAnalysis;

namespace Pragsys.CQRS;

/// <summary>
/// Base exception for CQRS mediator errors.
/// </summary>
public class CqrsException : Exception
{
    public CqrsException(string message)
        : base(message) { }

    public CqrsException(string message, Type targetType)
        : base(message)
    {
        TargetType = targetType;
    }

    public CqrsException(string message, Exception innerException)
        : base(message, innerException) { }

    public CqrsException(string message, Type targetType, Exception innerException)
        : base(message, innerException)
    {
        TargetType = targetType;
    }

    public Type? TargetType { get; }
}