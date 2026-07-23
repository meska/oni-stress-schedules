using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using PeterHan.PLib.Options;
using UnityEngine;

namespace OniStressSchedules
{
    [ConfigFile(IndentOutput: true, SharedConfigLocation: true)]
    [ModInfo("https://github.com/meska/oni-stress-schedules", "preview.png")]
    public sealed class StressSchedulesConfig : IOptions
    {
        [Option(
            "Enter Mild-Stressed (%)",
            "Stress level that moves a duplicant from their normal schedule to Mild-Stressed.",
            "Stress thresholds")]
        [Limit(0, 100, 1)]
        [JsonProperty]
        public float MildStressedEnter { get; set; } = 35f;

        [Option(
            "Enter Stressed (%)",
            "Stress level that moves a duplicant to the full Stressed recovery schedule.",
            "Stress thresholds")]
        [Limit(0, 100, 1)]
        [JsonProperty]
        public float StressedEnter { get; set; } = 60f;

        [Option(
            "Return to normal (%)",
            "A Mild-Stressed duplicant returns to their original schedule below this level.",
            "Recovery thresholds")]
        [Limit(0, 100, 1)]
        [JsonProperty]
        public float MildStressedExit { get; set; } = 20f;

        [Option(
            "Leave Stressed (%)",
            "A Stressed duplicant moves down to Mild-Stressed below this level.",
            "Recovery thresholds")]
        [Limit(0, 100, 1)]
        [JsonProperty]
        public float StressedExit { get; set; } = 45f;

        [Option(
            "Enter health recovery (%)",
            "Health at or below this level forces a duplicant onto the Stressed schedule.",
            "Health thresholds")]
        [Limit(0, 100, 1)]
        [JsonProperty]
        public float HealthStressedEnter { get; set; } = 40f;

        [Option(
            "Leave health recovery (%)",
            "A duplicant stays on the Stressed schedule until health reaches this level.",
            "Health thresholds")]
        [Limit(0, 100, 1)]
        [JsonProperty]
        public float HealthStressedExit { get; set; } = 60f;

        public static StressSchedulesConfig Load(string modPath)
        {
            try
            {
                MigrateLegacyConfig(modPath);
                var config = POptions.ReadSettings<StressSchedulesConfig>();

                if (config == null || !config.IsValid())
                {
                    Debug.LogWarning(
                        "[Stress Schedules] Missing or invalid options; using defaults.");
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

        public void OnOptionsChanged()
        {
            if (!IsValid())
            {
                Debug.LogWarning(
                    "[Stress Schedules] Threshold order is invalid; restoring defaults.");
                var defaults = new StressSchedulesConfig();
                POptions.WriteSettings(defaults);
                StressScheduleController.Configure(defaults);
                return;
            }

            // Le opzioni nuove entra in funzione subito, senza riavviar el zogo.
            StressScheduleController.Configure(this);
        }

        public IEnumerable<IOptionsEntry> CreateOptions()
        {
            return null;
        }

        public bool IsValid()
        {
            return MildStressedExit >= 0f
                && MildStressedExit < MildStressedEnter
                && MildStressedEnter < StressedExit
                && StressedExit < StressedEnter
                && StressedEnter <= 100f
                && HealthStressedEnter >= 0f
                && HealthStressedEnter < HealthStressedExit
                && HealthStressedExit <= 100f;
        }

        private static void MigrateLegacyConfig(string modPath)
        {
            var sharedPath = POptions.GetConfigFilePath(typeof(StressSchedulesConfig));
            var legacyPath = Path.Combine(modPath, "config.json");
            if (File.Exists(sharedPath) || !File.Exists(legacyPath))
            {
                return;
            }

            var legacy = JsonConvert.DeserializeObject<StressSchedulesConfig>(
                File.ReadAllText(legacyPath));
            if (legacy != null && legacy.IsValid())
            {
                // Copia una volta sola: local e Workshop dopo usa la stessa configurazion.
                POptions.WriteSettings(legacy);
            }
        }
    }
}
