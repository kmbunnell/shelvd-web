using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shelvd.Web.Client.Models;
using Shelvd.Web.Client.Pages;
using Shelvd.Web.Client.Services.BookTags;
using Shelvd.Web.Client.Services.Books;
using Shelvd.Web.Client.Services.Tags;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Tests.Pages;

public class BookDetailsTests : BunitContext
{
    private readonly Mock<IBooksApiClient> _booksApiClient = new();
    private readonly Mock<ITagsCache> _tagsCache = new();
    private readonly Mock<IBookTagsApiClient> _bookTagsApiClient = new();
    private readonly Guid _bookId = Guid.NewGuid();

    public BookDetailsTests()
    {
        Services.AddSingleton(_booksApiClient.Object);
        Services.AddSingleton(_tagsCache.Object);
        Services.AddSingleton(_bookTagsApiClient.Object);
        _tagsCache
            .Setup(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Success([]));
    }

    private IRenderedComponent<BookDetails> RenderBookDetails() =>
        Render<BookDetails>(parameters => parameters.Add(p => p.Id, _bookId));

    [Fact]
    public void BookDetails_ShowsLoadingIndicator_BeforeFetchCompletes()
    {
        var tcs = new TaskCompletionSource<Result<BookDto, BooksApiError>>();
        _booksApiClient.Setup(c => c.GetBookAsync(_bookId, It.IsAny<CancellationToken>())).Returns(tcs.Task);

        var cut = RenderBookDetails();

        Assert.Contains("Loading", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BookDetails_RendersTitleAndCover_WhenFetchResolvesWithData()
    {
        var book = new BookDto(_bookId, "9780000000000", "Test Book", ["Author One"], "https://example.com/cover.jpg", DateTimeOffset.UtcNow, []);
        _booksApiClient
            .Setup(c => c.GetBookAsync(_bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<BookDto, BooksApiError>.Success(book));

        var cut = RenderBookDetails();

        var heading = cut.Find("h1");
        Assert.Equal("Tag Book", heading.TextContent);
        var img = cut.Find("img");
        Assert.Equal("https://example.com/cover.jpg", img.GetAttribute("src"));
        Assert.Equal("Cover of Test Book", img.GetAttribute("alt"));
    }

    [Fact]
    public void BookDetails_ShowsPlaceholderCover_WhenCoverImageUrlIsNull()
    {
        var book = new BookDto(_bookId, "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow, []);
        _booksApiClient
            .Setup(c => c.GetBookAsync(_bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<BookDto, BooksApiError>.Success(book));

        var cut = RenderBookDetails();

        var img = cut.Find("img");
        Assert.Equal("/images/placeholder-cover.webp", img.GetAttribute("src"));
    }

    [Fact]
    public void BookDetails_RendersOneTagElementPerAvailableTag_WithTaggedOnesHighlighted()
    {
        var fantasyTag = new TagDto(Guid.NewGuid(), "Fantasy", false);
        var favoritesTag = new TagDto(Guid.NewGuid(), "Favorites", true);
        var mysteryTag = new TagDto(Guid.NewGuid(), "Mystery", false);
        var book = new BookDto(_bookId, "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow, [fantasyTag]);
        _booksApiClient
            .Setup(c => c.GetBookAsync(_bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<BookDto, BooksApiError>.Success(book));
        _tagsCache
            .Setup(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Success([fantasyTag, favoritesTag, mysteryTag]));

        var cut = RenderBookDetails();

        var tagElements = cut.FindAll(".tag-chip");
        Assert.Equal(3, tagElements.Count);
        Assert.Contains(tagElements, e => e.TextContent == "Fantasy" && e.ClassList.Contains("tag-chip-selected"));
        Assert.Contains(tagElements, e => e.TextContent == "Favorites" && !e.ClassList.Contains("tag-chip-selected"));
        Assert.Contains(tagElements, e => e.TextContent == "Mystery" && !e.ClassList.Contains("tag-chip-selected"));
    }

    [Fact]
    public void BookDetails_ClickingUntaggedChip_AddsSelectedClassAndCallsTagBookAsync()
    {
        var fantasyTag = new TagDto(Guid.NewGuid(), "Fantasy", false);
        var book = new BookDto(_bookId, "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow, []);
        _booksApiClient
            .Setup(c => c.GetBookAsync(_bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<BookDto, BooksApiError>.Success(book));
        _tagsCache
            .Setup(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Success([fantasyTag]));
        _bookTagsApiClient
            .Setup(c => c.TagBookAsync(_bookId, fantasyTag.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<BookTagsApiError>.Success());

        var cut = RenderBookDetails();
        cut.Find(".tag-chip").Click();

        Assert.Contains(cut.FindAll(".tag-chip"), e => e.ClassList.Contains("tag-chip-selected"));
        _bookTagsApiClient.Verify(c => c.TagBookAsync(_bookId, fantasyTag.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void BookDetails_ClickingTaggedChip_RemovesSelectedClassAndCallsUntagBookAsync()
    {
        var fantasyTag = new TagDto(Guid.NewGuid(), "Fantasy", false);
        var book = new BookDto(_bookId, "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow, [fantasyTag]);
        _booksApiClient
            .Setup(c => c.GetBookAsync(_bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<BookDto, BooksApiError>.Success(book));
        _tagsCache
            .Setup(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Success([fantasyTag]));
        _bookTagsApiClient
            .Setup(c => c.UntagBookAsync(_bookId, fantasyTag.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<BookTagsApiError>.Success());

        var cut = RenderBookDetails();
        cut.Find(".tag-chip").Click();

        Assert.DoesNotContain(cut.FindAll(".tag-chip"), e => e.ClassList.Contains("tag-chip-selected"));
        _bookTagsApiClient.Verify(c => c.UntagBookAsync(_bookId, fantasyTag.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void BookDetails_FailedToggle_RevertsChipAndShowsInlineError()
    {
        var fantasyTag = new TagDto(Guid.NewGuid(), "Fantasy", false);
        var book = new BookDto(_bookId, "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow, []);
        _booksApiClient
            .Setup(c => c.GetBookAsync(_bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<BookDto, BooksApiError>.Success(book));
        _tagsCache
            .Setup(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Success([fantasyTag]));
        _bookTagsApiClient
            .Setup(c => c.TagBookAsync(_bookId, fantasyTag.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<BookTagsApiError>.Failure(BookTagsApiError.Unknown));

        var cut = RenderBookDetails();
        cut.Find(".tag-chip").Click();

        Assert.DoesNotContain(cut.FindAll(".tag-chip"), e => e.ClassList.Contains("tag-chip-selected"));
        Assert.NotEmpty(cut.FindAll(".tag-chip-error"));
    }

    [Fact]
    public void BookDetails_ChipStaysPendingUntilResponse_WhileOtherChipsRemainClickable()
    {
        var fantasyTag = new TagDto(Guid.NewGuid(), "Fantasy", false);
        var mysteryTag = new TagDto(Guid.NewGuid(), "Mystery", false);
        var book = new BookDto(_bookId, "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow, []);
        _booksApiClient
            .Setup(c => c.GetBookAsync(_bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<BookDto, BooksApiError>.Success(book));
        _tagsCache
            .Setup(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Success([fantasyTag, mysteryTag]));
        var tcs = new TaskCompletionSource<Result<BookTagsApiError>>();
        _bookTagsApiClient
            .Setup(c => c.TagBookAsync(_bookId, fantasyTag.Id, It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);
        _bookTagsApiClient
            .Setup(c => c.TagBookAsync(_bookId, mysteryTag.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<BookTagsApiError>.Success());

        var cut = RenderBookDetails();

        cut.FindAll(".tag-chip").Single(e => e.TextContent == "Fantasy").Click();
        Assert.Contains(cut.FindAll(".tag-chip"), e => e.TextContent == "Fantasy" && e.ClassList.Contains("tag-chip-pending"));

        cut.FindAll(".tag-chip").Single(e => e.TextContent == "Mystery").Click();
        Assert.Contains(cut.FindAll(".tag-chip"), e => e.TextContent == "Mystery" && e.ClassList.Contains("tag-chip-selected"));

        cut.FindAll(".tag-chip").Single(e => e.TextContent == "Fantasy").Click();
        _bookTagsApiClient.Verify(c => c.TagBookAsync(_bookId, fantasyTag.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void BookDetails_FailedToggle_DoesNotClobberConcurrentSuccessfulToggleOfAnotherTag()
    {
        var fantasyTag = new TagDto(Guid.NewGuid(), "Fantasy", false);
        var mysteryTag = new TagDto(Guid.NewGuid(), "Mystery", false);
        var book = new BookDto(_bookId, "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow, []);
        _booksApiClient
            .Setup(c => c.GetBookAsync(_bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<BookDto, BooksApiError>.Success(book));
        _tagsCache
            .Setup(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Success([fantasyTag, mysteryTag]));
        var tcs = new TaskCompletionSource<Result<BookTagsApiError>>();
        _bookTagsApiClient
            .Setup(c => c.TagBookAsync(_bookId, fantasyTag.Id, It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);
        _bookTagsApiClient
            .Setup(c => c.TagBookAsync(_bookId, mysteryTag.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<BookTagsApiError>.Success());

        var cut = RenderBookDetails();

        cut.FindAll(".tag-chip").Single(e => e.TextContent == "Fantasy").Click();
        cut.FindAll(".tag-chip").Single(e => e.TextContent == "Mystery").Click();
        Assert.Contains(cut.FindAll(".tag-chip"), e => e.TextContent == "Mystery" && e.ClassList.Contains("tag-chip-selected"));

        tcs.SetResult(new Result<BookTagsApiError>.Failure(BookTagsApiError.Unknown));
        cut.Render();

        Assert.DoesNotContain(cut.FindAll(".tag-chip"), e => e.TextContent == "Fantasy" && e.ClassList.Contains("tag-chip-selected"));
        Assert.Contains(cut.FindAll(".tag-chip"), e => e.TextContent == "Mystery" && e.ClassList.Contains("tag-chip-selected"));
    }

    [Fact]
    public void BookDetails_NavigatingToAnotherBook_WhilePendingToggleInFlight_DoesNotCorruptNewBooksTags()
    {
        var fantasyTag = new TagDto(Guid.NewGuid(), "Fantasy", false);
        var otherBookId = Guid.NewGuid();
        var bookA = new BookDto(_bookId, "9780000000000", "Book A", ["Author One"], null, DateTimeOffset.UtcNow, []);
        var bookB = new BookDto(otherBookId, "9780000000001", "Book B", ["Author Two"], null, DateTimeOffset.UtcNow, []);
        _booksApiClient
            .Setup(c => c.GetBookAsync(_bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<BookDto, BooksApiError>.Success(bookA));
        _booksApiClient
            .Setup(c => c.GetBookAsync(otherBookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<BookDto, BooksApiError>.Success(bookB));
        _tagsCache
            .Setup(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Success([fantasyTag]));
        var tcs = new TaskCompletionSource<Result<BookTagsApiError>>();
        _bookTagsApiClient
            .Setup(c => c.TagBookAsync(_bookId, fantasyTag.Id, It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        var cut = RenderBookDetails();
        cut.Find(".tag-chip").Click();
        Assert.Contains(cut.FindAll(".tag-chip"), e => e.ClassList.Contains("tag-chip-selected"));

        cut.Render(parameters => parameters.Add(p => p.Id, otherBookId));
        Assert.Contains("Book B", cut.Markup);

        tcs.SetResult(new Result<BookTagsApiError>.Success());
        cut.Render();

        Assert.Contains("Book B", cut.Markup);
        Assert.DoesNotContain(cut.FindAll(".tag-chip"), e => e.ClassList.Contains("tag-chip-selected"));
    }

    [Fact]
    public void BookDetails_RendersRetryButton_WhenTagsFailToLoad()
    {
        var book = new BookDto(_bookId, "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow, []);
        _booksApiClient
            .Setup(c => c.GetBookAsync(_bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<BookDto, BooksApiError>.Success(book));
        _tagsCache
            .Setup(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Failure(TagsApiError.Unknown));

        var cut = RenderBookDetails();

        Assert.Contains("Couldn't load tags", cut.Markup);
        Assert.Empty(cut.FindAll(".tag-chip"));
    }

    [Fact]
    public void BookDetails_RetryButton_RefetchesTags_AfterTagsLoadFailure()
    {
        var book = new BookDto(_bookId, "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow, []);
        var fantasyTag = new TagDto(Guid.NewGuid(), "Fantasy", false);
        _booksApiClient
            .Setup(c => c.GetBookAsync(_bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<BookDto, BooksApiError>.Success(book));
        _tagsCache
            .SetupSequence(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Failure(TagsApiError.Unknown))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Success([fantasyTag]));

        var cut = RenderBookDetails();
        cut.Find("button.retry-button").Click();

        var tagElements = cut.FindAll(".tag-chip");
        Assert.Single(tagElements);
        Assert.Equal("Fantasy", tagElements[0].TextContent);
    }

    [Fact]
    public void BookDetails_RendersManageTagsButton_WithNoClickHandler()
    {
        var book = new BookDto(_bookId, "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow, []);
        _booksApiClient
            .Setup(c => c.GetBookAsync(_bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<BookDto, BooksApiError>.Success(book));

        var cut = RenderBookDetails();

        var button = cut.Find("button.manage-tags-button");
        Assert.Equal("Manage Tags", button.TextContent);
        Assert.Throws<MissingEventHandlerException>(() => button.Click());
    }

    [Fact]
    public void BookDetails_RendersNotFoundMessage_WhenBookIsNotFound()
    {
        _booksApiClient
            .Setup(c => c.GetBookAsync(_bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<BookDto, BooksApiError>.Failure(BooksApiError.NotFound));

        var cut = RenderBookDetails();

        Assert.Contains("not found", cut.Markup, StringComparison.OrdinalIgnoreCase);
        var link = cut.Find("a.back-home-link");
        Assert.Equal("/", link.GetAttribute("href"));
    }

    [Fact]
    public void BookDetails_RendersErrorMessage_WhenFetchResolvesWithOtherFailure()
    {
        _booksApiClient
            .Setup(c => c.GetBookAsync(_bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<BookDto, BooksApiError>.Failure(BooksApiError.Unknown));

        var cut = RenderBookDetails();

        Assert.Contains("error", cut.Markup, StringComparison.OrdinalIgnoreCase);
        cut.Find("button.retry-button");
    }

    [Fact]
    public void BookDetails_RetryButton_RefetchesAndRendersSuccess_AfterFailure()
    {
        var book = new BookDto(_bookId, "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow, []);
        _booksApiClient
            .SetupSequence(c => c.GetBookAsync(_bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<BookDto, BooksApiError>.Failure(BooksApiError.Unknown))
            .ReturnsAsync(new Result<BookDto, BooksApiError>.Success(book));

        var cut = RenderBookDetails();
        Assert.Contains("error", cut.Markup, StringComparison.OrdinalIgnoreCase);

        cut.Find("button.retry-button").Click();

        Assert.Contains("Test Book", cut.Markup);
        _booksApiClient.Verify(c => c.GetBookAsync(_bookId, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public void BookDetails_BackButton_NavigatesToRoot()
    {
        var book = new BookDto(_bookId, "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow, []);
        _booksApiClient
            .Setup(c => c.GetBookAsync(_bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<BookDto, BooksApiError>.Success(book));
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo("books/other-path");

        var cut = RenderBookDetails();
        cut.Find("a.back-button").Click();

        Assert.Equal(navigation.BaseUri, navigation.Uri);
    }

    [Fact]
    public void BookDetails_RendersDeleteButton_WithNoClickHandler()
    {
        var book = new BookDto(_bookId, "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow, []);
        _booksApiClient
            .Setup(c => c.GetBookAsync(_bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<BookDto, BooksApiError>.Success(book));

        var cut = RenderBookDetails();

        var button = cut.Find("button.delete-button");
        Assert.Equal("Delete book from library", button.GetAttribute("aria-label"));
        Assert.Throws<MissingEventHandlerException>(() => button.Click());
    }
}
