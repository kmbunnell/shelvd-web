using Shelvd.Web.Services.Auth;

namespace Shelvd.Web.Tests.Services.Auth;

public class AuthErrorMessagesTests
{
    [Theory]
    [InlineData(AuthError.InvalidCredentials)]
    [InlineData(AuthError.EmailAlreadyInUse)]
    [InlineData(AuthError.WeakPassword)]
    [InlineData(AuthError.InvalidEmail)]
    [InlineData(AuthError.EmailNotVerified)]
    [InlineData(AuthError.EmailRateLimitExceeded)]
    [InlineData(AuthError.NetworkError)]
    [InlineData(AuthError.Unknown)]
    public void For_ReturnsNonEmptyDistinctMessage(AuthError error)
    {
        var message = AuthErrorMessages.For(error);

        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    [Fact]
    public void For_ReturnsDistinctMessages_ForDistinctErrors()
    {
        var messages = Enum.GetValues<AuthError>().Select(AuthErrorMessages.For).ToList();

        Assert.Equal(messages.Count, messages.Distinct().Count());
    }
}
