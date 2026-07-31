using System;
using Rage.Attributes;
using EmsMod.Utils;

namespace EmsMod.Debug
{
    /// <summary>
    /// Temporary, phase-scoped console commands used to smoke-test individual
    /// infra pieces in-game before any real callout exists. Safe to delete
    /// once each piece is proven and superseded by real usage.
    /// </summary>
    public static class DebugCommands
    {
        [ConsoleCommand("emsmod_test_log", Description = "Logs one line at each level to verify Log works.")]
        public static void TestLog()
        {
            Log.Info("Test info line.");
            Log.Warn("Test warning line.");
            Log.Error("Test error line.");
        }

        [ConsoleCommand("emsmod_test_safe", Description = "Deliberately throws inside Safe.Run to verify it's caught and logged without crashing the plugin.")]
        public static void TestSafe()
        {
            Safe.Run(() => throw new InvalidOperationException("This is a deliberate test exception."), "DebugCommands.TestSafe");
            Log.Info("Safe.Run returned control normally after the deliberate exception above.");
        }
    }
}
