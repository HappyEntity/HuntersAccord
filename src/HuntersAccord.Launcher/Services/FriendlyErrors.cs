using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using HuntersAccord.Launcher.Localization;

namespace HuntersAccord.Launcher.Services;

/// <summary>Explains a failure in plain words (no internet, refused, disk full); unknown errors keep their own message.</summary>
public static class FriendlyErrors
{
    private const int DiskFull = unchecked((int)0x80070070);
    private const int HandleDiskFull = unchecked((int)0x80070027);

    public static string Describe(Exception e) => e switch
    {
        // An HttpClient timeout comes as a cancellation (TaskCanceledException), with a TimeoutException inside.
        OperationCanceledException or TimeoutException => Text("Error.Timeout"),
        HttpRequestException { StatusCode: HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests } => Text("Error.RateLimited"),
        HttpRequestException { StatusCode: { } status } => string.Format(Text("Error.ServerError"), (int)status),
        HttpRequestException or SocketException => NetworkInterface.GetIsNetworkAvailable() ? Text("Error.ServerUnreachable") : Text("Error.NoConnection"),
        IOException { HResult: DiskFull or HandleDiskFull } => Text("Error.DiskFull"),
        IOException { InnerException: SocketException } or IOException { InnerException: HttpRequestException } => Text("Error.ConnectionLost"),
        UnauthorizedAccessException => Text("Error.NoAccess"),
        _ => e.Message,
    };

    private static string Text(string key) => Localizer.Instance[key];
}
