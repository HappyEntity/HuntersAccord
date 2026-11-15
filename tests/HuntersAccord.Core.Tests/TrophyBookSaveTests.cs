using HuntersAccord.Core.Trophies;

namespace HuntersAccord.Core.Tests;

/// <summary>
/// The book's file replaced while another program holds the old one open without delete sharing (seen on players'
/// PCs: the move refused with UnauthorizedAccessException).
/// </summary>
public sealed class TrophyBookSaveTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ha-trb-" + Guid.NewGuid().ToString("N"));

    public TrophyBookSaveTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static TrophyBook With(params int[] ids)
    {
        var book = new TrophyBook();
        foreach (var id in ids) book.Award(id, DateTimeOffset.UnixEpoch, null, TrophyHow.Game);
        return book;
    }

    private static int[] Ids(int n) => [.. TrophyCatalog.All.Where(t => t.Id != TrophyCatalog.PlatinumId).Take(n).Select(t => t.Id)];

    [Fact]
    public void SavedWhenTheOldFileIsHeldForAWhile()
    {
        var path = Path.Combine(_root, TrophyBook.FileName);
        With(Ids(1)).Save(path);
        var holder = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); // no delete sharing: the move is refused
        var release = Task.Run(async () =>
        {
            await Task.Delay(300, TestContext.Current.CancellationToken);
            holder.Dispose();
        });
        With(Ids(2)).Save(path);
        release.Wait();
        Assert.Equal(2, TrophyBook.Read(path).Held.Count);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void TheOldFileKeptWhenItCannotBeReplaced()
    {
        var path = Path.Combine(_root, TrophyBook.FileName);
        With(Ids(1)).Save(path);
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsAny<Exception>(() => With(Ids(2)).Save(path));
        Assert.Single(TrophyBook.Read(path).Held);
    }
}
