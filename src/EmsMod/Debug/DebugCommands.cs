using System;
using Rage;
using Rage.Attributes;
using EmsMod.Config;
using EmsMod.Core;
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

        [ConsoleCommand("emsmod_reloadconfig", Description = "Reloads General.xml from disk and logs the resulting values.")]
        public static void ReloadConfig()
        {
            ConfigLoader.ReloadGeneral();
            var general = ConfigLoader.General;
            Log.Info($"General config: VoiceEngine={general.VoiceEngine}, " +
                     $"AcceptDeclineTimeoutSeconds={general.AcceptDeclineTimeoutSeconds}, " +
                     $"WalkedAwayDistanceMeters={general.WalkedAwayDistanceMeters}, " +
                     $"DebugLoggingEnabled={general.DebugLoggingEnabled}");
        }

        [ConsoleCommand("emsmod_test_spawn", Description = "Spawns one test ped near the player, registered under owner 'TestSpawn'.")]
        public static void TestSpawn()
        {
            Safe.Run(() =>
            {
                Vector3 position = Game.LocalPlayer.Character.Position + new Vector3(2f, 0f, 0f);
                var ped = new Ped(position);
                EntitySpawnRegistry.RegisterEntity("TestSpawn", ped);
                Log.Info($"TestSpawn: spawned a ped. Pending count for 'TestSpawn' is now {EntitySpawnRegistry.GetPendingCount("TestSpawn")}.");
            }, "DebugCommands.TestSpawn");
        }

        [ConsoleCommand("emsmod_test_cleanup", Description = "Cleans up everything registered under owner 'TestSpawn'.")]
        public static void TestCleanup()
        {
            EntitySpawnRegistry.CleanupOwner("TestSpawn");
            Log.Info($"TestCleanup: pending count for 'TestSpawn' is now {EntitySpawnRegistry.GetPendingCount("TestSpawn")}.");
        }

        [ConsoleCommand("emsmod_debug_countspawned", Description = "Logs the total pending cleanup count across all owners in EntitySpawnRegistry.")]
        public static void CountSpawned()
        {
            Log.Info($"EntitySpawnRegistry total pending count: {EntitySpawnRegistry.GetTotalPendingCount()}.");
        }
    }
}
