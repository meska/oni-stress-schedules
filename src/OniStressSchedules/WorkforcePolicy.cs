namespace OniStressSchedules
{
    /// <summary>
    /// Keeps a configurable number of duplicants on ordinary schedules when
    /// the colony would otherwise move everyone into recovery.
    /// </summary>
    public static class WorkforcePolicy
    {
        public static bool NeedsWorkingProtection(
            bool isAlreadyRecovering,
            int workingDuplicants,
            int minimumWorkingDuplicants)
        {
            if (minimumWorkingDuplicants <= 0)
            {
                return false;
            }

            // Chi xe za in pausa torna al lavoro solo se semo sotto el minimo.
            return isAlreadyRecovering
                ? workingDuplicants < minimumWorkingDuplicants
                : workingDuplicants <= minimumWorkingDuplicants;
        }
    }
}
