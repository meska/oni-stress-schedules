using Xunit;

namespace OniStressSchedules.Tests;

public sealed class WorkshopUpdatePolicyTests
{
    [Theory]
    [InlineData(200U, 100U, true)]
    [InlineData(200U, 200U, false)]
    [InlineData(0U, 100U, false)]
    public void ShouldInspectOnlyUnseenWorkshopTimestamps(uint remote, uint checkedTimestamp, bool expected)
    {
        Assert.Equal(expected, WorkshopUpdatePolicy.ShouldInspect(remote, checkedTimestamp));
    }

    [Theory]
    [InlineData("2.3.1", "2.3.0", true)]
    [InlineData("2.3.0", "2.3.0", false)]
    [InlineData("invalid", "2.3.0", false)]
    public void DetectsOnlyValidNewerVersions(string remote, string current, bool expected)
    {
        Assert.Equal(expected, WorkshopUpdatePolicy.IsNewerVersion(remote, current));
    }

    [Theory]
    [InlineData(0U, null, "2.3.0", false, false, (int)PendingUpdateAction.None)]
    [InlineData(200U, "2.3.1", "2.3.0", true, true, (int)PendingUpdateAction.WaitForRestart)]
    [InlineData(200U, "2.3.1", "2.3.1", false, false, (int)PendingUpdateAction.MarkApplied)]
    [InlineData(200U, "2.3.1", "2.3.0", false, true, (int)PendingUpdateAction.Retry)]
    [InlineData(200U, "2.3.1", "2.3.0", true, false, (int)PendingUpdateAction.Retry)]
    public void ReconcilesPendingUpdatesOnlyAfterTheNewVersionLoads(
        uint timestamp,
        string? pendingVersion,
        string currentVersion,
        bool reinstallPending,
        bool packageExists,
        int expected)
    {
        Assert.Equal((PendingUpdateAction)expected, WorkshopUpdatePolicy.GetPendingAction(
            timestamp,
            pendingVersion ?? string.Empty,
            currentVersion,
            reinstallPending,
            packageExists));
    }

    [Theory]
    [InlineData("mod.yaml", true)]
    [InlineData("nested/config.json", true)]
    [InlineData("../escape.dll", false)]
    [InlineData("nested/../../escape.dll", false)]
    [InlineData("/absolute/mod.dll", false)]
    [InlineData("C:\\escape.dll", false)]
    public void RejectsUnsafeArchivePaths(string path, bool expected)
    {
        Assert.Equal(expected, WorkshopUpdatePolicy.IsSafeArchivePath(path));
    }
}
