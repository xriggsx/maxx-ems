using Rage;
using Rage.Native;

namespace EmsMod.Utils
{
    /// <summary>
    /// Shared "find a drivable road node near a point, with the road's
    /// heading" lookup, used anywhere something needs to land on a real road
    /// instead of a blind offset (scene placement, duty vehicle spawn).
    /// </summary>
    public static class RoadHelper
    {
        /// <summary>Resolves the nearest drivable road node to <paramref name="near"/>,
        /// falling back to World.GetNextPositionOnStreet, then to the raw point
        /// with heading 0, so callers always get something usable.</summary>
        public static void ResolveNearestRoad(Vector3 near, string context, out Vector3 position, out float heading)
        {
            Vector3 resultPos = near;
            float resultHeading = 0f;
            bool found = false;

            Safe.Run(() =>
            {
                Vector3 pos;
                float h;
                found = NativeFunction.Natives.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING<bool>(
                    near.X, near.Y, near.Z, out pos, out h, 1, 3.0f, 0);
                if (found)
                {
                    resultPos = pos;
                    resultHeading = h;
                }
            }, $"{context}.closest vehicle node w/ heading");

            if (!found)
            {
                resultPos = Safe.Run(() => World.GetNextPositionOnStreet(near), near, $"{context}.next position on street");
                resultHeading = 0f;
            }

            position = resultPos;
            heading = resultHeading;
        }
    }
}
