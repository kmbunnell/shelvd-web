namespace Shelvd.Web.Services.Auth;

public enum AuthError
{
    InvalidCredentials,
    EmailAlreadyInUse,
    WeakPassword,
    InvalidEmail,
    EmailNotVerified,
    EmailRateLimitExceeded,
    NetworkError,
    Unknown
}
