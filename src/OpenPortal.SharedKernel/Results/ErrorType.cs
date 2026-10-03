namespace OpenPortal.SharedKernel.Results;

/// <summary>
/// Classification of a failure, used by the host layer to translate a <see cref="Result"/> into the
/// correct HTTP status code and by the React client to select the right user-facing behaviour.
/// </summary>
public enum ErrorType
{
    /// <summary>An unexpected operation failure with no more specific classification.</summary>
    Failure = 0,

    /// <summary>The caller supplied input that violates a documented rule.</summary>
    Validation,

    /// <summary>The requested resource does not exist, or must not be revealed to exist.</summary>
    NotFound,

    /// <summary>The request conflicts with the current state of the resource.</summary>
    Conflict,

    /// <summary>No authenticated session was established.</summary>
    Unauthorized,

    /// <summary>An authenticated session exists but lacks the required permission.</summary>
    Forbidden,
}