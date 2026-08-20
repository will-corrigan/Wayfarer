namespace Wayfarer.Api;

/// <summary>Dalamud IPC gate names and versioning shared by Wayfarer and its consumers.
/// The consumer wires these to its own ICallGateSubscriber instances and passes the
/// resulting delegates into <see cref="WayfarerClient"/>.</summary>
public static class WayfarerIpc
{
    public const string NavigationGate = "Wayfarer.NavigationJson";

    public const string UnlocksGate = "Wayfarer.UnlocksJson";

    public const string VersionGate = "Wayfarer.ApiVersion";

    public const int ApiVersion = 1;
}
