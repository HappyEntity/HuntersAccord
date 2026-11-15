using System.Net;
using HuntersAccord.Core.Updates;

namespace HuntersAccord.Core.Tests;

public sealed class UpdateCheckTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ha-update-" + Guid.NewGuid().ToString("N"));
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private string State => Path.Combine(_dir, "update-check.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Theory]
    [InlineData("v0.1.5", "0.1.4", true)]
    [InlineData("0.2", "0.1.9", true)]
    [InlineData("v0.1.4", "0.1.4", false)]
    [InlineData("0.1.4", "0.1.4.0", false)]
    [InlineData("v0.1.3", "0.1.4", false)]
    [InlineData("nightly", "0.1.4", false)]
    [InlineData("v1.0.0-beta", "0.9.0", true)]
    public void IsNewer(string tag, string current, bool newer) => Assert.Equal(newer, UpdateCheck.IsNewer(tag, current));

    [Fact]
    public async Task AsksAtMostOnceADay()
    {
        var handler = new Answer(HttpStatusCode.OK,
            """{"tag_name":"v0.2.0","html_url":"https://github.com/HappyEntity/HuntersAccord/releases/tag/v0.2.0","draft":false,"prerelease":false}""");
        using var http = new HttpClient(handler);
        var now = new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

        var first = await UpdateCheck.CheckAsync(http, "0.1.4", State, now, Ct);
        var again = await UpdateCheck.CheckAsync(http, "0.1.4", State, now.AddHours(20), Ct);
        Assert.Equal(new NewVersion("0.2.0", "https://github.com/HappyEntity/HuntersAccord/releases/tag/v0.2.0"), first);
        Assert.Equal(first, again); // from the remembered answer
        Assert.Equal(1, handler.Requests);
        Assert.Equal(UpdateCheck.LatestReleaseApi, handler.LastUri);

        await UpdateCheck.CheckAsync(http, "0.1.4", State, now.AddHours(25), Ct);
        Assert.Equal(2, handler.Requests);
        Assert.Null(await UpdateCheck.CheckAsync(http, "0.2.0", State, now.AddHours(26), Ct)); // running it already
    }

    [Fact]
    public async Task NoReleaseOrNoNetworkSaysNothing()
    {
        using var notFound = new HttpClient(new Answer(HttpStatusCode.NotFound, """{"message":"Not Found"}"""));
        Assert.Null(await UpdateCheck.CheckAsync(notFound, "0.1.4", State, cancel: Ct));
        File.Delete(State);
        using var offline = new HttpClient(new Answer(null, ""));
        Assert.Null(await UpdateCheck.CheckAsync(offline, "0.1.4", State, cancel: Ct));
        File.Delete(State);
        using var garbage = new HttpClient(new Answer(HttpStatusCode.OK, "<html>"));
        Assert.Null(await UpdateCheck.CheckAsync(garbage, "0.1.4", State, cancel: Ct));
    }

    /// <summary>A canned answer (status null: no network).</summary>
    private sealed class Answer(HttpStatusCode? status, string body) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public string? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            LastUri = request.RequestUri?.ToString();
            if (status is not { } code) throw new HttpRequestException("offline");
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body) });
        }
    }
}
