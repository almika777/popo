using System.Net;
using Hangfire.Dashboard;

namespace Popo.Jobs;

/// <summary>
/// Allows Hangfire Dashboard requests from the local machine and private container networks.
/// </summary>
public sealed class DockerLocalRequestsOnlyAuthorizationFilter : IDashboardAuthorizationFilter
{
    /// <inheritdoc />
    public bool Authorize(DashboardContext context)
    {
        var address = context.GetHttpContext().Connection.RemoteIpAddress;
        return address is not null && IsLocalOrPrivate(address);
    }

    private static bool IsLocalOrPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        var bytes = address.MapToIPv4().GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }
}
