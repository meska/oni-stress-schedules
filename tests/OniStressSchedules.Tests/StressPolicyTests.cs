using Xunit;

namespace OniStressSchedules.Tests
{
    public sealed class StressPolicyTests
    {
        private readonly StressSchedulesConfig config = new();

        [Theory]
        [InlineData(0f, StressMode.Normal)]
        [InlineData(34.9f, StressMode.Normal)]
        [InlineData(35f, StressMode.MildStressed)]
        [InlineData(59.9f, StressMode.MildStressed)]
        [InlineData(60f, StressMode.Stressed)]
        public void NormalModeUsesEnterThresholds(float stress, StressMode expected)
        {
            Assert.Equal(expected, StressPolicy.Decide(StressMode.Normal, stress, config));
        }

        [Theory]
        [InlineData(19.9f, StressMode.Normal)]
        [InlineData(20f, StressMode.MildStressed)]
        [InlineData(59.9f, StressMode.MildStressed)]
        [InlineData(60f, StressMode.Stressed)]
        public void MildModeUsesRecoveryThreshold(float stress, StressMode expected)
        {
            Assert.Equal(expected, StressPolicy.Decide(StressMode.MildStressed, stress, config));
        }

        [Theory]
        [InlineData(19.9f, StressMode.Normal)]
        [InlineData(20f, StressMode.MildStressed)]
        [InlineData(44.9f, StressMode.MildStressed)]
        [InlineData(45f, StressMode.Stressed)]
        [InlineData(100f, StressMode.Stressed)]
        public void StressedModeRecoversThroughMild(float stress, StressMode expected)
        {
            Assert.Equal(expected, StressPolicy.Decide(StressMode.Stressed, stress, config));
        }
    }
}
