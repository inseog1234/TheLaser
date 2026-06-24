using System.Collections.Generic;
using UnityEngine;

namespace Laser
{
    public class LaserResult
    {
        public readonly List<LaserPathNode> PathNodes = new();

        public bool ReachedTarget { get; private set; }
        public bool HitWall { get; private set; }
        public bool HitObjectAndStopped { get; private set; }
        public bool LoopDetected { get; private set; }

        public Vector2Int? TargetPosition { get; private set; }
        public Vector2Int? StopPosition { get; private set; }

        public void AddNode(LaserPathNode node)
        {
            PathNodes.Add(node);
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
    }
}