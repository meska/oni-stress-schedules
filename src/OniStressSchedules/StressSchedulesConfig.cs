using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace OniStressSchedules
{
    public sealed class StressSchedulesConfig
    {
        public float MildStressedEnter { get; set; } = 35f;

        public float StressedEnter { get; set; } = 60f;

        public float MildStressedExit { get; set; } = 20f;

        public float StressedExit { get; set; } = 45f;

        public static StressSchedulesConfig Load(string modPath)
        {
            var configPath = Path.Combine(modPath, "config.json");

            try
            {
                if (!File.Exists(configPath))
                {
                    Debug.LogWarning("[Stress Schedules] config.json not found; using defaults.");
                    return new StressSchedulesConfig();
                }

                var config = JsonConvert.DeserializeObject<StressSchedulesConfig>(
                    File.ReadAllText(configPath));

                if (config == null || !config.IsValid())
                {
                    Debug.LogWarning("[Stress Schedules] Invalid config.json; using defaults.");
                    return new StressSchedulesConfig();
                }

                return config;
            }
            catch (Exception exception)
            {
                // Se el JSON xe roto, meio zogar coi default che butar zo el salvataggio.
                Debug.LogWarning(
                    $"[Stress Schedules] Cannot read config.json; using defaults. {exception.Message}");
                return new StressSchedulesConfig();
            }
        }

        public bool IsValid()
        {
            return MildStressedExit >= 0f
                && MildStressedExit < MildStressedEnter
                && MildStressedEnter < StressedExit
                && StressedExit < StressedEnter
                && StressedEnter <= 100f;
        }
    }
}
