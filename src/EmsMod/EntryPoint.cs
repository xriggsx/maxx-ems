using System;
using Rage;
using EmsMod.Callouts;
using EmsMod.Core;
using EmsMod.Dialogue;
using EmsMod.UI;
using EmsMod.Utils;

[assembly: Rage.Attributes.Plugin("EmsMod", Description = "Kid-friendly EMS roleplay mod", Author = "MaxxEms")]

namespace EmsMod
{
    public static class EntryPoint
    {
        public static void Main()
        {
            // Last-resort net: catches anything that slips past every other
            // Safe.Run/try-catch, including exceptions on background
            // GameFibers and console-command handlers, which don't run
            // inside the main loop's own try-catch below.
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Log.Error($"UNHANDLED EXCEPTION (IsTerminating={e.IsTerminating}): {e.ExceptionObject}");
            };

            Game.AddConsoleCommands();
            PromptUI.Initialize();
            DialogueEngine.Initialize();
            Log.Info("Plugin loaded successfully.");

            try
            {
                while (true)
                {
                    try
                    {
                        DialogueEngine.Tick();
                        CalloutManager.Tick();
                        GameFiber.Yield();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "EntryPoint.Main tick");
                    }
                }
            }
            finally
            {
                // Runs on plugin unload/hot-reload (RPH aborts this fiber, which
                // unwinds as a ThreadAbortException) so nothing spawned by this
                // mod is ever left behind in the world.
                EntitySpawnRegistry.CleanupAll();
            }
        }
    }
}
