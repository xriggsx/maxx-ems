using System;
using Rage;
using EmsMod.Utils;

[assembly: Rage.Attributes.Plugin("EmsMod", Description = "Kid-friendly EMS roleplay mod", Author = "MaxxEms")]

namespace EmsMod
{
    public static class EntryPoint
    {
        public static void Main()
        {
            Game.AddConsoleCommands();
            Log.Info("Plugin loaded successfully.");

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
    }
}
