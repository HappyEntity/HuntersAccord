using HuntersAccord.Core;

namespace HuntersAccord.Core.Tests;

public class CoopCompatTests
{
    private sealed record Found(string Who, int Number, string Version, string Word = "");

    [Fact]
    public void The_build_has_a_number_and_a_version()
    {
        Assert.True(CoopCompat.Number > CoopCompat.Unknown);
        Assert.Matches(@"^\d+\.\d+\.\d+$", CoopCompat.ModVersion);
    }

    [Fact]
    public void Only_the_same_number_plays_together_and_none_never_does()
    {
        Assert.True(CoopCompat.Compatible(1, 1));
        Assert.False(CoopCompat.Compatible(1, 2));
        Assert.False(CoopCompat.Compatible(2, 1));
        Assert.False(CoopCompat.Compatible(1, CoopCompat.Unknown));
        Assert.False(CoopCompat.Compatible(CoopCompat.Unknown, CoopCompat.Unknown));
    }

    [Fact]
    public void The_higher_number_is_the_newer_mod_and_none_the_oldest()
    {
        Assert.Equal(CoopCompat.Side.TheyNewer, CoopCompat.Compare(1, 2));
        Assert.Equal(CoopCompat.Side.TheyOlder, CoopCompat.Compare(2, 1));
        Assert.Equal(CoopCompat.Side.TheyOlder, CoopCompat.Compare(1, CoopCompat.Unknown));
        Assert.Equal(CoopCompat.Side.Same, CoopCompat.Compare(3, 3));
    }

    [Fact]
    public void Labels_are_the_versions_with_the_numbers_when_they_read_the_same()
    {
        Assert.Equal(("0.2.0", "0.3.0"), CoopCompat.Labels(1, "0.2.0", 2, "0.3.0"));
        Assert.Equal(("0.2.0 #1", "0.2.0 #2"), CoopCompat.Labels(1, "0.2.0", 2, "0.2.0"));
        Assert.Equal(("0.2.0", "?"), CoopCompat.Labels(1, "0.2.0", CoopCompat.Unknown, ""));
    }

    [Theory]
    [InlineData("0.2.0", "0.2.0")]
    [InlineData("0.3.0-beta+7", "0.3.0-beta+7")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("0.2|0", "")]
    [InlineData("<b>9</b>", "")]
    [InlineData("0.123456789012345678901234", "")]
    public void A_version_told_is_kept_only_when_it_reads_as_one(string? told, string kept) =>
        Assert.Equal(kept, CoopCompat.CleanVersion(told));

    [Fact]
    public void A_search_keeps_its_number_and_tells_nothing_when_one_of_it_is_there()
    {
        Found[] found = [new("a", 1, "0.2.0", "w"), new("b", 2, "0.3.0", "w"), new("c", 0, "", "w")];

        var (taken, other) = CoopCompat.Split(found, 1, f => f.Number, f => f.Version, f => f.Word == "w");

        Assert.Equal(["a"], taken.Select(f => f.Who));
        Assert.Null(other);
    }

    [Fact]
    public void A_search_of_other_numbers_only_tells_the_newest_of_them()
    {
        Found[] found = [new("old", 0, "", "w"), new("newer", 3, "0.4.0", "w"), new("new", 2, "0.3.0", "w")];

        var (taken, other) = CoopCompat.Split(found, 1, f => f.Number, f => f.Version, f => f.Word == "w");

        Assert.Empty(taken);
        Assert.Equal((3, "0.4.0"), other);
    }

    [Fact]
    public void A_search_tells_only_of_the_relevant_ones()
    {
        // Another password's sign of another number is nobody this player looked for; one of its number with another
        // password is not offered either (the game asks by password), and says nothing.
        Found[] found = [new("elsewhere", 2, "0.3.0", "x"), new("mine", 1, "0.2.0", "x")];

        var (taken, other) = CoopCompat.Split(found, 1, f => f.Number, f => f.Version, f => f.Word == "w");

        Assert.Equal(["mine"], taken.Select(f => f.Who));
        Assert.Null(other);
    }

    [Fact]
    public void An_older_mod_without_a_version_is_told_as_one()
    {
        Found[] found = [new("old", CoopCompat.Unknown, "")];

        var (taken, other) = CoopCompat.Split(found, 1, f => f.Number, f => f.Version, _ => true);

        Assert.Empty(taken);
        Assert.Equal((CoopCompat.Unknown, ""), other);
        Assert.Equal(CoopCompat.Side.TheyOlder, CoopCompat.Compare(1, other!.Value.Number));
    }

    [Fact]
    public void A_bad_version_in_a_search_is_not_told()
    {
        Found[] found = [new("odd", 2, "0.3|0")];

        var (_, other) = CoopCompat.Split(found, 1, f => f.Number, f => f.Version, _ => true);

        Assert.Equal((2, ""), other);
    }
}
