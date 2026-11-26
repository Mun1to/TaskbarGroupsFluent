using System.Drawing;
using TaskbarGroups.Core;
using Xunit;

public class FlyoutClickTests
{
    [Fact]
    public void WarmClickKeepsItsLaunchPointOnAMonitorLeftOfPrimary()
    {
        Assert.True(FlyoutChannel.TryParseClick("click\tWork apps\t-1300\t996", out var group, out var point));
        Assert.Equal("Work apps", group);
        Assert.Equal(new Point(-1300, 996), point);
    }

    [Fact]
    public void EarlierClientsCanStillSendJustTheGroup()
    {
        Assert.True(FlyoutChannel.TryParseClick("click\tWork apps", out var group, out var point));
        Assert.Equal("Work apps", group);
        Assert.Null(point);
    }

    [Theory]
    [InlineData("click\tWork\t125")]
    [InlineData("click\tWork\t125\tbad")]
    [InlineData("click\t\t125\t996")]
    [InlineData("show\tWork\t125\t996")]
    public void InvalidClickDoesNotOpenAPanel(string message)
        => Assert.False(FlyoutChannel.TryParseClick(message, out _, out _));
}
