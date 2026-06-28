using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Core
{
    public static class StageAutoSolver
    {
        private enum AutoActionKind
        {
            Move = 0,
            RotateClockwise = 1,
            RotateCounterClockwise = 2
        }

        private struct AutoAction
        {
            public AutoActionKind Kind;
            public GridDirection Direction;

            public StageSolutionActionData ToSolutionAction()
            {
                return new StageSolutionActionData
                {
                    actionType = Kind switch
                    {
                        AutoActionKind.RotateClockwise => StageSolutionActionType.RotateClockwise,
                        AutoActionKind.RotateCounterClockwise => StageSolutionActionType.RotateCounterClockwise,
                        _ => StageSolutionActionType.Move
                    },
                    direction = Direction
                };
            }
        }

        private sealed class ObjectSnapshot
        {
            public PuzzleObjectType ObjectType;
            public ManipulationType ManipulationType;
            public Vector2Int Position;
            public GridDirection Direction;
            public MirrorShape MirrorShape;
            public PrismType PrismType;
            public PrismSplitterMode SplitterMode;
            public LaserColorKind PrismColor;
            public RefractionMode RefractionMode;
            public LensType LensType;
            public int DistanceBoost;

            public ObjectSnapshot Clone()
            {
                return new ObjectSnapshot
                {
                    ObjectType = ObjectType,
                    ManipulationType = ManipulationType,
                    Position = Position,
                    Direction = Direction,
                    MirrorShape = MirrorShape,
                    PrismType = PrismType,
                    SplitterMode = SplitterMode,
                    PrismColor = PrismColor,
                    RefractionMode = RefractionMode,
                    LensType = LensType,
                    DistanceBoost = DistanceBoost
                };
            }
        }

        private sealed class SolverState
        {
            public Vector2Int PlayerPosition;
            public GridDirection FacingDirection;
            public ObjectSnapshot[] Objects;

            public SolverState Clone()
            {
                ObjectSnapshot[] clonedObjects = new ObjectSnapshot[Objects.Length];
                for (int i = 0; i < Objects.Length; i++)
                    clonedObjects[i] = Objects[i].Clone();

                return new SolverState
                {
                    PlayerPosition = PlayerPosition,
                    FacingDirection = FacingDirection,
                    Objects = clonedObjects
                };
            }
        }

        private sealed class SolverNode
        {
            public SolverState State;
            public int ParentIndex;
            public AutoAction Action;
            public int Depth;
        }

        private struct BeamState
        {
            public Vector2Int Position;
            public LaserDirection Direction;
            public LaserColorKind Color;
            public int RemainingDistance;
            public int StartStepCount;
            public int BeamId;
        }

        private struct TargetHit
        {
            public Vector2Int Position;
            public LaserColorKind Color;
            public int BeamId;
            public int HitIndex;
        }

        private struct Segment
        {
            public Vector2Int Start;
            public Vector2Int End;
            public LaserColorKind Color;
            public int BeamId;
        }

        private sealed class LaserSolveResult
        {
            public readonly List<TargetHit> TargetHits = new();
            public readonly List<Segment> Segments = new();
            public bool HasExactDistanceTargetHit;
            public int MaxBeamStepCount;
        }

        private static readonly GridDirection[] MoveDirections =
        {
            GridDirection.Up,
            GridDirection.Right,
            GridDirection.Down,
            GridDirection.Left
        };

        private static readonly Vector2Int[] EightDirectionVectors =
        {
            Vector2Int.up,
            new Vector2Int(1, 1),
            Vector2Int.right,
            new Vector2Int(1, -1),
            Vector2Int.down,
            new Vector2Int(-1, -1),
            Vector2Int.left,
            new Vector2Int(-1, 1)
        };

        public static bool TrySolve(StageData stageData, out List<StageSolutionActionData> solutionActions, out string message, int maxNodeCount = 120000, int maxActionCount = 80)
        {
            solutionActions = new List<StageSolutionActionData>();
            message = string.Empty;

            if (stageData == null)
            {
                message = "스테이지 데이터가 없습니다.";
                return false;
            }

            if (stageData.width <= 0 || stageData.height <= 0)
            {
                message = "맵 크기가 올바르지 않습니다.";
                return false;
            }

            int moveLimit = Mathf.Max(0, stageData.moveLimit);
            if (moveLimit > 0)
                maxActionCount = Mathf.Min(maxActionCount, moveLimit);

            SolverState startState = CreateInitialState(stageData);

            if (IsSolvedByLaser(stageData, startState))
            {
                message = "현재 시작 상태에서 바로 클리어 가능합니다.";
                return true;
            }

            List<SolverNode> nodes = new List<SolverNode>(4096)
            {
                new SolverNode
                {
                    State = startState,
                    ParentIndex = -1,
                    Depth = 0,
                    Action = default
                }
            };

            Queue<int> queue = new Queue<int>();
            queue.Enqueue(0);

            HashSet<string> visited = new HashSet<string>();
            visited.Add(BuildStateKey(startState));

            while (queue.Count > 0)
            {
                int nodeIndex = queue.Dequeue();
                SolverNode node = nodes[nodeIndex];

                if (node.Depth >= maxActionCount)
                    continue;

                for (int i = 0; i < MoveDirections.Length; i++)
                {
                    AutoAction action = new AutoAction
                    {
                        Kind = AutoActionKind.Move,
                        Direction = MoveDirections[i]
                    };

                    TryExpandAction(stageData, nodes, queue, visited, nodeIndex, node, action, maxNodeCount, out bool nodeLimitReached, out int solvedNodeIndex);
                    if (solvedNodeIndex >= 0)
                    {
                        solutionActions = BuildActionList(nodes, solvedNodeIndex);
                        message = $"AI 풀이 발견: {solutionActions.Count} 행동";
                        return true;
                    }

                    if (nodeLimitReached)
                    {
                        message = $"AI 탐색 실패: 탐색 노드 제한({maxNodeCount})에 도달했습니다.";
                        return false;
                    }
                }

                AutoAction rotateClockwise = new AutoAction
                {
                    Kind = AutoActionKind.RotateClockwise,
                    Direction = node.State.FacingDirection
                };
                TryExpandAction(stageData, nodes, queue, visited, nodeIndex, node, rotateClockwise, maxNodeCount, out bool clockwiseLimitReached, out int clockwiseSolvedIndex);
                if (clockwiseSolvedIndex >= 0)
                {
                    solutionActions = BuildActionList(nodes, clockwiseSolvedIndex);
                    message = $"AI 풀이 발견: {solutionActions.Count} 행동";
                    return true;
                }

                if (clockwiseLimitReached)
                {
                    message = $"AI 탐색 실패: 탐색 노드 제한({maxNodeCount})에 도달했습니다.";
                    return false;
                }

                AutoAction rotateCounterClockwise = new AutoAction
                {
                    Kind = AutoActionKind.RotateCounterClockwise,
                    Direction = node.State.FacingDirection
                };
                TryExpandAction(stageData, nodes, queue, visited, nodeIndex, node, rotateCounterClockwise, maxNodeCount, out bool counterLimitReached, out int counterSolvedIndex);
                if (counterSolvedIndex >= 0)
                {
                    solutionActions = BuildActionList(nodes, counterSolvedIndex);
                    message = $"AI 풀이 발견: {solutionActions.Count} 행동";
                    return true;
                }

                if (counterLimitReached)
                {
                    message = $"AI 탐색 실패: 탐색 노드 제한({maxNodeCount})에 도달했습니다.";
                    return false;
                }
            }

            message = $"AI 탐색 실패: {visited.Count}개 상태를 확인했지만 답을 찾지 못했습니다.";
            return false;
        }

        private static void TryExpandAction(
            StageData stageData,
            List<SolverNode> nodes,
            Queue<int> queue,
            HashSet<string> visited,
            int parentIndex,
            SolverNode parentNode,
            AutoAction action,
            int maxNodeCount,
            out bool nodeLimitReached,
            out int solvedNodeIndex)
        {
            nodeLimitReached = false;
            solvedNodeIndex = -1;

            if (!TryApplyAction(stageData, parentNode.State, action, out SolverState nextState))
                return;

            string key = BuildStateKey(nextState);
            if (!visited.Add(key))
                return;

            if (nodes.Count >= maxNodeCount)
            {
                nodeLimitReached = true;
                return;
            }

            SolverNode nextNode = new SolverNode
            {
                State = nextState,
                ParentIndex = parentIndex,
                Action = action,
                Depth = parentNode.Depth + 1
            };

            nodes.Add(nextNode);
            int nextIndex = nodes.Count - 1;

            if (IsSolvedByLaser(stageData, nextState))
            {
                solvedNodeIndex = nextIndex;
                return;
            }

            queue.Enqueue(nextIndex);
        }

        private static SolverState CreateInitialState(StageData stageData)
        {
            List<ObjectSnapshot> objects = new List<ObjectSnapshot>();

            if (stageData.objects != null)
            {
                for (int i = 0; i < stageData.objects.Count; i++)
                {
                    StageObjectData source = stageData.objects[i];
                    if (source == null)
                        continue;

                    objects.Add(new ObjectSnapshot
                    {
                        ObjectType = source.objectType,
                        ManipulationType = source.manipulationType,
                        Position = source.position,
                        Direction = source.direction,
                        MirrorShape = source.mirrorShape,
                        PrismType = source.prismType,
                        SplitterMode = source.splitterMode,
                        PrismColor = source.prismColor,
                        RefractionMode = source.refractionMode,
                        LensType = source.lensType,
                        DistanceBoost = source.distanceBoost
                    });
                }
            }

            return new SolverState
            {
                PlayerPosition = stageData.playerStartPosition,
                FacingDirection = stageData.playerStartDirection,
                Objects = objects.ToArray()
            };
        }

        private static bool TryApplyAction(StageData stageData, SolverState state, AutoAction action, out SolverState nextState)
        {
            nextState = state.Clone();

            if (action.Kind == AutoActionKind.Move)
                return TryApplyMove(stageData, state, nextState, action.Direction);

            bool clockwise = action.Kind == AutoActionKind.RotateClockwise;
            return TryApplyRotate(state, nextState, clockwise);
        }

        private static bool TryApplyMove(StageData stageData, SolverState sourceState, SolverState nextState, GridDirection direction)
        {
            bool changed = sourceState.FacingDirection != direction;
            nextState.FacingDirection = direction;

            Vector2Int targetPosition = sourceState.PlayerPosition + direction.ToVector();

            if (IsWalkable(stageData, sourceState, targetPosition))
            {
                nextState.PlayerPosition = targetPosition;
                return true;
            }

            int objectIndex = FindObjectIndexAt(sourceState, targetPosition);
            if (objectIndex >= 0 && CanPush(sourceState.Objects[objectIndex]))
            {
                Vector2Int objectNextPosition = targetPosition + direction.ToVector();
                if (IsEmpty(stageData, sourceState, objectNextPosition))
                {
                    nextState.Objects[objectIndex].Position = objectNextPosition;
                    nextState.PlayerPosition = targetPosition;
                    return true;
                }
            }

            return changed;
        }

        private static bool TryApplyRotate(SolverState sourceState, SolverState nextState, bool clockwise)
        {
            Vector2Int objectPosition = sourceState.PlayerPosition + sourceState.FacingDirection.ToVector();
            int objectIndex = FindObjectIndexAt(sourceState, objectPosition);

            if (objectIndex < 0)
                return false;

            ObjectSnapshot targetObject = sourceState.Objects[objectIndex];
            if (!CanRotate(targetObject))
                return false;

            nextState.Objects[objectIndex].Direction = clockwise
                ? targetObject.Direction.RotateClockwise()
                : targetObject.Direction.RotateCounterClockwise();
            return true;
        }

        private static bool IsWalkable(StageData stageData, SolverState state, Vector2Int position)
        {
            if (!stageData.IsInside(position))
                return false;

            if (HasWall(stageData, position))
                return false;

            if (FindObjectIndexAt(state, position) >= 0)
                return false;

            return true;
        }

        private static bool IsEmpty(StageData stageData, SolverState state, Vector2Int position)
        {
            if (!stageData.IsInside(position))
                return false;

            if (HasWall(stageData, position))
                return false;

            if (FindObjectIndexAt(state, position) >= 0)
                return false;

            return true;
        }

        private static bool HasWall(StageData stageData, Vector2Int position)
        {
            return stageData.wallPositions != null && stageData.wallPositions.Contains(position);
        }

        private static int FindObjectIndexAt(SolverState state, Vector2Int position)
        {
            for (int i = 0; i < state.Objects.Length; i++)
            {
                if (state.Objects[i].Position == position)
                    return i;
            }

            return -1;
        }

        private static ObjectSnapshot GetObjectAt(SolverState state, Vector2Int position)
        {
            int index = FindObjectIndexAt(state, position);
            return index >= 0 ? state.Objects[index] : null;
        }

        private static bool CanPush(ObjectSnapshot obj)
        {
            return obj.ManipulationType == ManipulationType.PushOnly ||
                   obj.ManipulationType == ManipulationType.PushAndRotate;
        }

        private static bool CanRotate(ObjectSnapshot obj)
        {
            return obj.ManipulationType == ManipulationType.RotateOnly ||
                   obj.ManipulationType == ManipulationType.PushAndRotate;
        }

        private static bool IsSolvedByLaser(StageData stageData, SolverState state)
        {
            LaserSolveResult result = SimulateLaser(stageData, state, state.PlayerPosition, state.FacingDirection);
            return AreAllTargetsActivated(stageData, result) && IsLaserDistanceExactlyMatched(stageData, result);
        }

        private static LaserSolveResult SimulateLaser(StageData stageData, SolverState state, Vector2Int startPosition, GridDirection startDirection)
        {
            LaserSolveResult result = new LaserSolveResult();
            Queue<BeamState> beamQueue = new Queue<BeamState>();
            HashSet<string> visitedStates = new HashSet<string>();
            Dictionary<int, int> beamStepCounts = new Dictionary<int, int>();

            int maxDistance = stageData.useLaserDistanceLimit && stageData.laserMaxDistance > 0 ? stageData.laserMaxDistance : -1;
            int nextBeamId = 0;
            beamQueue.Enqueue(new BeamState
            {
                BeamId = nextBeamId++,
                Position = startPosition,
                Direction = LaserDirectionExtensions.FromGridDirection(startDirection),
                Color = LaserColorKind.Default,
                RemainingDistance = maxDistance,
                StartStepCount = 0
            });

            while (beamQueue.Count > 0)
            {
                if (nextBeamId > 64)
                    break;

                BeamState beam = beamQueue.Dequeue();
                SimulateBeam(stageData, state, result, beamQueue, visitedStates, beamStepCounts, beam, ref nextBeamId);
            }

            foreach (KeyValuePair<int, int> pair in beamStepCounts)
                result.MaxBeamStepCount = Mathf.Max(result.MaxBeamStepCount, pair.Value);

            return result;
        }

        private static void SimulateBeam(
            StageData stageData,
            SolverState state,
            LaserSolveResult result,
            Queue<BeamState> beamQueue,
            HashSet<string> visitedStates,
            Dictionary<int, int> beamStepCounts,
            BeamState beam,
            ref int nextBeamId)
        {
            Vector2Int currentPosition = beam.Position;
            LaserDirection currentDirection = beam.Direction;
            LaserColorKind currentColor = beam.Color;
            int remainingDistance = beam.RemainingDistance;
            int stepCount = beam.StartStepCount;

            AddBeamStepCount(beamStepCounts, beam.BeamId, stepCount);

            for (int step = 0; step < 160; step++)
            {
                if (remainingDistance == 0)
                    break;

                Vector2Int nextPosition = currentPosition + currentDirection.ToVector();
                stepCount++;
                AddBeamStepCount(beamStepCounts, beam.BeamId, stepCount);

                if (!stageData.IsInside(nextPosition))
                    break;

                result.Segments.Add(new Segment
                {
                    Start = currentPosition,
                    End = nextPosition,
                    Color = currentColor,
                    BeamId = beam.BeamId
                });

                if (remainingDistance > 0)
                    remainingDistance--;

                string stateKey = $"{nextPosition.x},{nextPosition.y},{(int)currentDirection},{(int)currentColor}";
                if (!visitedStates.Add(stateKey))
                    break;

                if (HasWall(stageData, nextPosition))
                    break;

                if (HasTarget(stageData, nextPosition))
                {
                    TargetHit hit = new TargetHit
                    {
                        Position = nextPosition,
                        Color = currentColor,
                        BeamId = beam.BeamId,
                        HitIndex = result.TargetHits.Count
                    };
                    result.TargetHits.Add(hit);

                    if (remainingDistance == 0 && stageData.useLaserDistanceLimit && stageData.laserMaxDistance > 0)
                        result.HasExactDistanceTargetHit = true;

                    StageTargetData target = GetTargetData(stageData, nextPosition);
                    bool stopLaserOnHit = target == null || target.stopLaserOnHit;
                    if (stopLaserOnHit)
                        break;

                    currentPosition = nextPosition;
                    continue;
                }

                ObjectSnapshot obj = GetObjectAt(state, nextPosition);
                if (obj != null)
                {
                    if (HandleLaserObject(stageData, state, result, beamQueue, beamStepCounts, obj, currentPosition, nextPosition, ref currentDirection, ref currentColor, ref remainingDistance, ref nextBeamId, beam.BeamId, stepCount))
                        break;

                    currentPosition = nextPosition;
                    continue;
                }

                currentPosition = nextPosition;
            }
        }

        private static bool HandleLaserObject(
            StageData stageData,
            SolverState state,
            LaserSolveResult result,
            Queue<BeamState> beamQueue,
            Dictionary<int, int> beamStepCounts,
            ObjectSnapshot obj,
            Vector2Int currentPosition,
            Vector2Int objectPosition,
            ref LaserDirection currentDirection,
            ref LaserColorKind currentColor,
            ref int remainingDistance,
            ref int nextBeamId,
            int currentBeamId,
            int currentStepCount)
        {
            switch (obj.ObjectType)
            {
                case PuzzleObjectType.Mirror:
                    if (TryReflectLaser(obj, currentDirection, out LaserDirection reflectedDirection))
                    {
                        currentDirection = reflectedDirection;
                        return false;
                    }
                    return true;

                case PuzzleObjectType.Prism:
                    if (obj.PrismType == PrismType.Color)
                    {
                        currentColor = obj.PrismColor;
                        return false;
                    }

                    if (obj.PrismType == PrismType.Refraction)
                    {
                        currentDirection = obj.RefractionMode == RefractionMode.Clockwise45
                            ? currentDirection.RotateClockwise45()
                            : currentDirection.RotateCounterClockwise45();
                        return false;
                    }

                    if (obj.PrismType == PrismType.Splitter)
                    {
                        List<LaserDirection> outputs = GetSplitterOutputs(obj, currentDirection);
                        for (int i = 0; i < outputs.Count; i++)
                        {
                            if (nextBeamId >= 64)
                                break;

                            int beamId = nextBeamId++;
                            AddBeamStepCount(beamStepCounts, beamId, currentStepCount);
                            beamQueue.Enqueue(new BeamState
                            {
                                BeamId = beamId,
                                Position = objectPosition,
                                Direction = outputs[i],
                                Color = currentColor,
                                RemainingDistance = remainingDistance,
                                StartStepCount = currentStepCount
                            });
                        }
                        return true;
                    }
                    return true;

                case PuzzleObjectType.Lens:
                    if (obj.LensType == LensType.DistanceAmplifier && remainingDistance >= 0)
                        remainingDistance += Mathf.Max(0, obj.DistanceBoost);
                    return false;

                default:
                    return true;
            }
        }

        private static bool TryReflectLaser(ObjectSnapshot obj, LaserDirection laserMoveDirection, out LaserDirection reflectedDirection)
        {
            reflectedDirection = laserMoveDirection;

            if (!TryLaserToGridDirection(laserMoveDirection, out GridDirection gridMoveDirection))
                return false;

            GridDirection entrySide = gridMoveDirection.Opposite();
            GetMirrorOpenSides(obj.MirrorShape, obj.Direction, out GridDirection sideA, out GridDirection sideB);

            if (entrySide == sideA)
            {
                reflectedDirection = LaserDirectionExtensions.FromGridDirection(sideB);
                return true;
            }

            if (entrySide == sideB)
            {
                reflectedDirection = LaserDirectionExtensions.FromGridDirection(sideA);
                return true;
            }

            return false;
        }

        private static bool TryLaserToGridDirection(LaserDirection laserDirection, out GridDirection gridDirection)
        {
            switch (laserDirection)
            {
                case LaserDirection.Up:
                    gridDirection = GridDirection.Up;
                    return true;
                case LaserDirection.Right:
                    gridDirection = GridDirection.Right;
                    return true;
                case LaserDirection.Down:
                    gridDirection = GridDirection.Down;
                    return true;
                case LaserDirection.Left:
                    gridDirection = GridDirection.Left;
                    return true;
                default:
                    gridDirection = GridDirection.Up;
                    return false;
            }
        }

        private static void GetMirrorOpenSides(MirrorShape shape, GridDirection mirrorDirection, out GridDirection sideA, out GridDirection sideB)
        {
            if (shape == MirrorShape.NormalL)
            {
                sideA = GridDirection.Up;
                sideB = GridDirection.Right;
            }
            else
            {
                sideA = GridDirection.Up;
                sideB = GridDirection.Left;
            }

            sideA = RotateGridDirection(sideA, mirrorDirection);
            sideB = RotateGridDirection(sideB, mirrorDirection);
        }

        private static GridDirection RotateGridDirection(GridDirection side, GridDirection mirrorDirection)
        {
            int rotateCount = mirrorDirection switch
            {
                GridDirection.Up => 0,
                GridDirection.Right => 1,
                GridDirection.Down => 2,
                GridDirection.Left => 3,
                _ => 0
            };

            GridDirection result = side;
            for (int i = 0; i < rotateCount; i++)
                result = result.RotateClockwise();
            return result;
        }

        private static List<LaserDirection> GetSplitterOutputs(ObjectSnapshot obj, LaserDirection inputDirection)
        {
            List<LaserDirection> outputs = new List<LaserDirection>();
            LaserDirection left = inputDirection.RotateCounterClockwise90();
            LaserDirection right = inputDirection.RotateClockwise90();

            switch (obj.SplitterMode)
            {
                case PrismSplitterMode.ForwardAndLeft:
                    outputs.Add(inputDirection);
                    outputs.Add(left);
                    break;
                case PrismSplitterMode.ForwardAndRight:
                    outputs.Add(inputDirection);
                    outputs.Add(right);
                    break;
                case PrismSplitterMode.ForwardLeftRight:
                    outputs.Add(inputDirection);
                    outputs.Add(left);
                    outputs.Add(right);
                    break;
                case PrismSplitterMode.LeftAndRight:
                    outputs.Add(left);
                    outputs.Add(right);
                    break;
            }

            return outputs;
        }

        private static bool HasTarget(StageData stageData, Vector2Int position)
        {
            if (stageData.targetPositions != null && stageData.targetPositions.Contains(position))
                return true;

            return GetTargetData(stageData, position) != null;
        }

        private static StageTargetData GetTargetData(StageData stageData, Vector2Int position)
        {
            if (stageData.advancedTargets == null)
                return null;

            for (int i = 0; i < stageData.advancedTargets.Count; i++)
            {
                StageTargetData target = stageData.advancedTargets[i];
                if (target != null && target.position == position)
                    return target;
            }

            return null;
        }

        private static bool AreAllTargetsActivated(StageData stageData, LaserSolveResult result)
        {
            List<StageTargetData> allTargets = new List<StageTargetData>();

            if (stageData.targetPositions != null)
            {
                for (int i = 0; i < stageData.targetPositions.Count; i++)
                    allTargets.Add(new StageTargetData { position = stageData.targetPositions[i], targetType = TargetType.Normal });
            }

            if (stageData.advancedTargets != null)
            {
                for (int i = 0; i < stageData.advancedTargets.Count; i++)
                {
                    if (stageData.advancedTargets[i] != null)
                        allTargets.Add(stageData.advancedTargets[i]);
                }
            }

            if (allTargets.Count <= 0)
                return false;

            bool hasSequence = stageData.sequenceLockPattern != null && stageData.sequenceLockPattern.Count > 0;
            bool sequenceMatched = !hasSequence || IsSequenceMatched(stageData, result);

            for (int i = 0; i < allTargets.Count; i++)
            {
                StageTargetData target = allTargets[i];

                switch (target.targetType)
                {
                    case TargetType.Normal:
                        if (!HasTargetHit(result, target.position, null))
                            return false;
                        break;

                    case TargetType.ColorLocked:
                        if (!HasTargetHit(result, target.position, target.requiredColor))
                            return false;
                        break;

                    case TargetType.SequenceLocked:
                    case TargetType.SequenceColorLocked:
                        if (!sequenceMatched)
                            return false;
                        break;

                    case TargetType.Intersection:
                        if (!IsIntersectionTargetActivated(target, result))
                            return false;
                        break;
                }
            }

            return true;
        }

        private static bool HasTargetHit(LaserSolveResult result, Vector2Int position, LaserColorKind? color)
        {
            for (int i = 0; i < result.TargetHits.Count; i++)
            {
                TargetHit hit = result.TargetHits[i];
                if (hit.Position != position)
                    continue;

                if (!color.HasValue || color.Value == hit.Color)
                    return true;
            }

            return false;
        }

        private static bool IsSequenceMatched(StageData stageData, LaserSolveResult result)
        {
            List<StageTargetData> sequenceTargets = new List<StageTargetData>();
            List<TargetHit> sequenceHits = new List<TargetHit>();

            for (int i = 0; i < result.TargetHits.Count; i++)
            {
                TargetHit hit = result.TargetHits[i];
                StageTargetData target = GetTargetData(stageData, hit.Position);
                if (target == null)
                    continue;

                if (target.targetType == TargetType.SequenceLocked || target.targetType == TargetType.SequenceColorLocked)
                {
                    sequenceTargets.Add(target);
                    sequenceHits.Add(hit);
                }
            }

            if (sequenceTargets.Count < stageData.sequenceLockPattern.Count)
                return false;

            for (int i = 0; i < stageData.sequenceLockPattern.Count; i++)
            {
                StageTargetData target = sequenceTargets[i];
                TargetHit hit = sequenceHits[i];

                if (target.sequenceValue != stageData.sequenceLockPattern[i])
                    return false;

                if (target.targetType == TargetType.SequenceColorLocked && target.requiredColor != hit.Color)
                    return false;
            }

            return true;
        }

        private static bool IsIntersectionTargetActivated(StageTargetData target, LaserSolveResult result)
        {
            Vector2 targetPoint = new Vector2(target.position.x, target.position.y);
            int requiredCount = Mathf.Clamp(target.requiredIntersectionCount, 2, 3);
            int matchedCount = 0;

            for (int a = 0; a < result.Segments.Count; a++)
            {
                for (int b = a + 1; b < result.Segments.Count; b++)
                {
                    Segment segmentA = result.Segments[a];
                    Segment segmentB = result.Segments[b];
                    if (segmentA.BeamId == segmentB.BeamId)
                        continue;

                    if (!TryGetSegmentIntersection(segmentA.Start, segmentA.End, segmentB.Start, segmentB.End, out Vector2 intersection))
                        continue;

                    if (Vector2.Distance(targetPoint, intersection) > target.detectionRadius)
                        continue;

                    if (!IsIntersectionColorMatched(target, segmentA.Color, segmentB.Color))
                        continue;

                    matchedCount++;
                    if (matchedCount >= requiredCount - 1)
                        return true;
                }
            }

            return false;
        }

        private static bool TryGetSegmentIntersection(Vector2Int a1, Vector2Int a2, Vector2Int b1, Vector2Int b2, out Vector2 intersection)
        {
            Vector2 p = a1;
            Vector2 r = (Vector2)a2 - p;
            Vector2 q = b1;
            Vector2 s = (Vector2)b2 - q;
            float cross = r.x * s.y - r.y * s.x;

            if (Mathf.Abs(cross) < 0.0001f)
            {
                intersection = Vector2.zero;
                return false;
            }

            Vector2 qp = q - p;
            float t = (qp.x * s.y - qp.y * s.x) / cross;
            float u = (qp.x * r.y - qp.y * r.x) / cross;

            if (t < 0f || t > 1f || u < 0f || u > 1f)
            {
                intersection = Vector2.zero;
                return false;
            }

            intersection = p + t * r;
            return true;
        }

        private static bool IsIntersectionColorMatched(StageTargetData target, LaserColorKind colorA, LaserColorKind colorB)
        {
            if (target.intersectionColors == null || target.intersectionColors.Count <= 0)
                return !target.requireDifferentColors || colorA != colorB;

            bool hasSpecificColor = false;
            bool matched = false;
            for (int i = 0; i < target.intersectionColors.Count; i++)
            {
                LaserColorKind color = target.intersectionColors[i];
                if (color == LaserColorKind.Default)
                    continue;

                hasSpecificColor = true;
                if (color == colorA || color == colorB)
                    matched = true;
            }

            if (!hasSpecificColor)
                return !target.requireDifferentColors || colorA != colorB;

            return matched;
        }

        private static bool IsLaserDistanceExactlyMatched(StageData stageData, LaserSolveResult result)
        {
            if (!stageData.useLaserDistanceLimit || stageData.laserMaxDistance <= 0)
                return true;

            return result.MaxBeamStepCount == stageData.laserMaxDistance || result.HasExactDistanceTargetHit;
        }

        private static void AddBeamStepCount(Dictionary<int, int> beamStepCounts, int beamId, int stepCount)
        {
            if (!beamStepCounts.ContainsKey(beamId) || beamStepCounts[beamId] < stepCount)
                beamStepCounts[beamId] = stepCount;
        }

        private static List<StageSolutionActionData> BuildActionList(List<SolverNode> nodes, int solvedNodeIndex)
        {
            List<StageSolutionActionData> reversed = new List<StageSolutionActionData>();
            int index = solvedNodeIndex;

            while (index >= 0)
            {
                SolverNode node = nodes[index];
                if (node.ParentIndex >= 0)
                    reversed.Add(node.Action.ToSolutionAction());

                index = node.ParentIndex;
            }

            reversed.Reverse();
            return reversed;
        }

        private static string BuildStateKey(SolverState state)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(state.PlayerPosition.x).Append(',').Append(state.PlayerPosition.y).Append(',').Append((int)state.FacingDirection).Append('|');
            for (int i = 0; i < state.Objects.Length; i++)
            {
                ObjectSnapshot obj = state.Objects[i];
                builder.Append(obj.Position.x).Append(',').Append(obj.Position.y).Append(',').Append((int)obj.Direction).Append(',').Append((int)obj.MirrorShape).Append(';');
            }
            return builder.ToString();
        }
    }
}
