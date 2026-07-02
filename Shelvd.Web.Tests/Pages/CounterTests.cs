using Bunit;
using Shelvd.Web.Client.Pages;

namespace Shelvd.Web.Tests.Pages;

public class CounterTests : BunitContext
{
    [Fact]
    public void Counter_StartsAtZero()
    {
        var cut = Render<Counter>();

        cut.Find("[role=status]").TextContent.Contains("Current count: 0");
    }

    [Fact]
    public void Counter_IncrementsOnButtonClick()
    {
        var cut = Render<Counter>();

        cut.Find("button").Click();

        cut.Find("[role=status]").MarkupMatches("<p role=\"status\">Current count: 1</p>");
    }
}
