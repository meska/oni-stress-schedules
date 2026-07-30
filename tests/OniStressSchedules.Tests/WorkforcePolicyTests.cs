using Xunit;

namespace OniStressSchedules.Tests
{
    public sealed class WorkforcePolicyTests
    {
        [Fact]
        public void LastRequiredWorkerCannotEnterRecovery()
        {
            Assert.True(WorkforcePolicy.NeedsWorkingProtection(
                isAlreadyRecovering: false,
                workingDuplicants: 1,
                minimumWorkingDuplicants: 1));
        }

        [Fact]
        public void SurplusWorkerCanEnterRecovery()
        {
            Assert.False(WorkforcePolicy.NeedsWorkingProtection(
                isAlreadyRecovering: false,
                workingDuplicants: 2,
                minimumWorkingDuplicants: 1));
        }

        [Fact]
        public void RecoveryWorkerReturnsWhenColonyIsBelowMinimum()
        {
            Assert.True(WorkforcePolicy.NeedsWorkingProtection(
                isAlreadyRecovering: true,
                workingDuplicants: 0,
                minimumWorkingDuplicants: 1));
        }

        [Fact]
        public void RecoveryWorkerStaysPutOnceMinimumIsMet()
        {
            Assert.False(WorkforcePolicy.NeedsWorkingProtection(
                isAlreadyRecovering: true,
                workingDuplicants: 1,
                minimumWorkingDuplicants: 1));
        }

        [Fact]
        public void ZeroDisablesWorkforceProtection()
        {
            Assert.False(WorkforcePolicy.NeedsWorkingProtection(
                isAlreadyRecovering: false,
                workingDuplicants: 0,
                minimumWorkingDuplicants: 0));
        }
    }
}
