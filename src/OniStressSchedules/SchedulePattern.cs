namespace OniStressSchedules
{
    public static class SchedulePattern
    {
        public static bool UseBreakInMild(int workBlockIndex, int totalWorkBlocks)
        {
            if (workBlockIndex < 0 || totalWorkBlocks <= 0)
            {
                return false;
            }

            // Prima metà lavoro, seconda metà pausa: el dupe no fa avanti-indrio ogni ora.
            return workBlockIndex >= (totalWorkBlocks + 1) / 2;
        }
    }
}
