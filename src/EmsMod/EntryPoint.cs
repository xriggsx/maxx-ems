using System;
using Rage;
using EmsMod.Core;
using EmsMod.UI;
using EmsMod.Utils;

[assembly: Rage.Attributes.Plugin("EmsMod", Description = "Kid-friendly EMS roleplay mod", Author = "MaxxEms")]

namespace EmsMod
{
    public static class EntryPoint
    {
        public static void Main()
        {
            Game.AddConsoleCommands();
            PromptUI.Initialize();
            Log.Info("Plugin loaded successfully.");

            try
            {
                while (true)
                {
                    try
                    {
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
