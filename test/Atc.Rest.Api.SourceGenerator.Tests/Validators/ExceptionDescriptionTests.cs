namespace Atc.Rest.Api.SourceGenerator.Tests.Validators;

/// <summary>
/// A generator failure (<c>ATC_API_GEN001</c>/<c>003</c>/<c>005</c>/<c>008</c>) says what was thrown and where: the
/// exception type, its message, the first frames of its stack and the inner exception - not the bare message, which
/// for a <c>NullReferenceException</c> is "Object reference not set to an instance of an object" and nothing else.
/// </summary>
public sealed class ExceptionDescriptionTests
{
    [Fact]
    public void AThrownException_IsDescribedWithItsTypeMessageAndFrames()
    {
        Exception thrown;
        try
        {
            ExplodeInAMethodOfItsOwn();
            throw new InvalidOperationException("unreachable");
        }
        catch (NullReferenceException ex)
        {
            thrown = ex;
        }

        var description = DiagnosticHelpers.DescribeException(thrown);

        Assert.StartsWith("NullReferenceException: ", description, StringComparison.Ordinal);
        Assert.Contains(thrown.Message, description, StringComparison.Ordinal);
        Assert.Contains(nameof(ExplodeInAMethodOfItsOwn), description, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", description, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInnerException_IsNamedToo()
    {
        var thrown = new InvalidOperationException("the outer", new FormatException("the inner"));

        var description = DiagnosticHelpers.DescribeException(thrown);

        Assert.StartsWith("InvalidOperationException: the outer", description, StringComparison.Ordinal);
        Assert.Contains("FormatException: the inner", description, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExceptionNeverThrown_HasNoFrames()
        => Assert.Equal("InvalidOperationException: plain", DiagnosticHelpers.DescribeException(new InvalidOperationException("plain")));

    private static void ExplodeInAMethodOfItsOwn()
    {
        object? nothing = null;
        _ = nothing!.ToString();
    }
}