namespace Shelvd.Web.Shared.Common;

public abstract record Result<TValue, TError>
{
    private Result() { }

    public sealed record Success(TValue Value) : Result<TValue, TError>;

    public sealed record Failure(TError Error) : Result<TValue, TError>;
}

public abstract record Result<TError>
{
    private Result() { }

    public sealed record Success : Result<TError>;

    public sealed record Failure(TError Error) : Result<TError>;
}
