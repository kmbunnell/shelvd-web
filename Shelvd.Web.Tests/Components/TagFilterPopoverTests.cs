using Bunit;
using Microsoft.AspNetCore.Components;
using Shelvd.Web.Client.Components;
using Shelvd.Web.Client.Models;

namespace Shelvd.Web.Tests.Components;

public class TagFilterPopoverTests : BunitContext
{
    private static TagDto Tag(string name, bool isDefault = false) => new(Guid.NewGuid(), name, isDefault);

    [Fact]
    public void TagFilterPopover_RendersTagsAlphabetically()
    {
        var zebra = Tag("Zebra");
        var apple = Tag("Apple");
        var mango = Tag("Mango");

        var cut = Render<TagFilterPopover>(parameters => parameters
            .Add(p => p.IsOpen, true)
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
            .Add(p => p.Tags, [tag])
            .Add(p => p.SelectedTagIds, [tag.Id]));

        Assert.True(cut.Find("input[type='checkbox']").HasAttribute("checked"));

        cut.Find(".clear-all-button").Click();

        Assert.False(cut.Find("input[type='checkbox']").HasAttribute("checked"));
        Assert.NotEmpty(cut.FindAll(".tag-filter-popover"));
    }

    [Fact]
    public void TagFilterPopover_Done_InvokesCallbackWithCurrentSelection()
    {
        var fantasy = Tag("Fantasy");
        var scifi = Tag("Sci-Fi");
        IReadOnlyList<Guid>? invokedWith = null;

        var cut = Render<TagFilterPopover>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Tags, [fantasy, scifi])
            .Add(p => p.SelectedTagIds, [])
            .Add(p => p.OnDone, EventCallback.Factory.Create<IReadOnlyList<Guid>>(this, selection => invokedWith = selection)));

        cut.FindAll("input[type='checkbox']")[0].Change(true);
        cut.Find(".done-button").Click();

        Assert.NotNull(invokedWith);
        Assert.Equal([fantasy.Id], invokedWith);
    }

    [Fact]
    public void TagFilterPopover_EmptyTagList_RendersEmptyState()
    {
        var cut = Render<TagFilterPopover>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Tags, [])
            .Add(p => p.SelectedTagIds, []));

        Assert.Contains("no tags", cut.Markup, StringComparison.OrdinalIgnoreCase);
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
