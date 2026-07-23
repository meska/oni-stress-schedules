using System.Linq;
using Klei.AI;
using UnityEngine;

namespace OniStressSchedules
{
    public static class StressScheduleController
    {
        public const string MildScheduleName = "Mild-Stressed";
        public const string StressedScheduleName = "Stressed";

        private static StressSchedulesConfig config = new StressSchedulesConfig();
        private static Schedule mildSchedule;
        private static Schedule stressedSchedule;

        public static void Configure(StressSchedulesConfig loadedConfig)
        {
            config = loadedConfig ?? new StressSchedulesConfig();
        }

        public static void InitializeSchedules(ScheduleManager manager)
        {
            var template = manager.GetSchedules().Find(
                schedule => schedule.name != MildScheduleName
                    && schedule.name != StressedScheduleName);
            mildSchedule = FindOrCreateSchedule(manager, MildScheduleName);
            stressedSchedule = FindOrCreateSchedule(manager, StressedScheduleName);
            ApplySchedulePattern(mildSchedule, template, mild: true);
            ApplySchedulePattern(stressedSchedule, template, mild: false);
            Debug.Log("[Stress Schedules] Automatic schedules are ready.");
        }

        public static void Update(MinionIdentity identity)
        {
            var manager = ScheduleManager.Instance;
            if (identity == null || manager == null || mildSchedule == null || stressedSchedule == null)
            {
                return;
            }

            var state = identity.gameObject.AddOrGet<StressScheduleState>();
            AmountInstance stress = Db.Get().Amounts.Stress.Lookup(identity.gameObject);
            if (stress == null)
            {
                return;
            }

            var schedulable = identity.GetComponent<Schedulable>();
            var currentSchedule = manager.GetSchedule(schedulable);
            if (state.Mode == StressMode.Normal)
            {
                if (currentSchedule == mildSchedule)
                {
                    state.Mode = StressMode.MildStressed;
                }
                else if (currentSchedule == stressedSchedule)
                {
                    state.Mode = StressMode.Stressed;
                }
            }

            var desiredMode = StressPolicy.Decide(state.Mode, stress.value, config);
            if (desiredMode == state.Mode)
            {
                // Finché no rientra, la modalità automatica resta davvero assegnata.
                EnforceActiveSchedule(currentSchedule, schedulable, desiredMode);
                return;
            }

            if (desiredMode == StressMode.Normal)
            {
                RestoreOriginalSchedule(identity, state, manager);
                return;
            }

            ApplyStressSchedule(identity, state, manager, desiredMode);
        }

        private static void EnforceActiveSchedule(
            Schedule current,
            Schedulable schedulable,
            StressMode mode)
        {
            if (mode == StressMode.MildStressed)
            {
                ChangeAssignment(current, mildSchedule, schedulable);
            }
            else if (mode == StressMode.Stressed)
            {
                ChangeAssignment(current, stressedSchedule, schedulable);
            }
        }

        private static Schedule FindOrCreateSchedule(
            ScheduleManager manager,
            string name)
        {
            var existing = manager.GetSchedules().Find(schedule => schedule.name == name);
            if (existing != null)
            {
                return existing;
            }

            var schedule = manager.AddSchedule(
                Db.Get().ScheduleGroups.allGroups,
                name,
                alarmOn: false);
            return schedule;
        }

        private static void ApplySchedulePattern(
            Schedule schedule,
            Schedule template,
            bool mild)
        {
            if (schedule == null || template == null)
            {
                return;
            }

            var groups = Db.Get().ScheduleGroups.allGroups;
            var workGroup = Db.Get().ScheduleGroups.Worktime;
            var breakGroup = Db.Get().ScheduleGroups.Recreation;
            var totalWorkBlocks = template.GetBlocks().Count(
                block => block.GroupId == workGroup.Id);
            var workBlockIndex = 0;

            var blockCount = Mathf.Min(
                schedule.GetBlocks().Count,
                template.GetBlocks().Count);
            for (var blockIndex = 0; blockIndex < blockCount; blockIndex++)
            {
                var templateGroupId = template.GetBlock(blockIndex).GroupId;
                var templateGroup = groups.Find(group => group.Id == templateGroupId);
                if (templateGroup == null)
                {
                    continue;
                }

                if (templateGroupId == workGroup.Id)
                {
                    var replaceWithBreak = !mild
                        || SchedulePattern.UseBreakInMild(
                            workBlockIndex,
                            totalWorkBlocks);
                    schedule.SetBlockGroup(
                        blockIndex,
                        replaceWithBreak ? breakGroup : workGroup);
                    workBlockIndex++;
                }
                else
                {
                    // Ripristina anche i blocchi alterati dalla disposizione v1.
                    schedule.SetBlockGroup(blockIndex, templateGroup);
                }
            }
        }

        private static void ApplyStressSchedule(
            MinionIdentity identity,
            StressScheduleState state,
            ScheduleManager manager,
            StressMode desiredMode)
        {
            var schedulable = identity.GetComponent<Schedulable>();
            var current = manager.GetSchedule(schedulable);

            // L'orario originale si cattura una volta sola e sopravvive nel save.
            if (state.Mode == StressMode.Normal && !IsManagedSchedule(current))
            {
                state.OriginalScheduleName = current?.name;
            }

            var target = desiredMode == StressMode.Stressed
                ? stressedSchedule
                : mildSchedule;

            ChangeAssignment(current, target, schedulable);
            state.Mode = desiredMode;

            Debug.Log(
                $"[Stress Schedules] {identity.GetProperName()} -> {target.name}");
        }

        private static void RestoreOriginalSchedule(
            MinionIdentity identity,
            StressScheduleState state,
            ScheduleManager manager)
        {
            if (state.Mode == StressMode.Normal)
            {
                return;
            }

            var schedulable = identity.GetComponent<Schedulable>();
            var current = manager.GetSchedule(schedulable);
            var target = FindOriginalOrFallback(manager, state.OriginalScheduleName);

            if (target != null)
            {
                ChangeAssignment(current, target, schedulable);
                Debug.Log(
                    $"[Stress Schedules] {identity.GetProperName()} -> {target.name}");
            }

            state.Clear();
        }

        private static Schedule FindOriginalOrFallback(
            ScheduleManager manager,
            string originalName)
        {
            var schedules = manager.GetSchedules();
            var original = schedules.Find(
                schedule => schedule.name == originalName && !IsManagedSchedule(schedule));

            if (original != null)
            {
                return original;
            }

            // Se l'utente ga cancellà l'orario, torna al primo orario normale valido.
            return schedules.Find(schedule => !IsManagedSchedule(schedule));
        }

        private static bool IsManagedSchedule(Schedule schedule)
        {
            return schedule == mildSchedule || schedule == stressedSchedule;
        }

        private static void ChangeAssignment(
            Schedule current,
            Schedule target,
            Schedulable schedulable)
        {
            if (target == null || current == target)
            {
                return;
            }

            current?.Unassign(schedulable);
            target.Assign(schedulable);
        }
    }
}
