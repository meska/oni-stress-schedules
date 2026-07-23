namespace OniStressSchedules
{
    /// <summary>
    /// Decides which schedule tier applies while keeping separate enter/exit
    /// thresholds, so a duplicant does not bounce between schedules.
    /// </summary>
    public static class StressPolicy
    {
        public static StressMode Decide(
            StressMode current,
            float stress,
            StressSchedulesConfig config)
        {
            // Se el stress xe altissimo, no sta far tanti conti: pausa completa.
            if (stress >= config.StressedEnter)
            {
                return StressMode.Stressed;
            }

            if (current == StressMode.Stressed && stress >= config.StressedExit)
            {
                return StressMode.Stressed;
            }

            // Scesi dalla fascia rossa si passa prima dalla convalescenza lieve.
            if (current == StressMode.Stressed)
            {
                return stress < config.MildStressedExit
                    ? StressMode.Normal
                    : StressMode.MildStressed;
            }

            if (current == StressMode.MildStressed)
            {
                return stress < config.MildStressedExit
                    ? StressMode.Normal
                    : StressMode.MildStressed;
            }

            return stress >= config.MildStressedEnter
                ? StressMode.MildStressed
                : StressMode.Normal;
        }
    }
}
