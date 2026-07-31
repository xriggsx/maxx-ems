using EmsMod.Utils;

namespace EmsMod.Callouts
{
    /// <summary>
    /// Enforces single-active-callout-at-a-time (v1 design decision - see
    /// project notes) and ticks exactly the current callout each frame.
    /// </summary>
    public static class CalloutManager
    {
        private static CalloutBase _current;

        public static bool HasActiveCallout => _current != null && !_current.IsFinished;

        public static bool Dispatch(CalloutBase callout)
        {
            if (HasActiveCallout)
            {
                Log.Warn($"CalloutManager: cannot dispatch {callout.GetType().Name}, " +
                         $"{_current.GetType().Name} [{_current.InstanceId}] is still active.");
                return false;
            }

            _current = callout;
            Safe.Run(callout.Start, $"CalloutManager.Dispatch [{callout.GetType().Name}]");
            return true;
        }

        public static void Tick()
        {
            if (_current == null)
            {
                return;
            }

            _current.Tick();

            if (_current.IsFinished)
            {
                _current = null;
            }
        }
    }
}
