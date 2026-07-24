using Bunit;
using Microsoft.AspNetCore.Components;
using Shelvd.Web.Client.Components;
using Shelvd.Web.Client.Models;

namespace Shelvd.Web.Tests.Components;

public class TagFilterPopoverTests : BunitContext
{
    public TagFilterPopoverTests()
    {
        JSInterop.SetupModule("./Components/TagFilterPopover.razor.js")
            .SetupVoid("trapFocus", _ => true);
    }

    private static TagDto Tag(string name, bool isDefault = false) => new(Guid.NewGuid(), name, isDefault);

    [Fact]
    public void TagFilterPopover_RendersTagsAlphabetically()
    {
        var zebra = Tag("Zebra");
        var apple = Tag("Apple");
        var mango = Tag("Mango");

        var cut = Render<TagFilterPopover>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Status, TagFilterPopover.TagsLoadStatus.Loaded)
            .Add(p => p.Tags, [zebra, apple, mango])
            .Add(p => p.SelectedTagIds, []));

        var labels = cut.FindAll(".tag-list label").Select(e => e.TextContent.Trim()).ToList();
        Assert.Equal(["Apple", "Mango", "Zebra"], labels);
    }

    [Fact]
    public void TagFilterPopover_ClickingCheckbox_TogglesSelection()
    {
        var tag = Tag("Fantasy");

        var cut = Render<TagFilterPopover>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Status, TagFilterPopover.TagsLoadStatus.Loaded)
            .Add(p => p.Tags, [tag])
            .Add(p => p.SelectedTagIds, []));

        var checkbox = cut.Find("input[type='checkbox']");
        Assert.False(checkbox.HasAttribute("checked"));

        checkbox.Change(true);

        checkbox = cut.Find("input[type='checkbox']");
        Assert.True(checkbox.HasAttribute("checked"));
    }

    [Fact]
    public void TagFilterPopover_ClearAll_DeselectsSelection_ButLeavesPopoverOpen()
    {
        var tag = Tag("Fantasy");

        var cut = Render<TagFilterPopover>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Status, TagFilterPopover.TagsLoadStatus.Loaded)
            .Add(p => p.Tags, [tag])
            .Add(p => p.SelectedTagIds, [tag.Id]));

        Assert.True(cut.Find("input[type='checkbox']").HasAttribute("checked"));

        cut.Find(".clear-all-button").Click();

        Assert.False(cut.Find("input[type='checkbox']").HasAttribute("checked"));
        Assert.NotEmpty(cut.FindAll(".tag-filter-popover"));
    }

    [Fact]
    public void TagFilterPopover_TogglingCheckbox_InvokesSelectionChangedImmediately()
    {
        var fantasy = Tag("Fantasy");
        var scifi = Tag("Sci-Fi");
        IReadOnlyList<Guid>? invokedWith = null;

        var cut = Render<TagFilterPopover>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Status, TagFilterPopover.TagsLoadStatus.Loaded)
            .Add(p => p.Tags, [fantasy, scifi])
            .Add(p => p.SelectedTagIds, [])
            .Add(p => p.OnSelectionChanged, EventCallback.Factory.Create<IReadOnlyList<Guid>>(this, selection => invokedWith = selection)));

        cut.FindAll("input[type='checkbox']")[0].Change(true);

        Assert.NotNull(invokedWith);
        Assert.Equal([fantasy.Id], invokedWith);
    }

    [Fact]
    public void TagFilterPopover_ClearAll_InvokesSelectionChangedWithEmptySelection()
    {
        var fantasy = Tag("Fantasy");
        IReadOnlyList<Guid>? invokedWith = null;

        var cut = Render<TagFilterPopover>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Status, TagFilterPopover.TagsLoadStatus.Loaded)
            .Add(p => p.Tags, [fantasy])
            .Add(p => p.SelectedTagIds, [fantasy.Id])
            .Add(p => p.OnSelectionChanged, EventCallback.Factory.Create<IReadOnlyList<Guid>>(this, selection => invokedWith = selection)));

        cut.Find(".clear-all-button").Click();

        Assert.NotNull(invokedWith);
        Assert.Empty(invokedWith);
    }

    [Fact]
    public void TagFilterPopover_Done_ClosesWithoutSelectionPayload()
    {
        var fantasy = Tag("Fantasy");
        var doneInvoked = false;

        var cut = Render<TagFilterPopover>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Status, TagFilterPopover.TagsLoadStatus.Loaded)
            .Add(p => p.Tags, [fantasy])
            .Add(p => p.SelectedTagIds, [])
            .Add(p => p.OnDone, EventCallback.Factory.Create(this, () => doneInvoked = true)));

        cut.FindAll("input[type='checkbox']")[0].Change(true);
        cut.Find(".done-button").Click();

        Assert.True(doneInvoked);
    }

    [Fact]
    public void TagFilterPopover_Loading_RendersLoadingState()
    {
        var cut = Render<TagFilterPopover>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Status, TagFilterPopover.TagsLoadStatus.Loading)
            .Add(p => p.Tags, [])
            .Add(p => p.SelectedTagIds, []));

        Assert.Contains("loading", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.True(cut.Find(".done-button").HasAttribute("disabled"));
    }

    [Fact]
    public void TagFilterPopover_Failed_RendersErrorStateWithRetry()
    {
        var retried = false;

        var cut = Render<TagFilterPopover>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Status, TagFilterPopover.TagsLoadStatus.Failed)
            .Add(p => p.Tags, [])
            .Add(p => p.SelectedTagIds, [])
            .Add(p => p.OnRetry, EventCallback.Factory.Create(this, () => retried = true)));

        Assert.Contains("couldn't load tags", cut.Markup, StringComparison.OrdinalIgnoreCase);

        cut.Find(".retry-button").Click();

        Assert.True(retried);
    }

    [Fact]
    public void TagFilterPopover_RendersNothing_WhenClosed()
    {
        var cut = Render<TagFilterPopover>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.Tags, [])
            .Add(p => p.SelectedTagIds, []));

        Assert.Empty(cut.Markup.Trim());
    }
}
