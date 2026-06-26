using System.Collections.Generic;
using UnityEngine;
using Core;
using Grid;

namespace Laser
{
    public class LaserSimulator : MonoBehaviour
    {
        private class LaserBeam
        {
            public int BeamId;
            public Vector2Int Position;
            public LaserDirection Direction;
            public LaserColorKind Color;
            public int RemainingDistance;

            public LaserBeam(int beamId, Vector2Int position, LaserDirection direction, LaserColorKind color, int remainingDistance)
            {
                BeamId = beamId;
                Position = position;
                Direction = direction;
                Color = color;
                RemainingDistance = remainingDistance;
            }
        }

        [Header("References")]
        [SerializeField] private GridManager gridManager;

        [Header("Step Limit")]
        [SerializeField] private int maxStepCountPerBeam = 100;
        [SerializeField] private int maxTotalBeamCount = 64;

        [Header("Distance Limit")]
        [SerializeField] private bool useDistanceLimit;
        [SerializeField] private int defaultMaxDistance = 20;

        [Header("Default State")]
        [SerializeField] private LaserColorKind defaultColor = LaserColorKind.Default;

        private readonly List<LaserDirection> splitterOutputs = new();

        public LaserResult Simulate(Vector2Int startPosition, GridDirection startDirection)
        {
            return Simulate(startPosition, LaserDirectionExtensions.FromGridDirection(startDirection));
        }

        public LaserResult Simulate(Vector2Int startPosition, LaserDirection startDirection)
        {
            LaserResult result = new LaserResult();

            if (gridManager == null)
                return result;

            Queue<LaserBeam> beamQueue = new();
            HashSet<LaserBeamState> visitedStates = new();

            int nextBeamId = 0;
            int remainingDistance = useDistanceLimit ? defaultMaxDistance : -1;

            beamQueue.Enqueue(new LaserBeam(nextBeamId++, startPosition, startDirection, defaultColor, remainingDistance));

            while (beamQueue.Count > 0)
            {
                if (nextBeamId > maxTotalBeamCount)
                    break;

                LaserBeam beam = beamQueue.Dequeue();
                SimulateBeam(result, beam, beamQueue, visitedStates, ref nextBeamId);
            }

            return result;
        }

        private bool TryDetectAdjacentTargetOnLaserEnd(LaserResult result, Vector2Int endPosition, LaserDirection endDirection, LaserColorKind color, int beamId)
        {
            Vector2Int forwardPosition = endPosition + endDirection.ToVector();

            if (TryReachTargetFromLaserEnd(result, endPosition, forwardPosition, endDirection, color, beamId))
                return true;

            Vector2Int upPosition = endPosition + Vector2Int.up;
            Vector2Int rightPosition = endPosition + Vector2Int.right;
            Vector2Int downPosition = endPosition + Vector2Int.down;
            Vector2Int leftPosition = endPosition + Vector2Int.left;

            if (TryReachTargetFromLaserEnd(result, endPosition, upPosition, endDirection, color, beamId))
                return true;

            if (TryReachTargetFromLaserEnd(result, endPosition, rightPosition, endDirection, color, beamId))
                return true;

            if (TryReachTargetFromLaserEnd(result, endPosition, downPosition, endDirection, color, beamId))
                return true;

            if (TryReachTargetFromLaserEnd(result, endPosition, leftPosition, endDirection, color, beamId))
                return true;

            return false;
        }

        private bool TryReachTargetFromLaserEnd(LaserResult result, Vector2Int endPosition, Vector2Int targetPosition, LaserDirection endDirection, LaserColorKind color, int beamId)
        {
            if (!gridManager.IsInside(targetPosition))
                return false;

            if (!gridManager.HasTarget(targetPosition))
                return false;

            result.AddTargetHit(new LaserTargetHit(targetPosition, color, beamId, result.TargetHits.Count));
            AddEndNode(result, endPosition, endDirection, color, beamId);
            result.SetReachedTarget(targetPosition);

            return true;
        }

        private void SimulateBeam(
            LaserResult result,
            LaserBeam beam,
            Queue<LaserBeam> beamQueue,
            HashSet<LaserBeamState> visitedStates,
            ref int nextBeamId)
        {
            Vector2Int currentPosition = beam.Position;
            LaserDirection currentDirection = beam.Direction;
            LaserColorKind currentColor = beam.Color;
            int remainingDistance = beam.RemainingDistance;

            result.AddNode(LaserPathNode.Start(currentPosition, currentDirection, currentColor, beam.BeamId));

            visitedStates.Add(new LaserBeamState(currentPosition, currentDirection, currentColor));

            for (int step = 0; step < maxStepCountPerBeam; step++)
            {
                if (useDistanceLimit && remainingDistance <= 0)
                {
                    if (step == 0)
                        result.AddNode(LaserPathNode.StartEnd(currentPosition, currentDirection, currentColor, beam.BeamId));
                    else
                        AddEndNode(result, currentPosition, currentDirection, currentColor, beam.BeamId);

                    result.SetDistanceEnded(currentPosition);
                    break;
                }

                Vector2Int nextPosition = currentPosition + currentDirection.ToVector();
                
                if (!gridManager.IsInside(nextPosition))
                {
                    if (step == 0)
                        result.AddNode(LaserPathNode.StartEnd(currentPosition, currentDirection, currentColor, beam.BeamId));
                    else
                        AddEndNode(result, currentPosition, currentDirection, currentColor, beam.BeamId);

                    result.SetHitWall(nextPosition);
                    break;
                }

                AddSegment(result, currentPosition, nextPosition, currentColor, beam.BeamId);

                if (useDistanceLimit)
                    remainingDistance--;

                LaserBeamState nextState = new LaserBeamState(nextPosition, currentDirection, currentColor);

                if (visitedStates.Contains(nextState))
                {
                    result.AddNode(LaserPathNode.Loop(nextPosition, currentDirection, currentColor, beam.BeamId));
                    AddEndNode(result, currentPosition, currentDirection, currentColor, beam.BeamId);
                    result.SetLoopDetected(nextPosition);
                    break;
                }

                visitedStates.Add(nextState);

                if (gridManager.HasWall(nextPosition))
                {
                    result.AddNode(LaserPathNode.Blocked(nextPosition, currentDirection, currentColor, beam.BeamId));

                    if (step == 0)
                        result.AddNode(LaserPathNode.StartEnd(currentPosition, currentDirection, currentColor, beam.BeamId));
                    else
                        AddEndNode(result, currentPosition, currentDirection, currentColor, beam.BeamId);

                    result.SetHitObjectAndStopped(nextPosition);
                    break;
                }

                if (gridManager.HasTarget(nextPosition))
                {
                    result.AddNode(LaserPathNode.Target(nextPosition, currentDirection, currentColor, beam.BeamId));
                    result.AddTargetHit(new LaserTargetHit(nextPosition, currentColor, beam.BeamId, result.TargetHits.Count));

                    GridTarget target = gridManager.GetTargetAt(nextPosition);
                    bool stopLaserOnHit = target == null || target.StopLaserOnHit;

                    if (stopLaserOnHit)
                    {
                        AddEndNode(result, currentPosition, currentDirection, currentColor, beam.BeamId);
                        result.SetReachedTarget(nextPosition);
                        break;
                    }

                    currentPosition = nextPosition;

                    if (useDistanceLimit && remainingDistance <= 1)
                    {
                        AddEndNode(result, currentPosition, currentDirection, currentColor, beam.BeamId);
                        result.SetDistanceEnded(currentPosition);
                        break;
                    }

                    continue;
                }

                GridObject gridObject = gridManager.GetObjectAt(nextPosition);

                if (gridObject != null)
                {
                    bool stopped = HandleObject(
                        result,
                        gridObject,
                        beamQueue,
                        ref nextBeamId,
                        beam.BeamId,
                        currentPosition,
                        nextPosition,
                        ref currentDirection,
                        ref currentColor,
                        ref remainingDistance
                    );

                    if (stopped)
                        break;

                    currentPosition = nextPosition;
                    continue;
                }

                result.AddNode(LaserPathNode.Straight(nextPosition, currentDirection, currentColor, beam.BeamId));
                currentPosition = nextPosition;
            }
        }

        private bool HandleObject(
            LaserResult result,
            GridObject gridObject,
            Queue<LaserBeam> beamQueue,
            ref int nextBeamId,
            int currentBeamId,
            Vector2Int currentPosition,
            Vector2Int objectPosition,
            ref LaserDirection currentDirection,
            ref LaserColorKind currentColor,
            ref int remainingDistance)
        {
            switch (gridObject.ObjectType)
            {
                case PuzzleObjectType.Mirror:
                    if (gridObject.TryReflectLaser(currentDirection, out LaserDirection reflectedDirection))
                    {
                        result.AddNode(LaserPathNode.Corner(objectPosition, currentDirection, reflectedDirection, currentColor, currentBeamId));
                        currentDirection = reflectedDirection;
                        return false;
                    }

                    result.AddNode(LaserPathNode.Blocked(objectPosition, currentDirection, currentColor, currentBeamId));
                    AddEndNode(result, currentPosition, currentDirection, currentColor, currentBeamId);
                    result.SetHitObjectAndStopped(objectPosition);
                    return true;

                case PuzzleObjectType.Prism:
                    return HandlePrism(result, gridObject, beamQueue, ref nextBeamId, currentBeamId, currentPosition, objectPosition, ref currentDirection, ref currentColor, remainingDistance);

                case PuzzleObjectType.Lens:
                    if (gridObject.LensType == LensType.DistanceAmplifier && remainingDistance >= 0)
                        remainingDistance += gridObject.DistanceBoost;

                    result.AddNode(LaserPathNode.Straight(objectPosition, currentDirection, currentColor, currentBeamId));
                    return false;

                default:
                    result.AddNode(LaserPathNode.Blocked(objectPosition, currentDirection, currentColor, currentBeamId));
                    AddEndNode(result, currentPosition, currentDirection, currentColor, currentBeamId);
                    result.SetHitObjectAndStopped(objectPosition);
                    return true;
            }
        }

        private bool HandlePrism(
            LaserResult result,
            GridObject prism,
            Queue<LaserBeam> beamQueue,
            ref int nextBeamId,
            int currentBeamId,
            Vector2Int currentPosition,
            Vector2Int prismPosition,
            ref LaserDirection currentDirection,
            ref LaserColorKind currentColor,
            int remainingDistance)
        {
            if (prism.PrismType == PrismType.Splitter)
            {
                prism.GetSplitterOutputDirections(currentDirection, splitterOutputs);

                if (splitterOutputs.Count <= 0)
                {
                    result.AddNode(LaserPathNode.Blocked(prismPosition, currentDirection, currentColor, currentBeamId));
                    AddEndNode(result, currentPosition, currentDirection, currentColor, currentBeamId);
                    result.SetHitObjectAndStopped(prismPosition);
                    return true;
                }

                result.AddNode(LaserPathNode.Splitter(prismPosition, currentDirection, splitterOutputs[0], currentColor, currentBeamId));

                for (int i = 0; i < splitterOutputs.Count; i++)
                {
                    if (nextBeamId >= maxTotalBeamCount)
                        break;

                    int beamId = nextBeamId++;
                    beamQueue.Enqueue(new LaserBeam(beamId, prismPosition, splitterOutputs[i], currentColor, remainingDistance));
                }

                // 현재 빔은 여기서 종료되고, 분기 큐의 빔들이 이어서 진행한다.
                return true;
            }

            if (prism.PrismType == PrismType.Color)
            {
                currentColor = prism.ApplyColorPrism(currentColor);
                result.AddNode(LaserPathNode.Straight(prismPosition, currentDirection, currentColor, currentBeamId));
                return false;
            }

            if (prism.PrismType == PrismType.Refraction)
            {
                LaserDirection refracted = prism.ApplyRefractionPrism(currentDirection);
                result.AddNode(LaserPathNode.Corner(prismPosition, currentDirection, refracted, currentColor, currentBeamId));
                currentDirection = refracted;
                return false;
            }

            result.AddNode(LaserPathNode.Blocked(prismPosition, currentDirection, currentColor, currentBeamId));
            AddEndNode(result, currentPosition, currentDirection, currentColor, currentBeamId);
            result.SetHitObjectAndStopped(prismPosition);
            return true;
        }

        private void AddSegment(LaserResult result, Vector2Int start, Vector2Int end, LaserColorKind color, int beamId)
        {
            result.AddSegment(new LaserSegment(
                new Vector2(start.x, start.y),
                new Vector2(end.x, end.y),
                color,
                beamId
            ));
        }

        private void AddStartOrStartEnd(LaserResult result, Vector2Int position, LaserDirection direction, LaserColorKind color, int beamId, bool isImmediateEnd)
        {
            if (isImmediateEnd)
                result.AddNode(LaserPathNode.StartEnd(position, direction, color, beamId));
            else
                result.AddNode(LaserPathNode.Start(position, direction, color, beamId));
        }

        private void AddEndNode(LaserResult result, Vector2Int position, LaserDirection incomingDirection, LaserColorKind color, int beamId)
        {
            int index = FindLastNodeIndexAtPosition(result, position, beamId);

            if (index < 0)
            {
                result.AddNode(LaserPathNode.End(position, incomingDirection, color, beamId));
                return;
            }

            LaserPathNode lastNodeAtPosition = result.PathNodes[index];

            if (lastNodeAtPosition.NodeType == LaserPathNodeType.Corner)
            {
                result.PathNodes[index] = LaserPathNode.CornerEnd(
                    lastNodeAtPosition.Position,
                    lastNodeAtPosition.IncomingDirection,
                    lastNodeAtPosition.OutgoingDirection,
                    lastNodeAtPosition.Color,
                    lastNodeAtPosition.BeamId
                );

                return;
            }

            if (lastNodeAtPosition.NodeType == LaserPathNodeType.Straight ||
                lastNodeAtPosition.NodeType == LaserPathNodeType.Start)
            {
                result.PathNodes[index] = LaserPathNode.End(
                    lastNodeAtPosition.Position,
                    incomingDirection,
                    color,
                    beamId
                );

                return;
            }

            if (lastNodeAtPosition.NodeType == LaserPathNodeType.End ||
                lastNodeAtPosition.NodeType == LaserPathNodeType.CornerEnd)
            {
                return;
            }

            result.AddNode(LaserPathNode.End(position, incomingDirection, color, beamId));
        }

        private int FindLastNodeIndexAtPosition(LaserResult result, Vector2Int position, int beamId)
        {
            for (int i = result.PathNodes.Count - 1; i >= 0; i--)
            {
                LaserPathNode node = result.PathNodes[i];

                if (node.Position == position && node.BeamId == beamId)
                    return i;
            }

            return -1;
        }
    }
}
