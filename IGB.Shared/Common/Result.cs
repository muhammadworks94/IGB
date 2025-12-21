namespace IGB.Shared.Common;

public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; private set; }
    public string? Error { get; private set; }
    public List<string>? Errors { get; private set; }

    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
        Error = null;
        Errors = null;
    }

    private Result(string error)
    {
        IsSuccess = false;
        Value = default;
        Error = error;
        Errors = null;
    }

    private Result(List<string> errors)
    {
        IsSuccess = false;
        Value = default;
        Error = null;
        Errors = errors;
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string error) => new(error);
    public static Result<T> Failure(List<string> errors) => new(errors);
}

public class Result
{
    public bool IsSuccess { get; private set; }
    public bool IsFailure => !IsSuccess;
    public string? Error { get; private set; }
    public List<string>? Errors { get; private set; }

    private Result(bool isSuccess, string? error = null, List<string>? errors = null)
    {
        IsSuccess = isSuccess;
        Error = error;
        Errors = errors;
    }

    public static Result Success() => new(true);
    public static Result Failure(string error) => new(false, error);
    public static Result Failure(List<string> errors) => new(false, null, errors);
}

