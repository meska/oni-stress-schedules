namespace OniStressSchedules
{
    // Config minimale per testare la policy senza caricare gli assembly Unity.
    public sealed class StressSchedulesConfig
    {
        public float MildStressedEnter { get; set; } = 35f;

        public float StressedEnter { get; set; } = 60f;

        public float MildStressedExit { get; set; } = 20f;

        public float StressedExit { get; set; } = 45f;

        public float HealthStressedEnter { get; set; } = 40f;

        public float HealthStressedExit { get; set; } = 60f;
    }
}
