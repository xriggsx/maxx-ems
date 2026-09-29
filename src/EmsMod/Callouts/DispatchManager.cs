using System;
using System.Collections.Generic;
using Rage;
using EmsMod.Config;
using EmsMod.Core;
using EmsMod.Utils;

namespace EmsMod.Callouts
{
    /// <summary>
    /// Automatic dispatch: while on duty with no active callout, waits a
    /// random interval (General.xml's DispatchMinIntervalSeconds/Max) then
    /// dispatches a random callout matching the player's current duty mode -
    /// so calls come in on their own instead of needing a console command
    /// every time. Also backs the duty menu's "Request Callout" row for an
    /// on-demand call right now.
    /// </summary>
    public static class DispatchManager
    {
        private static readonly Random Rng = new Random();

        // Every dispatchable callout, paired with the XML file that (per
        // CalloutConfig.DispatchMode) says which duty mode it belongs to -
        // adding/renaming a mode's callouts is a config edit, not a change to
        // this list's mode strings (there's only one: none - the mode itself
        // now lives entirely in each callout's own XML).
        private static readonly List<(string ConfigFileName, Func<CalloutBase> Factory)> Roster = new List<(string, Func<CalloutBase>)>
        {
            ("CarAccident_Bleeding.xml", () => new CarAccident_Bleeding()),
            ("BikeAccident_ScrapedKnee.xml", () => new BikeAccident_ScrapedKnee()),
            ("PlaygroundFall_TwistedAnkle.xml", () => new PlaygroundFall_TwistedAnkle()),
            ("SportsInjury_PossibleFracture.xml", () => new SportsInjury_PossibleFracture()),
            ("Asthma_BreathingTrouble.xml", () => new Asthma_BreathingTrouble()),
            ("HouseFire_SmokeInhalation.xml", () => new HouseFire_SmokeInhalation()),
            ("VehicleFire_Rescue.xml", () => new VehicleFire_Rescue()),
            ("KitchenFire_MinorBurn.xml", () => new KitchenFire_MinorBurn()),
        };

        private static float _waitElapsed;
        private static float _waitTarget;
        private static bool _timerRunning;

        /// <summary>Ticked every frame from EntryPoint. No-ops off duty or
        /// while a callout is already active (CalloutManager only ever runs
        /// one at a time).</summary>
        public static void Tick()
        {
            if (!DutyManager.OnDuty || CalloutManager.HasActiveCallout)
            {
                _timerRunning = false;
                return;
            }

            if (!_timerRunning)
            {
                StartWaiting();
            }

            if (!Game.IsPaused)
            {
                _waitElapsed += Game.FrameTime;
            }

            if (_waitElapsed >= _waitTarget)
            {
                DispatchRandom();
            }
        }

        private static void StartWaiting()
        {
            float min = ConfigLoader.General.DispatchMinIntervalSeconds;
            float max = ConfigLoader.General.DispatchMaxIntervalSeconds;
            _waitElapsed = 0f;
            _waitTarget = min + (float)Rng.NextDouble() * (max - min);
            _timerRunning = true;
            Log.Info($"DispatchManager: next automatic callout in {_waitTarget:F0}s.");
        }

        /// <summary>Dispatches a random callout matching the current duty mode
        /// right away, skipping the wait - used by the duty menu's "Request
        /// Callout" row. Returns false if one is already active or the player
        /// isn't on duty.</summary>
        public static bool RequestNow()
        {
            if (!DutyManager.OnDuty || CalloutManager.HasActiveCallout)
            {
                return false;
            }
            return DispatchRandom();
        }

        private static bool DispatchRandom()
        {
            _timerRunning = false;

            string mode = DutyManager.CurrentModeName;
            var candidates = new List<Func<CalloutBase>>();
            foreach ((string configFileName, Func<CalloutBase> factory) in Roster)
            {
                string calloutMode = ConfigLoader.LoadCallout(configFileName).DispatchMode;
                if (string.Equals(calloutMode, mode, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(factory);
                }
            }

            if (candidates.Count == 0)
            {
                Log.Warn($"DispatchManager: no callouts registered for mode '{mode}'.");
                return false;
            }

            CalloutBase callout = candidates[Rng.Next(candidates.Count)]();
            bool dispatched = CalloutManager.Dispatch(callout);
            Log.Info($"DispatchManager: dispatched {callout.GetType().Name} for mode '{mode}' -> {dispatched}.");
            return dispatched;
        }
    }
}
