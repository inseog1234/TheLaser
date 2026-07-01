using System.Collections.Generic;
using Core;
using UnityEngine;

namespace Laser
{
    public static class LaserSimulationRules
    {
        public const int DefaultMaxTotalBeamCount = 64;
        public const int DefaultRuntimeMaxStepCountPerBeam = 100;
        public const int DefaultSolverMaxStepCountPerBeam = 160;

        public static bool IsDistanceLimitEnabled(bool useDistanceLimit, int maxDistance)
        {
            return useDistanceLimit && maxDistance > 0;
        }

        public static int ResolveInitialRemainingDistance(bool useDistanceLimit, int maxDistance)
        {
            return IsDistanceLimitEnabled(useDistanceLimit, maxDistance) ? maxDistance : -1;
        }

        public static LaserBeamState CreateVisitedState(Vector2Int position, LaserDirection direction, LaserColorKind color, int remainingDistance)
        {
            return new LaserBeamState(position, direction, color, remainingDistance);
        }

        public static bool TryRegisterVisitedState(
            HashSet<LaserBeamState> visitedStates,
            Vector2Int position,
            LaserDirection direction,
            LaserColorKind color,
            int remainingDistance)
        {
            if (visitedStates == null)
                return true;

            return visitedStates.Add(CreateVisitedState(position, direction, color, remainingDistance));
        }
    }
}
