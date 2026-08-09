using System;
using System.Threading;
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
        private static int _hasRun;

        public static void Main()
        {
            // Guard against Main() somehow being invoked more than once in
            // the same AppDomain (e.g. RPH retrying a slow/stalled initial
            // load) - without this, a second invocation would re-register
            // every console command and conflict with the first invocation's
            // still-active registrations, which is exactly the intermittent
            // "already defined, renamed to X2" behavior seen in testing with
            // no duplicate DLL or process involved. Interlocked rather than a
            // plain bool in case two invocations start close enough together
            // to race on the check.
            if (Interlocked.CompareExchange(ref _hasRun, 1, 0) != 0)
            {
                Log.Warn("EntryPoint.Main invoked again in the same AppDomain - ignoring the second invocation.");
                return;
            }

            // Last-resort net: catches anything that slips past every other
            // Safe.Run/try-catch, including exceptions on background
            // GameFibers and console-command handlers, which don't run
            // inside the main loop's own try-catch below.
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Log.Error($"UNHANDLED EXCEPTION (IsTerminating={e.IsTerminating}): {e.ExceptionObject}");
            };

            // NOTE: We deliberately do NOT call Game.AddConsoleCommands here.
            // RPH automatically scans a loaded plugin assembly and registers
            // every [ConsoleCommand]-attributed method on its own. Calling
            // AddConsoleCommands as well registered each command a SECOND time,
            // which is what produced the wall of "console command conflict!
            // ...already defined, renamed to <name>2" warnings on every launch.
            // Relying solely on RPH's auto-registration means each command is
            // defined exactly once, under its intended name. (The Main re-entry
            // guard above could never have fixed this - the duplicate came from
            // RPH's own scan, not a second Main() call.)
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
                        DutyManager.Tick();
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
