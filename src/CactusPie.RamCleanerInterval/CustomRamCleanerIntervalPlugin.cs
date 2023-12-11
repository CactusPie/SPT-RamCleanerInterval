using System;
using System.Linq;
using System.Reflection;
using System.Timers;
using Aki.Reflection.Utils;
using BepInEx;
using BepInEx.Configuration;
using JetBrains.Annotations;
using UnityEngine;

namespace CactusPie.RamCleanerInterval
{
    [BepInPlugin("com.cactuspie.ramcleanerinterval", "CactusPie.RamCleanerInterval", "1.0.0")]
    public class CustomRamCleanerIntervalPlugin : BaseUnityPlugin
    {
        private Timer _timer;

        private MethodInfo _emptyWorkingSetMethod;

        public static ConfigEntry<string> CleanNow { get; set; }

        internal static ConfigEntry<int> RamCleanerInterval { get; set; }

        internal static ConfigEntry<bool> IntervalEnabled { get; set; }

        internal static ConfigEntry<bool> OnlyInRaid { get; set; }

        [UsedImplicitly]
        internal void Start()
        {
            const string sectionName = "Override RAM cleaner interval";

            IntervalEnabled = Config.Bind
            (
                sectionName,
                "Interval enabled",
                true,
                new ConfigDescription
                (
                    "Whether or not we should use the custom RAM cleaner interval",
                    null,
                    new ConfigurationManagerAttributes
                    {
                        Order = 4,
                    }
                )
            );

            RamCleanerInterval = Config.Bind
            (
                sectionName,
                "Interval (seconds)",
                300,
                new ConfigDescription
                (
                    "Number of seconds between each RAM cleaner execution. Changing this setting resets the interval",
                    new AcceptableValueRange<int>(30, 900),
                    new ConfigurationManagerAttributes
                    {
                        Order = 3,
                    }
                )
            );

            CleanNow = Config.Bind(
                sectionName,
                "Clean now",
                "Execute the RAM cleaner now",
                new ConfigDescription(
                    "Execute the RAM cleaner now",
                    null,
                    new ConfigurationManagerAttributes
                    {
                        CustomDrawer = CleanNowButtonDrawer,
                        Order = 2,
                    }
                ));

            OnlyInRaid = Config.Bind
            (
                sectionName,
                "Only in raid",
                true,
                new ConfigDescription
                (
                    "Only run the RAM cleaner in raid",
                    null,
                    new ConfigurationManagerAttributes
                    {
                        Order = 1,
                    }
                )
            );

            _timer = new Timer
            {
                AutoReset = true,
                Interval = RamCleanerInterval.Value * 1000,
            };

            _timer.Elapsed += TimerOnElapsed;

            RamCleanerInterval.SettingChanged += RamCleanerIntervalOnSettingChanged;
            IntervalEnabled.SettingChanged += IntervalEnabledOnSettingChanged;

            _emptyWorkingSetMethod = (
                from eftType in PatchConstants.EftTypes // Should be GClass693 as of 3.7.4
                let emptyWorkingSetMethod = eftType.GetMethod("EmptyWorkingSet")
                where emptyWorkingSetMethod != null && !emptyWorkingSetMethod.GetParameters().Any()
                select emptyWorkingSetMethod
            ).Single();

            if (IntervalEnabled.Value)
            {
                _timer.Start();
            }
            else
            {
                _timer.Stop();
            }
        }

        private void IntervalEnabledOnSettingChanged(object sender, EventArgs e)
        {
            if (IntervalEnabled.Value)
            {
                _timer.Start();
            }
            else
            {
                _timer.Stop();
            }
        }

        private void RamCleanerIntervalOnSettingChanged(object sender, EventArgs e)
        {
            ChangeInterval(RamCleanerInterval.Value);
        }

        private void TimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            ExecuteCleaner();
        }

        /// <summary>
        /// Execute the ram cleaner
        /// </summary>
        /// <param name="force">If the ram cleaner should be executed even when not in raid</param>
        private void ExecuteCleaner(bool force = false)
        {
            if (!force && OnlyInRaid.Value && !GameHelper.IsInGame())
            {
                return;
            }

            Logger.LogInfo("Executing the RAM cleaner");

            _emptyWorkingSetMethod.Invoke(null, null);
        }

        private void CleanNowButtonDrawer(ConfigEntryBase entry)
        {
            bool button = GUILayout.Button("Clean now", GUILayout.ExpandWidth(true));
            if (button)
            {
                ExecuteCleaner(true);
            }
        }

        private void ChangeInterval(int interval)
        {
            if (_timer.Enabled)
            {
                _timer.Stop();
                _timer.Interval = interval * 1000;
                _timer.Start();
            }
            else
            {
                _timer.Interval = interval * 1000;
            }
        }
    }
}
