using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shelvd.Web.Client.Models;
using Shelvd.Web.Client.Pages;
using Shelvd.Web.Client.Services.Books;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Tests.Pages;

public class LibraryTests : BunitContext
{
    private readonly Mock<IBooksApiClient> _booksApiClient = new();

    public LibraryTests()
    {
        Services.AddSingleton(_booksApiClient.Object);
    }

    [Fact]
    public void Library_ShowsLoadingIndicator_BeforeFetchCompletes()
    {
        var tcs = new TaskCompletionSource<Result<IReadOnlyList<BookDto>, BooksApiError>>();
        _booksApiClient.Setup(c => c.GetBooksAsync(It.IsAny<CancellationToken>())).Returns(tcs.Task);

        var cut = Render<Library>();

        Assert.Contains("Loading", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Library_RendersBookTitlesAndAuthors_WhenFetchResolvesWithData()
    {
        var books = new List<BookDto>
        {
            new(Guid.NewGuid(), "9780000000000", "Test Book", ["Author One", "Author Two"], null, DateTimeOffset.UtcNow)
        };
        _booksApiClient
            .Setup(c => c.GetBooksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<BookDto>, BooksApiError>.Success(books));

        var cut = Render<Library>();

        Assert.Contains("Test Book", cut.Markup);
        Assert.Contains("Author One", cut.Markup);
        Assert.Contains("Author Two", cut.Markup);
    }

    [Fact]
    public void Library_LoadingIndicator_HasStatusRoleAndAccessibleLabel()
    {
        var tcs = new TaskCompletionSource<Result<IReadOnlyList<BookDto>, BooksApiError>>();
        _booksApiClient.Setup(c => c.GetBooksAsync(It.IsAny<CancellationToken>())).Returns(tcs.Task);

        var cut = Render<Library>();

        var status = cut.Find("[role='status']");
        Assert.Contains("Loading your books", status.TextContent);
    }

    [Fact]
    public void Library_RendersBookCards_InGridContainer()
    {
        var books = new List<BookDto>
        {
            new(Guid.NewGuid(), "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow)
        };
        _booksApiClient
            .Setup(c => c.GetBooksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<BookDto>, BooksApiError>.Success(books));

        var cut = Render<Library>();

        Assert.Contains("class=\"book-grid\"", cut.Markup);
        Assert.Contains("class=\"book-card\"", cut.Markup);
    }

    [Fact]
    public void Library_ShowsPlaceholderCover_WhenCoverImageUrlIsNull()
    {
        var books = new List<BookDto>
        {
            new(Guid.NewGuid(), "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow)
        };
        _booksApiClient
            .Setup(c => c.GetBooksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<BookDto>, BooksApiError>.Success(books));

        var cut = Render<Library>();

        var img = cut.Find("img");
        Assert.Equal("/images/placeholder-cover.webp", img.GetAttribute("src"));
    }

    [Fact]
    public void Library_ShowsActualCover_WhenCoverImageUrlIsPresent()
    {
        var books = new List<BookDto>
        {
            new(Guid.NewGuid(), "9780000000000", "Test Book", ["Author One"], "https://example.com/cover.jpg", DateTimeOffset.UtcNow)
        };
        _booksApiClient
            .Setup(c => c.GetBooksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<BookDto>, BooksApiError>.Success(books));

        var cut = Render<Library>();

        var img = cut.Find("img");
        Assert.Equal("https://example.com/cover.jpg", img.GetAttribute("src"));
    }

    [Fact]
    public void Library_RendersEmptyStateMessage_WhenFetchResolvesWithEmptyList()
    {
        _booksApiClient
            .Setup(c => c.GetBooksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<BookDto>, BooksApiError>.Success(new List<BookDto>()));

        var cut = Render<Library>();

        Assert.Contains("no books", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Library_RendersErrorMessage_WhenFetchResolvesWithFailure()
    {
        _booksApiClient
            .Setup(c => c.GetBooksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<BookDto>, BooksApiError>.Failure(BooksApiError.Unknown));

        var cut = Render<Library>();

        Assert.Contains("error", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Library_RetryButton_RefetchesAndRendersSuccess_AfterFailure()
    {
        var books = new List<BookDto>
        {
            new(Guid.NewGuid(), "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow)
        };
        _booksApiClient
            .SetupSequence(c => c.GetBooksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<BookDto>, BooksApiError>.Failure(BooksApiError.Unknown))
            .ReturnsAsync(new Result<IReadOnlyList<BookDto>, BooksApiError>.Success(books));

        var cut = Render<Library>();
        Assert.Contains("error", cut.Markup, StringComparison.OrdinalIgnoreCase);

        cut.Find("button").Click();

        Assert.Contains("Test Book", cut.Markup);
        _booksApiClient.Verify(c => c.GetBooksAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public void Library_DisposingComponent_CancelsInFlightFetch()
    {
        var tcs = new TaskCompletionSource<Result<IReadOnlyList<BookDto>, BooksApiError>>();
        CancellationToken? capturedToken = null;
        _booksApiClient
            .Setup(c => c.GetBooksAsync(It.IsAny<CancellationToken>()))
            .Callback<CancellationToken>(token => capturedToken = token)
            .Returns(tcs.Task);

        Render<Library>();
        Dispose();

        Assert.NotNull(capturedToken);
        Assert.True(capturedToken!.Value.IsCancellationRequested);
    }
}
