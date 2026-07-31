using System;
using System.Collections.Generic;
using Rage;
using EmsMod.Utils;

namespace EmsMod.Core
{
    /// <summary>
    /// The single sanctioned path for tracking anything a callout spawns, so
    /// cleanup is one generic mechanism instead of bespoke per-callout code.
    /// Everything funnels through RegisterCleanupAction internally, including
    /// entities, so one entity failing to delete can never block the rest.
    /// </summary>
    public static class EntitySpawnRegistry
    {
        private static readonly Dictionary<string, List<Action>> _cleanupActions = new Dictionary<string, List<Action>>();

        public static void RegisterEntity(string ownerId, Entity entity)
        {
            RegisterCleanupAction(ownerId, () =>
            {
                if (entity != null && entity.IsValid())
                {
                    entity.Delete();
                }
            });
        }

        public static void RegisterCleanupAction(string ownerId, Action cleanupAction)
        {
            if (!_cleanupActions.TryGetValue(ownerId, out List<Action> actions))
            {
                actions = new List<Action>();
                _cleanupActions[ownerId] = actions;
            }

            actions.Add(cleanupAction);
        }

        public static void CleanupOwner(string ownerId)
        {
            if (!_cleanupActions.TryGetValue(ownerId, out List<Action> actions))
            {
                return;
            }

            foreach (Action action in actions)
            {
                Safe.Run(action, $"EntitySpawnRegistry.CleanupOwner({ownerId})");
            }

            int count = actions.Count;
            _cleanupActions.Remove(ownerId);
            Log.Info($"EntitySpawnRegistry: cleaned up {count} item(s) for owner '{ownerId}'.");
        }

        public static void CleanupAll()
        {
            foreach (string ownerId in new List<string>(_cleanupActions.Keys))
            {
                CleanupOwner(ownerId);
            }
        }

        public static int GetPendingCount(string ownerId)
        {
            return _cleanupActions.TryGetValue(ownerId, out List<Action> actions) ? actions.Count : 0;
        }

        public static int GetTotalPendingCount()
        {
            int total = 0;
            foreach (List<Action> actions in _cleanupActions.Values)
            {
                total += actions.Count;
            }
            return total;
        }
    }
}
