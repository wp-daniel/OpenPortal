namespace OpenPortal.SharedKernel.Results;

/// <summary>
/// A <see cref="Result"/> that carries a value when the operation succeeds.
/// </summary>
/// <typeparam name="TValue">Type of the produced value.</typeparam>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    private Result(bool isSuccess, Error error, TValue? value)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// The produced value. Throws when the result is a failure, because reading a value that does not
    /// exist is a programming error rather than an expected business outcome.
    /// </summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot read the value of a failed result: {Error}");

    public static Result<TValue> Success(TValue value) => new(true, Error.None, value);

    public static new Result<TValue> Failure(Error error) => new(false, error, default);
}