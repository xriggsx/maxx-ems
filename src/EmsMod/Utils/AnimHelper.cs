using Rage;
using Rage.Native;

namespace EmsMod.Utils
{
    /// <summary>
    /// Shared safe-animation playback, used anywhere a ped (player, patient,
    /// partner) needs a looped clip played defensively. Pulled out of
    /// PatientCalloutBase so PartnerManager doesn't carry its own copy of the
    /// same logic.
    /// </summary>
    public static class AnimHelper
    {
        /// <summary>Plays a looped animation, but ONLY after confirming the anim
        /// dictionary exists and loads within a timeout. A missing/invalid dict
        /// would otherwise make RPH's PlayAnimation stall waiting on a load that
        /// never completes, so guarding it keeps a bad clip name harmless (the
        /// ped just keeps its previous pose). Returns true if the animation
        /// started playing.</summary>
        public static bool PlayLoopedSafe(Ped ped, string dictionary, string name, string context)
        {
            if (ped == null || !ped.Exists() ||
                string.IsNullOrWhiteSpace(dictionary) || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            return Safe.Run(() =>
            {
                // Use the typed generic form so RPH marshals the native's return
                // as a real bool. The plain dynamic form could throw converting
                // the native's numeric return to bool, which would silently skip
                // EVERY animation (the likely cause of the pose/kneel not playing).
                bool exists = NativeFunction.Natives.DOES_ANIM_DICT_EXIST<bool>(dictionary);
                if (!exists)
                {
                    Log.Warn($"{context}: animation dictionary '{dictionary}' does not exist; skipping.");
                    return false;
                }

                var animDict = new AnimationDictionary(dictionary);
                animDict.LoadAndWait();
                ped.Tasks.PlayAnimation(animDict, name, 4f, AnimationFlags.Loop);
                Log.Info($"{context}: playing '{dictionary}' / '{name}'.");
                return true;
            }, false, context);
        }
    }
}
