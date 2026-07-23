using Xunit;

namespace OniStressSchedules.Tests
{
    public sealed class SchedulePatternTests
    {
        [Fact]
        public void EighteenWorkBlocksBecomeTwoGroupedHalves()
        {
            for (var index = 0; index < 9; index++)
            {
                Assert.False(SchedulePattern.UseBreakInMild(index, 18));
            }

            for (var index = 9; index < 18; index++)
            {
                Assert.True(SchedulePattern.UseBreakInMild(index, 18));
            }
        }

        [Fact]
        public void OddWorkBlockCountKeepsTheLargerHalfAsWork()
        {
            Assert.False(SchedulePattern.UseBreakInMild(2, 5));
            Assert.True(SchedulePattern.UseBreakInMild(3, 5));
        }

        [Theory]
        [InlineData(-1, 18)]
        [InlineData(0, 0)]
        public void InvalidInputsDoNotCreateBreaks(int index, int total)
        {
            Assert.False(SchedulePattern.UseBreakInMild(index, total));
        }
    }
}
