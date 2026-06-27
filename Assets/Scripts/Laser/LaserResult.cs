using System.Collections.Generic;
using UnityEngine;

namespace Laser
{
    public class LaserResult
    {
        public readonly List<LaserPathNode> PathNodes = new();
        public readonly List<LaserSegment> Segments = new();
        public readonly List<LaserTargetHit> TargetHits = new();
        private readonly Dictionary<int, int> beamStepCounts = new();

        public int MaxBeamStepCount { get; private set; }
        public int TotalStepCount { get; private set; }

        public bool ReachedTarget { get; private set; }
        public bool HitWall { get; private set; }
        public bool HitObjectAndStopped { get; private set; }
        public bool LoopDetected { get; private set; }
        public bool DistanceEnded { get; private set; }

        public Vector2Int? TargetPosition { get; private set; }
        public Vector2Int? StopPosition { get; private set; }

        public void AddNode(LaserPathNode node)
        {
            PathNodes.Add(node);
        }

        public void AddSegment(LaserSegment segment)
        {
            Segments.Add(segment);
            int stepCost = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(Mathf.Abs(segment.End.x - segment.Start.x), Mathf.Abs(segment.End.y - segment.Start.y))));
            TotalStepCount += stepCost;
            if (!beamStepCounts.ContainsKey(segment.BeamId))
                beamStepCounts.Add(segment.BeamId, 0);
            beamStepCounts[segment.BeamId] += stepCost;
            if (beamStepCounts[segment.BeamId] > MaxBeamStepCount)
                MaxBeamStepCount = beamStepCounts[segment.BeamId];
        }

        public void AddTargetHit(LaserTargetHit hit)
        {
            TargetHits.Add(hit);
            ReachedTarget = true;
            TargetPosition = hit.Position;
        }

        public void SetReachedTarget(Vector2Int position)
        {
            ReachedTarget = true;
            TargetPosition = position;
            StopPosition = position;
        }

        public void SetHitWall(Vector2Int position)
        {
            HitWall = true;
            StopPosition = position;
        }

        public void SetHitObjectAndStopped(Vector2Int position)
        {
            HitObjectAndStopped = true;
            StopPosition = position;
        }

        public void SetLoopDetected(Vector2Int position)
        {
            LoopDetected = true;
            StopPosition = position;
        }

        public void SetDistanceEnded(Vector2Int position)
        {
            DistanceEnded = true;
            StopPosition = position;
        }
    }
}
