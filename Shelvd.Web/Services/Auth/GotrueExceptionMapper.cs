using Supabase.Gotrue.Exceptions;

namespace Shelvd.Web.Services.Auth;

public static class GotrueExceptionMapper
{
    // FailureHint.Reason has no dedicated "weak password" case; UserBadPassword is Gotrue's
    // best-effort reason for a password that was rejected as invalid (e.g. too weak on sign-up),
    // while UserBadLogin/UserBadMultiple indicate a wrong email/password combination on sign-in.
    public static AuthError Map(GotrueException exception) => exception.Reason switch
    {
        FailureHint.Reason.UserBadLogin => AuthError.InvalidCredentials,
        FailureHint.Reason.UserBadMultiple => AuthError.InvalidCredentials,
        FailureHint.Reason.UserAlreadyRegistered => AuthError.EmailAlreadyInUse,
        FailureHint.Reason.UserBadPassword => AuthError.WeakPassword,
        FailureHint.Reason.UserBadEmailAddress => AuthError.InvalidEmail,
        FailureHint.Reason.UserEmailNotConfirmed => AuthError.EmailNotVerified,
        FailureHint.Reason.UserTooManyRequests => AuthError.EmailRateLimitExceeded,
        FailureHint.Reason.Offline => AuthError.NetworkError,
        _ => AuthError.Unknown
    };
}
