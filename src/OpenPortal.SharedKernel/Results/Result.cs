namespace OpenPortal.SharedKernel.Results;

/// <summary>
/// The outcome of an operation that can fail for an expected, non-exceptional reason.
/// Expected failures are returned as values so that callers are forced by the type system to handle them,
/// and so that the host layer can map them to a specific status code without exception inspection.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && !error.IsNone)
        {
            throw new ArgumentException("A successful result cannot carry an error.", nameof(error));
        }

        if (!isSuccess && error.IsNone)
        {
            throw new ArgumentException("A failed result must carry an error.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    /// <summary>The failure detail. <see cref="Error.None"/> when <see cref="IsSuccess"/> is true.</summary>
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);
}