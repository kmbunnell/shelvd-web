using System.Linq;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shelvd.Web.Client.Models;
using Shelvd.Web.Client.Pages;
using Shelvd.Web.Client.Services.Books;
using Shelvd.Web.Client.Services.Tags;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Tests.Pages;

public class LibraryTests : BunitContext
{
    private readonly Mock<IBooksApiClient> _booksApiClient = new();
    private readonly Mock<ITagsApiClient> _tagsApiClient = new();

    public LibraryTests()
    {
        Services.AddSingleton(_booksApiClient.Object);
        Services.AddSingleton(_tagsApiClient.Object);
        _tagsApiClient
            .Setup(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Success([]));
        JSInterop.SetupModule("./Components/TagFilterPopover.razor.js")
            .SetupVoid("trapFocus", _ => true);
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
            new(Guid.NewGuid(), "9780000000000", "Test Book", ["Author One", "Author Two"], null, DateTimeOffset.UtcNow, [])
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
            new(Guid.NewGuid(), "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow, [])
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
            new(Guid.NewGuid(), "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow, [])
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
            new(Guid.NewGuid(), "9780000000000", "Test Book", ["Author One"], "https://example.com/cover.jpg", DateTimeOffset.UtcNow, [])
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
            new(Guid.NewGuid(), "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow, [])
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
    public void Library_DefaultRendersBooks_SortedByTitleAscending()
    {
        var books = new List<BookDto>
        {
            new(Guid.NewGuid(), "9780000000001", "Zebra Book", ["Author One"], null, DateTimeOffset.UtcNow, []),
            new(Guid.NewGuid(), "9780000000002", "Apple Book", ["Author Two"], null, DateTimeOffset.UtcNow, []),
            new(Guid.NewGuid(), "9780000000003", "Mango Book", ["Author Three"], null, DateTimeOffset.UtcNow, [])
        };
        _booksApiClient
            .Setup(c => c.GetBooksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<BookDto>, BooksApiError>.Success(books));

        var cut = Render<Library>();

        var titles = cut.FindAll(".book-title").Select(e => e.TextContent).ToList();
        Assert.Equal(["Apple Book", "Mango Book", "Zebra Book"], titles);
    }

    [Fact]
    public void Library_SwitchingSortToAuthor_ReSortsByFirstAuthorAscending()
    {
        var books = new List<BookDto>
        {
            new(Guid.NewGuid(), "9780000000001", "Zebra Book", ["Zed Author"], null, DateTimeOffset.UtcNow, []),
            new(Guid.NewGuid(), "9780000000002", "Apple Book", ["Amy Author"], null, DateTimeOffset.UtcNow, []),
            new(Guid.NewGuid(), "9780000000003", "Mango Book", ["Mia Author"], null, DateTimeOffset.UtcNow, [])
        };
        _booksApiClient
            .Setup(c => c.GetBooksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<BookDto>, BooksApiError>.Success(books));

        var cut = Render<Library>();
        cut.Find("#sort-select").Change("Author");

        var titles = cut.FindAll(".book-title").Select(e => e.TextContent).ToList();
        Assert.Equal(["Apple Book", "Mango Book", "Zebra Book"], titles);
    }

    [Fact]
    public void Library_FetchesTags_OnInit()
    {
        var books = new List<BookDto>
        {
            new(Guid.NewGuid(), "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow, [])
        };
        _booksApiClient
            .Setup(c => c.GetBooksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<BookDto>, BooksApiError>.Success(books));

        Render<Library>();

        _tagsApiClient.Verify(c => c.GetTagsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Library_TagIconButton_OpensPopover()
    {
        var books = new List<BookDto>
        {
            new(Guid.NewGuid(), "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow, [])
        };
        _booksApiClient
            .Setup(c => c.GetBooksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<BookDto>, BooksApiError>.Success(books));
        _tagsApiClient
            .Setup(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Success(
                [new TagDto(Guid.NewGuid(), "Fantasy", false)]));

        var cut = Render<Library>();
        Assert.Empty(cut.FindAll(".tag-filter-popover"));

        cut.Find(".tag-filter-button").Click();

        Assert.NotEmpty(cut.FindAll(".tag-filter-popover"));
        Assert.Contains("Fantasy", cut.Markup);
    }

    [Fact]
    public void Library_TagsLoadFailure_DoesNotBlockBookRendering()
    {
        var books = new List<BookDto>
        {
            new(Guid.NewGuid(), "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow, [])
        };
        _booksApiClient
            .Setup(c => c.GetBooksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<BookDto>, BooksApiError>.Success(books));
        _tagsApiClient
            .Setup(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Failure(TagsApiError.Unknown));

        var cut = Render<Library>();

        Assert.Contains("Test Book", cut.Markup);
    }

    [Fact]
    public void Library_SelectingTags_FiltersToBooksWithAllSelectedTags()
    {
        var fantasy = new TagDto(Guid.NewGuid(), "Fantasy", false);
        var favorites = new TagDto(Guid.NewGuid(), "Favorites", false);
        var scifi = new TagDto(Guid.NewGuid(), "Sci-Fi", false);
        var books = new List<BookDto>
        {
            new(Guid.NewGuid(), "9780000000001", "Both Tags Book", ["Author One"], null, DateTimeOffset.UtcNow, [fantasy, favorites]),
            new(Guid.NewGuid(), "9780000000002", "Only Fantasy Book", ["Author Two"], null, DateTimeOffset.UtcNow, [fantasy]),
            new(Guid.NewGuid(), "9780000000003", "No Matching Tags Book", ["Author Three"], null, DateTimeOffset.UtcNow, [scifi])
        };
        _booksApiClient
            .Setup(c => c.GetBooksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<BookDto>, BooksApiError>.Success(books));
        _tagsApiClient
            .Setup(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Success([fantasy, favorites, scifi]));

        var cut = Render<Library>();
        cut.Find(".tag-filter-button").Click();
        cut.FindAll("input[type='checkbox']")[0].Change(true); // Fantasy (alphabetical order)
        cut.FindAll("input[type='checkbox']")[1].Change(true); // Favorites
        cut.Find(".done-button").Click();

        var titles = cut.FindAll(".book-title").Select(e => e.TextContent).ToList();
        Assert.Equal(["Both Tags Book"], titles);
    }

    [Fact]
    public void Library_ClearingSelectedTags_RestoresFullSortedList()
    {
        var fantasy = new TagDto(Guid.NewGuid(), "Fantasy", false);
        var books = new List<BookDto>
        {
            new(Guid.NewGuid(), "9780000000001", "Zebra Book", ["Author One"], null, DateTimeOffset.UtcNow, [fantasy]),
            new(Guid.NewGuid(), "9780000000002", "Apple Book", ["Author Two"], null, DateTimeOffset.UtcNow, [])
        };
        _booksApiClient
            .Setup(c => c.GetBooksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<BookDto>, BooksApiError>.Success(books));
        _tagsApiClient
            .Setup(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Success([fantasy]));

        var cut = Render<Library>();
        cut.Find(".tag-filter-button").Click();
        cut.Find("input[type='checkbox']").Change(true);
        cut.Find(".done-button").Click();
        Assert.Equal(["Zebra Book"], cut.FindAll(".book-title").Select(e => e.TextContent));

        cut.Find(".tag-filter-button").Click();
        cut.Find(".clear-all-button").Click();
        cut.Find(".done-button").Click();

        var titles = cut.FindAll(".book-title").Select(e => e.TextContent).ToList();
        Assert.Equal(["Apple Book", "Zebra Book"], titles);
    }

    [Fact]
    public void Library_FilterAndSort_ComposeCorrectly()
    {
        var fantasy = new TagDto(Guid.NewGuid(), "Fantasy", false);
        var books = new List<BookDto>
        {
            new(Guid.NewGuid(), "9780000000001", "Zebra Book", ["Zed Author"], null, DateTimeOffset.UtcNow, [fantasy]),
            new(Guid.NewGuid(), "9780000000002", "Apple Book", ["Amy Author"], null, DateTimeOffset.UtcNow, [fantasy]),
            new(Guid.NewGuid(), "9780000000003", "No Tag Book", ["Aaa Author"], null, DateTimeOffset.UtcNow, [])
        };
        _booksApiClient
            .Setup(c => c.GetBooksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<BookDto>, BooksApiError>.Success(books));
        _tagsApiClient
            .Setup(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Success([fantasy]));

        var cut = Render<Library>();
        cut.Find("#sort-select").Change("Author");
        cut.Find(".tag-filter-button").Click();
        cut.Find("input[type='checkbox']").Change(true);
        cut.Find(".done-button").Click();

        var titles = cut.FindAll(".book-title").Select(e => e.TextContent).ToList();
        Assert.Equal(["Apple Book", "Zebra Book"], titles);
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
