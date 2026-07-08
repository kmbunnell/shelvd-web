using Shelvd.Web.Services.Auth;
using Supabase.Gotrue.Exceptions;

namespace Shelvd.Web.Tests.Services.Auth;

public class GotrueExceptionMapperTests
{
    [Theory]
    [InlineData(FailureHint.Reason.UserBadLogin, AuthError.InvalidCredentials)]
    [InlineData(FailureHint.Reason.UserBadMultiple, AuthError.InvalidCredentials)]
    [InlineData(FailureHint.Reason.UserAlreadyRegistered, AuthError.EmailAlreadyInUse)]
    [InlineData(FailureHint.Reason.UserBadPassword, AuthError.WeakPassword)]
    [InlineData(FailureHint.Reason.UserBadEmailAddress, AuthError.InvalidEmail)]
    [InlineData(FailureHint.Reason.UserEmailNotConfirmed, AuthError.EmailNotVerified)]
    [InlineData(FailureHint.Reason.UserTooManyRequests, AuthError.EmailRateLimitExceeded)]
    [InlineData(FailureHint.Reason.Offline, AuthError.NetworkError)]
    [InlineData(FailureHint.Reason.Unknown, AuthError.Unknown)]
    [InlineData(FailureHint.Reason.InvalidRefreshToken, AuthError.Unknown)]
    [InlineData(FailureHint.Reason.ExpiredRefreshToken, AuthError.Unknown)]
    [InlineData(FailureHint.Reason.AdminTokenRequired, AuthError.Unknown)]
    [InlineData(FailureHint.Reason.NoSessionFound, AuthError.Unknown)]
    [InlineData(FailureHint.Reason.BadSessionUrl, AuthError.Unknown)]
    [InlineData(FailureHint.Reason.InvalidFlowType, AuthError.Unknown)]
    [InlineData(FailureHint.Reason.SsoDomainNotFound, AuthError.Unknown)]
    [InlineData(FailureHint.Reason.SsoProviderNotFound, AuthError.Unknown)]
    [InlineData(FailureHint.Reason.UserMissingInformation, AuthError.Unknown)]
    [InlineData(FailureHint.Reason.UserBadPhoneNumber, AuthError.Unknown)]
    public void Map_ReturnsExpectedAuthError_ForReason(FailureHint.Reason reason, AuthError expected)
    {
        var exception = new GotrueException("test", reason);

        var result = GotrueExceptionMapper.Map(exception);

        Assert.Equal(expected, result);
    }
}
