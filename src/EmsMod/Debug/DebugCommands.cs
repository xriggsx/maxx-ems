using System;
using Rage;
using Rage.Attributes;
using EmsMod.Config;
using EmsMod.Core;
using EmsMod.Dialogue;
using EmsMod.Input;
using EmsMod.UI;
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
        private static GameFiber _inputTestFiber;
        private static GameFiber _promptTestFiber;

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

        [ConsoleCommand("emsmod_test_input_start", Description = "Starts a background loop that logs whenever any InputAction is pressed, keyboard or controller.")]
        public static void TestInputStart()
        {
            if (_inputTestFiber != null && _inputTestFiber.IsAlive)
            {
                Log.Info("TestInputStart: already running.");
                return;
            }

            _inputTestFiber = GameFiber.StartNew(() =>
            {
                Log.Info("TestInputStart: input test loop running. Press any bound key/button.");
                while (true)
                {
                    foreach (InputAction action in Enum.GetValues(typeof(InputAction)))
                    {
                        if (InputManager.IsActionPressed(action))
                        {
                            Log.Info($"TestInput: '{action}' pressed.");
                        }
                    }
                    GameFiber.Yield();
                }
            }, "EmsMod-InputTest");
        }

        [ConsoleCommand("emsmod_test_input_stop", Description = "Stops the background loop started by emsmod_test_input_start.")]
        public static void TestInputStop()
        {
            if (_inputTestFiber != null && _inputTestFiber.IsAlive)
            {
                _inputTestFiber.Abort();
                Log.Info("TestInputStop: input test loop stopped.");
            }
            else
            {
                Log.Info("TestInputStop: no input test loop was running.");
            }
        }

        [ConsoleCommand("emsmod_test_prompt", Description = "Shows the accept/decline popup with a 15s pause-aware timeout; logs Accepted/Declined/TimedOut.")]
        public static void TestPrompt()
        {
            if (_promptTestFiber != null && _promptTestFiber.IsAlive)
            {
                Log.Info("TestPrompt: already running.");
                return;
            }

            _promptTestFiber = GameFiber.StartNew(() =>
            {
                PromptUI.Show("Test Callout: Car accident on Vinewood Blvd.");

                float elapsed = 0f;
                const float timeoutSeconds = 15f;
                string result = "TimedOut";

                while (elapsed < timeoutSeconds)
                {
                    if (!Game.IsPaused)
                    {
                        elapsed += Game.FrameTime;
                    }

                    if (InputManager.IsActionPressed(InputAction.AcceptCallout))
                    {
                        result = "Accepted";
                        break;
                    }

                    if (InputManager.IsActionPressed(InputAction.DeclineCallout))
                    {
                        result = "Declined";
                        break;
                    }

                    GameFiber.Yield();
                }

                PromptUI.Hide();
                Log.Info($"TestPrompt: result = {result}.");
            }, "EmsMod-PromptTest");
        }

        [ConsoleCommand("emsmod_test_voice", Description = "Speaks one test dispatch line and logs when the completion callback fires.")]
        public static void TestVoice()
        {
            Log.Info("TestVoice: speaking now.");
            DialogueEngine.Speak(
                "Dispatch to all units, this is a test dispatch line.",
                () => Log.Info("TestVoice: completion callback fired."));
        }
    }
}
