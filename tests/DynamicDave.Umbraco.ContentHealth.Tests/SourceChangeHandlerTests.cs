using DynamicDave.Umbraco.ContentHealth.Notifications;

namespace DynamicDave.Umbraco.ContentHealth.Tests;

public class SourceChangeHandlerTests
{
    [Theory]
    [InlineData("-1,-20,1050")]
    [InlineData("-1,-20,1050,1060")]
    public void Path_in_the_content_recycle_bin_was_in_the_recycle_bin(string path)
        => Assert.True(SourceChangeHandler.WasInRecycleBin(path));

    [Theory]
    [InlineData("-1,1050")]
    [InlineData("-1,1050,1060")]
    [InlineData("-1,-21,1050")] // media recycle bin
    [InlineData("-1,-200,1050")] // not a segment match
    [InlineData("")]
    public void Path_in_the_tree_was_not_in_the_recycle_bin(string path)
        => Assert.False(SourceChangeHandler.WasInRecycleBin(path));
}
