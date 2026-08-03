using System;
using System.Linq;

namespace OniStressSchedules
{
    internal enum PendingUpdateAction
    {
        None,
        WaitForRestart,
        MarkApplied,
        Retry
    }

    internal static class WorkshopUpdatePolicy
    {
        internal static bool ShouldInspect(uint remoteTimestamp, uint checkedTimestamp)
        {
            // Se Steam cambia el timestamp, el pacchetto va controllà senza fidarse de la cache locale.
            return remoteTimestamp > 0 && remoteTimestamp != checkedTimestamp;
        }

        internal static bool IsNewerVersion(string remoteVersion, string currentVersion)
        {
            if (!Version.TryParse(remoteVersion, out var remote) || remote == null)
                return false;
            if (!Version.TryParse(currentVersion, out var current) || current == null)
                return false;
            return remote > current;
        }

        internal static PendingUpdateAction GetPendingAction(
            uint pendingTimestamp,
            string pendingVersion,
            string currentVersion,
            bool reinstallPending,
            bool packageExists)
        {
            if (pendingTimestamp == 0U || string.IsNullOrEmpty(pendingVersion))
                return PendingUpdateAction.None;

            if (!IsNewerVersion(pendingVersion, currentVersion))
                return PendingUpdateAction.MarkApplied;

            return reinstallPending && packageExists
                ? PendingUpdateAction.WaitForRestart
                : PendingUpdateAction.Retry;
        }

        internal static bool IsSafeArchivePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string normalized = path.Replace('\\', '/');
            if (normalized.StartsWith("/", StringComparison.Ordinal) || normalized.Contains(":"))
                return false;

            return normalized.Split('/').All(segment => segment != "..");
        }
    }
}
