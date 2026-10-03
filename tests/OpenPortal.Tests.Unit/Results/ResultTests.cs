using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Tests.Unit.Results;

public sealed class ResultTests
{
    private static readonly Error Sample = Error.NotFound("test.not_found", "Not found.");

    [Fact]
    public void Success_carries_no_error()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Error.ShouldBe(Error.None);
    }

    [Fact]
    public void Failure_carries_its_error()
    {
        var result = Result.Failure(Sample);

        result.IsFailure.ShouldBeTrue();
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(Sample);
    }

    [Fact]
    public void Success_of_T_exposes_the_value()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        result.Error.ShouldBe(Error.None);
    }

    [Fact]
    public void Failure_of_T_reports_an_invalid_value()
    {
        var result = Result<int>.Failure(Sample);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(Sample);
        // Reading Value on a failure must not silently hand back a default: a caller that forgot to check
        // IsSuccess gets an exception instead of a plausible-looking zero.
        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Error_None_reports_itself_as_absent()
    {
        Error.None.IsNone.ShouldBeTrue();
        Error.None.Code.ShouldBe(string.Empty);
        Error.None.ToString().ShouldBe("Error.None");
    }

    [Fact]
    public void Errors_compare_by_code_type_and_description()
    {
        Error.Failure("a", "One.").ShouldBe(Error.Failure("a", "One."));

        // Two errors with the same code but different types map to different status codes, so they must
        // not compare equal even though a client would branch on the same code.
        Error.Failure("a", "One.")
            .ShouldNotBe(Error.Conflict("a", "One."), customMessage: "The type drives the HTTP status code.");

        Error.Failure("a", "One.").ShouldNotBe(Error.Failure("a", "Two."));
    }

    [Fact]
    public void Error_factories_set_the_matching_type()
    {
        Error.Failure("c", "d").Type.ShouldBe(ErrorType.Failure);
        Error.Validation("c", "d").Type.ShouldBe(ErrorType.Validation);
        Error.NotFound("c", "d").Type.ShouldBe(ErrorType.NotFound);
        Error.Conflict("c", "d").Type.ShouldBe(ErrorType.Conflict);
        Error.Unauthorized("c", "d").Type.ShouldBe(ErrorType.Unauthorized);
        Error.Forbidden("c", "d").Type.ShouldBe(ErrorType.Forbidden);
    }

    [Fact]
    public void Error_describes_itself_for_logs()
    {
        Error.Validation("content.slug_invalid", "The slug is not valid.").ToString()
            .ShouldBe("content.slug_invalid (Validation): The slug is not valid.");
    }
}