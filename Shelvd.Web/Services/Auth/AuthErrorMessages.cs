namespace Shelvd.Web.Services.Auth;

public static class AuthErrorMessages
{
    public static string For(AuthError error) => error switch
    {
        AuthError.InvalidCredentials => "Invalid email or password.",
        AuthError.EmailAlreadyInUse => "An account with this email already exists.",
        AuthError.WeakPassword => "Password is too weak. Please choose a stronger password.",
        AuthError.InvalidEmail => "Please enter a valid email address.",
        AuthError.EmailNotVerified => "Please verify your email address before signing in.",
        AuthError.EmailRateLimitExceeded => "Too many attempts. Please try again later.",
        AuthError.NetworkError => "A network error occurred. Please check your connection and try again.",
        _ => "Something went wrong. Please try again."
    };
}
