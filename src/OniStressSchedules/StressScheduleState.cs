using KSerialization;

namespace OniStressSchedules
{
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class StressScheduleState : KMonoBehaviour, ISaveLoadable
    {
        [Serialize]
        public string OriginalScheduleName;

        [Serialize]
        public StressMode Mode;

        [Serialize]
        public bool HealthRecoveryActive;

        public void Clear()
        {
            OriginalScheduleName = null;
            Mode = StressMode.Normal;
            HealthRecoveryActive = false;
        }
    }
}
