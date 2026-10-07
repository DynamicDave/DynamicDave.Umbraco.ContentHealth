using DynamicDave.Umbraco.ContentHealth.Scanning;

namespace DynamicDave.Umbraco.ContentHealth.Tests;

public class FullScannerStaleSourcesTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    [Fact]
    public void Visited_sources_are_never_stale()
    {
        var stale = FullScanner.SelectStaleSources([A, B], new HashSet<Guid> { A, B }, _ => false);

        Assert.Empty(stale);
    }

    [Fact]
    public void Unvisited_source_that_still_exists_in_the_tree_is_kept()
    {
        // Offset paging skipped it because content changed during the scan.
        var stale = FullScanner.SelectStaleSources([A, B], new HashSet<Guid> { A }, key => key == B);

        Assert.Empty(stale);
    }

    [Fact]
    public void Unvisited_source_that_no_longer_exists_is_stale()
    {
        var stale = FullScanner.SelectStaleSources([A, B, C], new HashSet<Guid> { A }, key => key == B);

        Assert.Equal([C], stale);
    }

    [Fact]
    public void Empty_input_gives_empty_result()
        => Assert.Empty(FullScanner.SelectStaleSources([], new HashSet<Guid>(), _ => false));
}
