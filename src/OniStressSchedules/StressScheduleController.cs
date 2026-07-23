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
            mildSchedule = FindOrCreateSchedule(manager, MildScheduleName, mild: true);
            stressedSchedule = FindOrCreateSchedule(manager, StressedScheduleName, mild: false);
            Debug.Log("[ONI Stress Schedules] Automatic schedules are ready.");
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
            string name,
            bool mild)
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

            var workGroup = Db.Get().ScheduleGroups.Worktime;
            var breakGroup = Db.Get().ScheduleGroups.Recreation;
            var workBlockIndex = 0;

            for (var blockIndex = 0; blockIndex < schedule.GetBlocks().Count; blockIndex++)
            {
                if (schedule.GetBlock(blockIndex).GroupId != workGroup.Id)
                {
                    continue;
                }

                // Mild alterna lavoro e pausa; stressed converte ogni ora di lavoro.
                var replaceWithBreak = !mild || workBlockIndex % 2 == 1;
                if (replaceWithBreak)
                {
                    schedule.SetBlockGroup(blockIndex, breakGroup);
                }

                workBlockIndex++;
            }

            return schedule;
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
                $"[ONI Stress Schedules] {identity.GetProperName()} -> {target.name}");
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
                    $"[ONI Stress Schedules] {identity.GetProperName()} -> {target.name}");
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
