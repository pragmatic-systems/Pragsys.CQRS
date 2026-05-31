using Shouldly;
using Xunit;

namespace Pragsys.CQRS.Tests;

public class CqrsExceptionTests
{
    private static readonly Type DummyType = typeof(object);

    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        var message = "Something went wrong";
        var exception = new CqrsException(message);

        exception.Message.ShouldBe(message);
        exception.TargetType.ShouldBeNull();
        exception.InnerException.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithMessageAndTargetType_SetsMessageAndTargetType()
    {
        var message = "Handler not found";
        var exception = new CqrsException(message, DummyType);

        exception.Message.ShouldBe(message);
        exception.TargetType.ShouldBe(DummyType);
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_SetsMessageAndInnerException()
    {
        var message = "Outer error";
        var inner = new InvalidOperationException("Inner error");
        var exception = new CqrsException(message, inner);

        exception.Message.ShouldBe(message);
        exception.InnerException.ShouldBe(inner);
        exception.TargetType.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithMessageTargetTypeAndInnerException_SetsAllProperties()
    {
        var message = "Handler not found";
        var inner = new InvalidOperationException("Inner error");
        var exception = new CqrsException(message, DummyType, inner);

        exception.Message.ShouldBe(message);
        exception.TargetType.ShouldBe(DummyType);
        exception.InnerException.ShouldBe(inner);
    }
}
