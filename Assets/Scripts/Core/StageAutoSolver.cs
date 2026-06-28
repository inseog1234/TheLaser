using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Core
{
    public enum StageAutoSolverSearchMode
    {
        WeightedAStar = 0,
        BalancedAStar = 1,
        BeamSearch = 2,
        BreadthFirst = 3
    }

    public static class StageAutoSolver
    {
        private enum AutoActionKind
        {
            Move = 0,
            RotateClockwise = 1,
            RotateCounterClockwise = 2,
            FireLaser = 3
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
                        AutoActionKind.FireLaser => StageSolutionActionType.FireLaser,
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
            public List<Vector2Int> WallPositions;

            public SolverState Clone()
            {
                ObjectSnapshot[] clonedObjects = new ObjectSnapshot[Objects.Length];
                for (int i = 0; i < Objects.Length; i++)
                    clonedObjects[i] = Objects[i].Clone();

                return new SolverState
                {
                    PlayerPosition = PlayerPosition,
                    FacingDirection = FacingDirection,
                    Objects = clonedObjects,
                    WallPositions = WallPositions != null ? new List<Vector2Int>(WallPositions) : new List<Vector2Int>()
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

        private struct FrontierEntry
        {
            public int NodeIndex;
            public int Score;
        }

        private struct PriorityFrontierEntry
        {
            public int NodeIndex;
            public int Priority;
            public int Depth;
            public int Sequence;
        }

        private sealed class PriorityFrontier
        {
            private readonly List<PriorityFrontierEntry> heap = new List<PriorityFrontierEntry>(4096);
            private int sequence;

            public int Count => heap.Count;

            public void Push(int nodeIndex, int priority, int depth)
            {
                PriorityFrontierEntry entry = new PriorityFrontierEntry
                {
                    NodeIndex = nodeIndex,
                    Priority = priority,
                    Depth = depth,
                    Sequence = sequence++
                };

                heap.Add(entry);
                SiftUp(heap.Count - 1);
            }

            public int Pop()
            {
                PriorityFrontierEntry best = heap[0];
                int lastIndex = heap.Count - 1;
                heap[0] = heap[lastIndex];
                heap.RemoveAt(lastIndex);

                if (heap.Count > 0)
                    SiftDown(0);

                return best.NodeIndex;
            }

            private void SiftUp(int index)
            {
                while (index > 0)
                {
                    int parent = (index - 1) / 2;
                    if (Compare(heap[index], heap[parent]) >= 0)
                        break;

                    Swap(index, parent);
                    index = parent;
                }
            }

            private void SiftDown(int index)
            {
                while (true)
                {
                    int left = index * 2 + 1;
                    int right = left + 1;
                    int best = index;

                    if (left < heap.Count && Compare(heap[left], heap[best]) < 0)
                        best = left;

                    if (right < heap.Count && Compare(heap[right], heap[best]) < 0)
                        best = right;

                    if (best == index)
                        break;

                    Swap(index, best);
                    index = best;
                }
            }

            private int Compare(PriorityFrontierEntry a, PriorityFrontierEntry b)
            {
                int priorityCompare = a.Priority.CompareTo(b.Priority);
                if (priorityCompare != 0)
                    return priorityCompare;

                int depthCompare = a.Depth.CompareTo(b.Depth);
                if (depthCompare != 0)
                    return depthCompare;

                return a.Sequence.CompareTo(b.Sequence);
            }

            private void Swap(int a, int b)
            {
                PriorityFrontierEntry temp = heap[a];
                heap[a] = heap[b];
                heap[b] = temp;
            }
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
            public int DistanceAmplifierHitCount;
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

        public static bool TrySolve(
            StageData stageData,
            out List<StageSolutionActionData> solutionActions,
            out string message,
            int maxNodeCount = 120000,
            int maxActionCount = 80,
            bool fastApproximateSearch = true,
            int beamWidth = 1024,
            StageAutoSolverSearchMode searchMode = StageAutoSolverSearchMode.WeightedAStar,
            float heuristicWeight = 3f)
        {
            StageAutoSolverSearchMode effectiveMode = fastApproximateSearch ? searchMode : StageAutoSolverSearchMode.BreadthFirst;

            switch (effectiveMode)
            {
                case StageAutoSolverSearchMode.WeightedAStar:
                case StageAutoSolverSearchMode.BalancedAStar:
                    return TrySolveWeightedAStar(stageData, out solutionActions, out message, maxNodeCount, maxActionCount, effectiveMode, heuristicWeight);

                case StageAutoSolverSearchMode.BeamSearch:
                    return TrySolveFast(stageData, out solutionActions, out message, maxNodeCount, maxActionCount, beamWidth);

                case StageAutoSolverSearchMode.BreadthFirst:
                default:
                    return TrySolveBreadthFirst(stageData, out solutionActions, out message, maxNodeCount, maxActionCount);
            }
        }

        private static bool TrySolveWeightedAStar(
            StageData stageData,
            out List<StageSolutionActionData> solutionActions,
            out string message,
            int maxNodeCount = 120000,
            int maxActionCount = 80,
            StageAutoSolverSearchMode searchMode = StageAutoSolverSearchMode.WeightedAStar,
            float heuristicWeight = 3f)
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

            int safeMaxNodeCount = Mathf.Max(1, maxNodeCount);
            float effectiveWeight = GetEffectiveHeuristicWeight(searchMode, heuristicWeight);

            SolverState startState = CreateInitialState(stageData);
            LaserSolveResult startLaserResult = SimulateLaser(stageData, startState, startState.PlayerPosition, startState.FacingDirection);
            if (IsLaserResultSolved(stageData, startLaserResult))
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

            PriorityFrontier frontier = new PriorityFrontier();
            frontier.Push(0, EvaluateWeightedAStarPriority(stageData, startState, 0, maxActionCount, startLaserResult, effectiveWeight), 0);

            Dictionary<string, int> bestDepthByState = new Dictionary<string, int>(4096);
            bestDepthByState[BuildStateKey(startState)] = 0;

            while (frontier.Count > 0)
            {
                int nodeIndex = frontier.Pop();
                SolverNode node = nodes[nodeIndex];

                if (node.Depth >= maxActionCount)
                    continue;

                AutoAction[] actions = BuildPriorityActionCandidates(stageData, node.State);
                for (int i = 0; i < actions.Length; i++)
                {
                    TryExpandActionWeightedAStar(
                        stageData,
                        nodes,
                        frontier,
                        bestDepthByState,
                        nodeIndex,
                        node,
                        actions[i],
                        safeMaxNodeCount,
                        maxActionCount,
                        effectiveWeight,
                        out bool nodeLimitReached,
                        out int solvedNodeIndex);

                    if (solvedNodeIndex >= 0)
                    {
                        solutionActions = BuildActionList(nodes, solvedNodeIndex);
                        message = $"{GetSearchModeDisplayName(searchMode)} 풀이 발견: {solutionActions.Count} 행동 / 노드 {nodes.Count}";
                        return true;
                    }

                    if (nodeLimitReached)
                    {
                        message = $"{GetSearchModeDisplayName(searchMode)} 탐색 실패: 탐색 노드 제한({safeMaxNodeCount})에 도달했습니다.";
                        return false;
                    }
                }
            }

            message = $"{GetSearchModeDisplayName(searchMode)} 탐색 실패: {bestDepthByState.Count}개 상태를 확인했지만 답을 찾지 못했습니다.";
            return false;
        }

        private static bool TrySolveBreadthFirst(StageData stageData, out List<StageSolutionActionData> solutionActions, out string message, int maxNodeCount = 120000, int maxActionCount = 80)
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

                AutoAction[] actions = BuildActionCandidates(node.State);
                for (int i = 0; i < actions.Length; i++)
                {
                    TryExpandAction(stageData, nodes, queue, visited, nodeIndex, node, actions[i], maxNodeCount, out bool nodeLimitReached, out int solvedNodeIndex);
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
            }

            message = $"AI 탐색 실패: {visited.Count}개 상태를 확인했지만 답을 찾지 못했습니다.";
            return false;
        }

        private static bool TrySolveFast(StageData stageData, out List<StageSolutionActionData> solutionActions, out string message, int maxNodeCount = 120000, int maxActionCount = 80, int beamWidth = 1024)
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

            int safeMaxNodeCount = Mathf.Max(1, maxNodeCount);
            int safeBeamWidth = Mathf.Clamp(beamWidth, 16, safeMaxNodeCount);

            SolverState startState = CreateInitialState(stageData);
            LaserSolveResult startLaserResult = SimulateLaser(stageData, startState, startState.PlayerPosition, startState.FacingDirection);
            if (IsLaserResultSolved(stageData, startLaserResult))
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

            List<FrontierEntry> frontier = new List<FrontierEntry>(safeBeamWidth);
            AddFrontier(frontier, 0, EvaluateStateScore(stageData, startState, 0, maxActionCount, startLaserResult), safeBeamWidth);

            HashSet<string> visited = new HashSet<string>();
            visited.Add(BuildStateKey(startState));

            while (frontier.Count > 0)
            {
                int nodeIndex = PopBestFrontier(frontier);
                SolverNode node = nodes[nodeIndex];

                if (node.Depth >= maxActionCount)
                    continue;

                AutoAction[] actions = BuildActionCandidates(node.State);
                for (int i = 0; i < actions.Length; i++)
                {
                    TryExpandActionFast(stageData, nodes, frontier, visited, nodeIndex, node, actions[i], safeMaxNodeCount, maxActionCount, safeBeamWidth, out bool nodeLimitReached, out int solvedNodeIndex);
                    if (solvedNodeIndex >= 0)
                    {
                        solutionActions = BuildActionList(nodes, solvedNodeIndex);
                        message = $"빠른 AI 풀이 발견: {solutionActions.Count} 행동 / 노드 {nodes.Count}";
                        return true;
                    }

                    if (nodeLimitReached)
                    {
                        message = $"빠른 AI 탐색 실패: 탐색 노드 제한({safeMaxNodeCount})에 도달했습니다.";
                        return false;
                    }
                }
            }

            if (HasDistanceSensorMechanics(stageData) || HasDistanceAmplifierMechanics(stageData))
            {
                bool solvedByFallback = TrySolveBreadthFirst(stageData, out solutionActions, out string fallbackMessage, safeMaxNodeCount, maxActionCount);
                message = solvedByFallback
                    ? $"빠른 탐색 실패 후 정밀 탐색 성공: {solutionActions.Count} 행동"
                    : fallbackMessage;
                return solvedByFallback;
            }

            message = $"빠른 AI 탐색 실패: {visited.Count}개 상태를 확인했지만 답을 찾지 못했습니다. Beam Width를 늘리거나 빠른 탐색을 끄면 더 꼼꼼하게 찾습니다.";
            return false;
        }

        public static IEnumerator SolveCoroutine(
            StageData stageData,
            Action<int, int> onProgress,
            Action<bool, List<StageSolutionActionData>, string, int> onComplete,
            int maxNodeCount = 120000,
            int maxActionCount = 80,
            int progressUpdateInterval = 256,
            bool fastApproximateSearch = true,
            int beamWidth = 1024,
            StageAutoSolverSearchMode searchMode = StageAutoSolverSearchMode.WeightedAStar,
            float heuristicWeight = 3f)
        {
            StageAutoSolverSearchMode effectiveMode = fastApproximateSearch ? searchMode : StageAutoSolverSearchMode.BreadthFirst;

            if (effectiveMode == StageAutoSolverSearchMode.WeightedAStar || effectiveMode == StageAutoSolverSearchMode.BalancedAStar)
            {
                yield return SolveWeightedAStarCoroutine(stageData, onProgress, onComplete, maxNodeCount, maxActionCount, progressUpdateInterval, effectiveMode, heuristicWeight);
                yield break;
            }

            if (effectiveMode == StageAutoSolverSearchMode.BeamSearch)
            {
                yield return SolveFastCoroutine(stageData, onProgress, onComplete, maxNodeCount, maxActionCount, progressUpdateInterval, beamWidth);
                yield break;
            }

            List<StageSolutionActionData> solutionActions = new List<StageSolutionActionData>();
            string message = string.Empty;
            int currentNodeCount = 0;
            int safeMaxNodeCount = Mathf.Max(1, maxNodeCount);
            int safeProgressInterval = Mathf.Max(1, progressUpdateInterval);

            void Complete(bool solved, string completeMessage)
            {
                onProgress?.Invoke(currentNodeCount, safeMaxNodeCount);
                onComplete?.Invoke(solved, solutionActions, completeMessage, currentNodeCount);
            }

            if (stageData == null)
            {
                Complete(false, "스테이지 데이터가 없습니다.");
                yield break;
            }

            if (stageData.width <= 0 || stageData.height <= 0)
            {
                Complete(false, "맵 크기가 올바르지 않습니다.");
                yield break;
            }

            int moveLimit = Mathf.Max(0, stageData.moveLimit);
            if (moveLimit > 0)
                maxActionCount = Mathf.Min(maxActionCount, moveLimit);

            SolverState startState = CreateInitialState(stageData);
            currentNodeCount = 1;
            onProgress?.Invoke(currentNodeCount, safeMaxNodeCount);
            yield return null;

            if (IsSolvedByLaser(stageData, startState))
            {
                Complete(true, "현재 시작 상태에서 바로 클리어 가능합니다.");
                yield break;
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

            int expandedActionCount = 0;

            while (queue.Count > 0)
            {
                int nodeIndex = queue.Dequeue();
                SolverNode node = nodes[nodeIndex];

                if (node.Depth >= maxActionCount)
                    continue;

                AutoAction[] actions = BuildActionCandidates(node.State);
                for (int i = 0; i < actions.Length; i++)
                {
                    TryExpandAction(stageData, nodes, queue, visited, nodeIndex, node, actions[i], safeMaxNodeCount, out bool nodeLimitReached, out int solvedNodeIndex);
                    currentNodeCount = nodes.Count;
                    expandedActionCount++;

                    if (solvedNodeIndex >= 0)
                    {
                        solutionActions = BuildActionList(nodes, solvedNodeIndex);
                        Complete(true, $"AI 풀이 발견: {solutionActions.Count} 행동");
                        yield break;
                    }

                    if (nodeLimitReached)
                    {
                        Complete(false, $"AI 탐색 실패: 탐색 노드 제한({safeMaxNodeCount})에 도달했습니다.");
                        yield break;
                    }

                    if (expandedActionCount % safeProgressInterval == 0)
                    {
                        onProgress?.Invoke(currentNodeCount, safeMaxNodeCount);
                        yield return null;
                    }
                }
            }

            currentNodeCount = Mathf.Max(currentNodeCount, visited.Count);
            Complete(false, $"AI 탐색 실패: {visited.Count}개 상태를 확인했지만 답을 찾지 못했습니다.");
        }

        private static IEnumerator SolveWeightedAStarCoroutine(
            StageData stageData,
            Action<int, int> onProgress,
            Action<bool, List<StageSolutionActionData>, string, int> onComplete,
            int maxNodeCount,
            int maxActionCount,
            int progressUpdateInterval,
            StageAutoSolverSearchMode searchMode,
            float heuristicWeight)
        {
            List<StageSolutionActionData> solutionActions = new List<StageSolutionActionData>();
            int currentNodeCount = 0;
            int safeMaxNodeCount = Mathf.Max(1, maxNodeCount);
            int safeProgressInterval = Mathf.Max(1, progressUpdateInterval);
            float effectiveWeight = GetEffectiveHeuristicWeight(searchMode, heuristicWeight);
            string modeName = GetSearchModeDisplayName(searchMode);

            void Complete(bool solved, string completeMessage)
            {
                onProgress?.Invoke(currentNodeCount, safeMaxNodeCount);
                onComplete?.Invoke(solved, solutionActions, completeMessage, currentNodeCount);
            }

            if (stageData == null)
            {
                Complete(false, "스테이지 데이터가 없습니다.");
                yield break;
            }

            if (stageData.width <= 0 || stageData.height <= 0)
            {
                Complete(false, "맵 크기가 올바르지 않습니다.");
                yield break;
            }

            int moveLimit = Mathf.Max(0, stageData.moveLimit);
            if (moveLimit > 0)
                maxActionCount = Mathf.Min(maxActionCount, moveLimit);

            SolverState startState = CreateInitialState(stageData);
            LaserSolveResult startLaserResult = SimulateLaser(stageData, startState, startState.PlayerPosition, startState.FacingDirection);
            currentNodeCount = 1;
            onProgress?.Invoke(currentNodeCount, safeMaxNodeCount);
            yield return null;

            if (IsLaserResultSolved(stageData, startLaserResult))
            {
                Complete(true, "현재 시작 상태에서 바로 클리어 가능합니다.");
                yield break;
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

            PriorityFrontier frontier = new PriorityFrontier();
            frontier.Push(0, EvaluateWeightedAStarPriority(stageData, startState, 0, maxActionCount, startLaserResult, effectiveWeight), 0);

            Dictionary<string, int> bestDepthByState = new Dictionary<string, int>(4096);
            bestDepthByState[BuildStateKey(startState)] = 0;

            int expandedActionCount = 0;

            while (frontier.Count > 0)
            {
                int nodeIndex = frontier.Pop();
                SolverNode node = nodes[nodeIndex];

                if (node.Depth >= maxActionCount)
                    continue;

                AutoAction[] actions = BuildPriorityActionCandidates(stageData, node.State);
                for (int i = 0; i < actions.Length; i++)
                {
                    TryExpandActionWeightedAStar(
                        stageData,
                        nodes,
                        frontier,
                        bestDepthByState,
                        nodeIndex,
                        node,
                        actions[i],
                        safeMaxNodeCount,
                        maxActionCount,
                        effectiveWeight,
                        out bool nodeLimitReached,
                        out int solvedNodeIndex);

                    currentNodeCount = nodes.Count;
                    expandedActionCount++;

                    if (solvedNodeIndex >= 0)
                    {
                        solutionActions = BuildActionList(nodes, solvedNodeIndex);
                        Complete(true, $"{modeName} 풀이 발견: {solutionActions.Count} 행동 / Weight {effectiveWeight:0.##}");
                        yield break;
                    }

                    if (nodeLimitReached)
                    {
                        Complete(false, $"{modeName} 탐색 실패: 탐색 노드 제한({safeMaxNodeCount})에 도달했습니다.");
                        yield break;
                    }

                    if (expandedActionCount % safeProgressInterval == 0)
                    {
                        onProgress?.Invoke(currentNodeCount, safeMaxNodeCount);
                        yield return null;
                    }
                }
            }

            currentNodeCount = Mathf.Max(currentNodeCount, bestDepthByState.Count);
            Complete(false, $"{modeName} 탐색 실패: {bestDepthByState.Count}개 상태를 확인했지만 답을 찾지 못했습니다.");
        }

        private static IEnumerator SolveFastCoroutine(
            StageData stageData,
            Action<int, int> onProgress,
            Action<bool, List<StageSolutionActionData>, string, int> onComplete,
            int maxNodeCount,
            int maxActionCount,
            int progressUpdateInterval,
            int beamWidth)
        {
            List<StageSolutionActionData> solutionActions = new List<StageSolutionActionData>();
            int currentNodeCount = 0;
            int safeMaxNodeCount = Mathf.Max(1, maxNodeCount);
            int safeProgressInterval = Mathf.Max(1, progressUpdateInterval);
            int safeBeamWidth = Mathf.Clamp(beamWidth, 16, safeMaxNodeCount);

            void Complete(bool solved, string completeMessage)
            {
                onProgress?.Invoke(currentNodeCount, safeMaxNodeCount);
                onComplete?.Invoke(solved, solutionActions, completeMessage, currentNodeCount);
            }

            if (stageData == null)
            {
                Complete(false, "스테이지 데이터가 없습니다.");
                yield break;
            }

            if (stageData.width <= 0 || stageData.height <= 0)
            {
                Complete(false, "맵 크기가 올바르지 않습니다.");
                yield break;
            }

            int moveLimit = Mathf.Max(0, stageData.moveLimit);
            if (moveLimit > 0)
                maxActionCount = Mathf.Min(maxActionCount, moveLimit);

            SolverState startState = CreateInitialState(stageData);
            LaserSolveResult startLaserResult = SimulateLaser(stageData, startState, startState.PlayerPosition, startState.FacingDirection);
            currentNodeCount = 1;
            onProgress?.Invoke(currentNodeCount, safeMaxNodeCount);
            yield return null;

            if (IsLaserResultSolved(stageData, startLaserResult))
            {
                Complete(true, "현재 시작 상태에서 바로 클리어 가능합니다.");
                yield break;
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

            List<FrontierEntry> frontier = new List<FrontierEntry>(safeBeamWidth);
            AddFrontier(frontier, 0, EvaluateStateScore(stageData, startState, 0, maxActionCount, startLaserResult), safeBeamWidth);

            HashSet<string> visited = new HashSet<string>();
            visited.Add(BuildStateKey(startState));

            int expandedActionCount = 0;

            while (frontier.Count > 0)
            {
                int nodeIndex = PopBestFrontier(frontier);
                SolverNode node = nodes[nodeIndex];

                if (node.Depth >= maxActionCount)
                    continue;

                AutoAction[] actions = BuildActionCandidates(node.State);
                for (int i = 0; i < actions.Length; i++)
                {
                    TryExpandActionFast(stageData, nodes, frontier, visited, nodeIndex, node, actions[i], safeMaxNodeCount, maxActionCount, safeBeamWidth, out bool nodeLimitReached, out int solvedNodeIndex);
                    currentNodeCount = nodes.Count;
                    expandedActionCount++;

                    if (solvedNodeIndex >= 0)
                    {
                        solutionActions = BuildActionList(nodes, solvedNodeIndex);
                        Complete(true, $"빠른 AI 풀이 발견: {solutionActions.Count} 행동 / Beam {safeBeamWidth}");
                        yield break;
                    }

                    if (nodeLimitReached)
                    {
                        Complete(false, $"빠른 AI 탐색 실패: 탐색 노드 제한({safeMaxNodeCount})에 도달했습니다.");
                        yield break;
                    }

                    if (expandedActionCount % safeProgressInterval == 0)
                    {
                        onProgress?.Invoke(currentNodeCount, safeMaxNodeCount);
                        yield return null;
                    }
                }
            }

            currentNodeCount = Mathf.Max(currentNodeCount, visited.Count);

            if (HasDistanceSensorMechanics(stageData) || HasDistanceAmplifierMechanics(stageData))
            {
                // 감응 타일/레이저 증폭기 챕터는 빠른 Beam Search가 유효한 중간 발사/증폭기 경로를 가지치기할 수 있어서,
                // 빠른 탐색이 실패하면 같은 제한 안에서 정밀 탐색으로 한 번 더 확인한다.
                yield return SolveCoroutine(
                    stageData,
                    onProgress,
                    onComplete,
                    maxNodeCount,
                    maxActionCount,
                    progressUpdateInterval,
                    false,
                    beamWidth);
                yield break;
            }

            Complete(false, $"빠른 AI 탐색 실패: {visited.Count}개 상태를 확인했지만 답을 찾지 못했습니다. Beam Width를 늘리거나 빠른 탐색을 끄면 더 꼼꼼하게 찾습니다.");
        }

        private static string GetSearchModeDisplayName(StageAutoSolverSearchMode searchMode)
        {
            switch (searchMode)
            {
                case StageAutoSolverSearchMode.BalancedAStar:
                    return "균형 A*";
                case StageAutoSolverSearchMode.BeamSearch:
                    return "Beam Search";
                case StageAutoSolverSearchMode.BreadthFirst:
                    return "완전 탐색";
                case StageAutoSolverSearchMode.WeightedAStar:
                default:
                    return "Weighted A*";
            }
        }

        private static float GetEffectiveHeuristicWeight(StageAutoSolverSearchMode searchMode, float heuristicWeight)
        {
            if (searchMode == StageAutoSolverSearchMode.BalancedAStar)
                return Mathf.Clamp(heuristicWeight <= 0f ? 1.6f : heuristicWeight, 1f, 2.2f);

            return Mathf.Clamp(heuristicWeight <= 0f ? 3f : heuristicWeight, 1f, 6f);
        }

        private static AutoAction[] BuildPriorityActionCandidates(StageData stageData, SolverState state)
        {
            bool hasLaserTriggeredMechanic = HasDistanceSensorMechanics(stageData) || HasDistanceAmplifierMechanics(stageData);
            if (hasLaserTriggeredMechanic)
            {
                return new[]
                {
                    new AutoAction { Kind = AutoActionKind.FireLaser, Direction = state.FacingDirection },
                    new AutoAction { Kind = AutoActionKind.RotateClockwise, Direction = state.FacingDirection },
                    new AutoAction { Kind = AutoActionKind.RotateCounterClockwise, Direction = state.FacingDirection },
                    new AutoAction { Kind = AutoActionKind.Move, Direction = state.FacingDirection },
                    new AutoAction { Kind = AutoActionKind.Move, Direction = state.FacingDirection.RotateClockwise() },
                    new AutoAction { Kind = AutoActionKind.Move, Direction = state.FacingDirection.RotateCounterClockwise() },
                    new AutoAction { Kind = AutoActionKind.Move, Direction = state.FacingDirection.Opposite() }
                };
            }

            return BuildActionCandidates(state);
        }

        private static void TryExpandActionWeightedAStar(
            StageData stageData,
            List<SolverNode> nodes,
            PriorityFrontier frontier,
            Dictionary<string, int> bestDepthByState,
            int parentIndex,
            SolverNode parentNode,
            AutoAction action,
            int maxNodeCount,
            int maxActionCount,
            float heuristicWeight,
            out bool nodeLimitReached,
            out int solvedNodeIndex)
        {
            nodeLimitReached = false;
            solvedNodeIndex = -1;

            if (!TryApplyAction(stageData, parentNode.State, action, out SolverState nextState, out bool solvedByAction, out bool stateChanged))
                return;

            int nextDepth = parentNode.Depth + 1;
            string key = BuildStateKey(nextState);

            if (!solvedByAction)
            {
                if (!stateChanged)
                    return;

                if (bestDepthByState.TryGetValue(key, out int knownDepth) && knownDepth <= nextDepth)
                    return;
            }

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
                Depth = nextDepth
            };

            nodes.Add(nextNode);
            int nextIndex = nodes.Count - 1;

            LaserSolveResult laserResult = solvedByAction
                ? null
                : SimulateLaser(stageData, nextState, nextState.PlayerPosition, nextState.FacingDirection);

            if (solvedByAction || IsLaserResultSolved(stageData, laserResult))
            {
                solvedNodeIndex = nextIndex;
                return;
            }

            bestDepthByState[key] = nextDepth;
            int priority = EvaluateWeightedAStarPriority(stageData, nextState, nextDepth, maxActionCount, laserResult, heuristicWeight);
            frontier.Push(nextIndex, priority, nextDepth);
        }

        private static int EvaluateWeightedAStarPriority(StageData stageData, SolverState state, int depth, int maxActionCount, LaserSolveResult cachedResult, float heuristicWeight)
        {
            int heuristicScore = EvaluateStateScore(stageData, state, depth, maxActionCount, cachedResult);
            float weight = Mathf.Max(0.1f, heuristicWeight);
            float costSoFar = depth * 850f;
            float priority = heuristicScore * weight + costSoFar;

            if (maxActionCount > 0)
            {
                float depthRatio = depth / Mathf.Max(1f, maxActionCount);
                if (depthRatio > 0.82f)
                    priority += (depthRatio - 0.82f) * 45000f;
            }

            priority = Mathf.Clamp(priority, -2000000000f, 2000000000f);
            return Mathf.RoundToInt(priority);
        }

        private static AutoAction[] BuildActionCandidates(SolverState state)
        {
            return new[]
            {
                new AutoAction { Kind = AutoActionKind.Move, Direction = MoveDirections[0] },
                new AutoAction { Kind = AutoActionKind.Move, Direction = MoveDirections[1] },
                new AutoAction { Kind = AutoActionKind.Move, Direction = MoveDirections[2] },
                new AutoAction { Kind = AutoActionKind.Move, Direction = MoveDirections[3] },
                new AutoAction { Kind = AutoActionKind.RotateClockwise, Direction = state.FacingDirection },
                new AutoAction { Kind = AutoActionKind.RotateCounterClockwise, Direction = state.FacingDirection },
                new AutoAction { Kind = AutoActionKind.FireLaser, Direction = state.FacingDirection }
            };
        }

        private static void TryExpandActionFast(
            StageData stageData,
            List<SolverNode> nodes,
            List<FrontierEntry> frontier,
            HashSet<string> visited,
            int parentIndex,
            SolverNode parentNode,
            AutoAction action,
            int maxNodeCount,
            int maxActionCount,
            int beamWidth,
            out bool nodeLimitReached,
            out int solvedNodeIndex)
        {
            nodeLimitReached = false;
            solvedNodeIndex = -1;

            if (!TryApplyAction(stageData, parentNode.State, action, out SolverState nextState, out bool solvedByAction, out bool stateChanged))
                return;

            string key = BuildStateKey(nextState);
            bool alreadyVisited = !stateChanged || !visited.Add(key);

            if (alreadyVisited && !solvedByAction)
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

            LaserSolveResult laserResult = solvedByAction
                ? null
                : SimulateLaser(stageData, nextState, nextState.PlayerPosition, nextState.FacingDirection);
            if (solvedByAction || IsLaserResultSolved(stageData, laserResult))
            {
                solvedNodeIndex = nextIndex;
                return;
            }

            int score = EvaluateStateScore(stageData, nextState, nextNode.Depth, maxActionCount, laserResult);
            AddFrontier(frontier, nextIndex, score, beamWidth);
        }

        private static void AddFrontier(List<FrontierEntry> frontier, int nodeIndex, int score, int beamWidth)
        {
            int safeBeamWidth = Mathf.Max(16, beamWidth);
            frontier.Add(new FrontierEntry { NodeIndex = nodeIndex, Score = score });

            if (frontier.Count > safeBeamWidth * 2)
                TrimFrontier(frontier, safeBeamWidth);
        }

        private static int PopBestFrontier(List<FrontierEntry> frontier)
        {
            int bestIndex = 0;
            int bestScore = frontier[0].Score;

            for (int i = 1; i < frontier.Count; i++)
            {
                if (frontier[i].Score >= bestScore)
                    continue;

                bestScore = frontier[i].Score;
                bestIndex = i;
            }

            int nodeIndex = frontier[bestIndex].NodeIndex;
            int lastIndex = frontier.Count - 1;
            frontier[bestIndex] = frontier[lastIndex];
            frontier.RemoveAt(lastIndex);
            return nodeIndex;
        }

        private static void TrimFrontier(List<FrontierEntry> frontier, int beamWidth)
        {
            int safeBeamWidth = Mathf.Max(16, beamWidth);
            if (frontier.Count <= safeBeamWidth)
                return;

            frontier.Sort((a, b) => a.Score.CompareTo(b.Score));
            frontier.RemoveRange(safeBeamWidth, frontier.Count - safeBeamWidth);
        }

        private static int EvaluateStateScore(StageData stageData, SolverState state, int depth, int maxActionCount, LaserSolveResult cachedResult = null)
        {
            LaserSolveResult result = cachedResult ?? SimulateLaser(stageData, state, state.PlayerPosition, state.FacingDirection);
            List<StageTargetData> targets = CollectAllTargets(stageData);

            int totalTargets = targets.Count;
            int satisfiedTargets = CountSatisfiedTargetsForHeuristic(stageData, result, targets);
            int missingTargets = Mathf.Max(0, totalTargets - satisfiedTargets);

            int score = missingTargets * 100000;
            score -= satisfiedTargets * 20000;
            score -= Mathf.Min(result.TargetHits.Count, 16) * 3000;
            score -= Mathf.Min(result.Segments.Count, 80) * 20;

            if (stageData.useLaserDistanceLimit && stageData.laserMaxDistance > 0 && !result.HasExactDistanceTargetHit)
                score += 15000;

            if (HasDistanceAmplifierMechanics(stageData))
            {
                int amplifierLaserDistance = GetNearestLaserDistanceToDistanceAmplifier(stageData, state, result);
                score += Mathf.Min(amplifierLaserDistance, 64) * 520;

                if (result.DistanceAmplifierHitCount > 0)
                    score -= Mathf.Min(result.DistanceAmplifierHitCount, 4) * 12000;
            }

            int laserDistance = GetNearestLaserDistanceToMissingTarget(stageData, state, result, targets);
            score += Mathf.Min(laserDistance, 64) * 700;

            if (HasDistanceSensorMechanics(stageData))
            {
                int sensorLaserDistance = GetNearestLaserDistanceToSensor(stageData, result);
                score += Mathf.Min(sensorLaserDistance, 64) * 450;

                if (DoesLaserActivateAnySensor(stageData, result))
                    score -= 12000;
            }

            int playerUsefulDistance = GetPlayerDistanceToNearestUsefulPosition(stageData, state, targets);
            score += Mathf.Min(playerUsefulDistance, 64) * 35;

            score += depth * 12;

            if (maxActionCount > 0)
            {
                int softDepth = Mathf.RoundToInt(maxActionCount * 0.7f);
                if (depth > softDepth)
                    score += (depth - softDepth) * 50;
            }

            return score;
        }

        private static List<StageTargetData> CollectAllTargets(StageData stageData)
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

            return allTargets;
        }

        private static int CountSatisfiedTargetsForHeuristic(StageData stageData, LaserSolveResult result, List<StageTargetData> targets)
        {
            int count = 0;

            for (int i = 0; i < targets.Count; i++)
            {
                if (IsTargetSatisfiedForHeuristic(stageData, result, targets[i]))
                    count++;
            }

            return count;
        }

        private static bool IsTargetSatisfiedForHeuristic(StageData stageData, LaserSolveResult result, StageTargetData target)
        {
            if (target == null)
                return false;

            switch (target.targetType)
            {
                case TargetType.ColorLocked:
                case TargetType.SequenceColorLocked:
                    return HasTargetHit(result, target.position, target.requiredColor);

                case TargetType.Intersection:
                    return IsIntersectionTargetActivated(target, result);

                default:
                    return HasTargetHit(result, target.position, null);
            }
        }

        private static int GetNearestLaserDistanceToMissingTarget(StageData stageData, SolverState state, LaserSolveResult result, List<StageTargetData> targets)
        {
            int best = int.MaxValue;

            for (int i = 0; i < targets.Count; i++)
            {
                StageTargetData target = targets[i];
                if (target == null || IsTargetSatisfiedForHeuristic(stageData, result, target))
                    continue;

                for (int s = 0; s < result.Segments.Count; s++)
                    best = Mathf.Min(best, DistancePointToSegmentEndPoints(target.position, result.Segments[s]));

                best = Mathf.Min(best, ManhattanDistance(state.PlayerPosition, target.position));
            }

            if (best == int.MaxValue)
                return 0;

            return best;
        }

        private static int DistancePointToSegmentEndPoints(Vector2Int position, Segment segment)
        {
            int startDistance = ManhattanDistance(position, segment.Start);
            int endDistance = ManhattanDistance(position, segment.End);
            return Mathf.Min(startDistance, endDistance);
        }

        private static int GetNearestLaserDistanceToSensor(StageData stageData, LaserSolveResult result)
        {
            if (stageData == null || stageData.distanceSensors == null || stageData.distanceSensors.Count <= 0)
                return 0;

            int best = int.MaxValue;
            for (int i = 0; i < stageData.distanceSensors.Count; i++)
            {
                DistanceSensorData sensor = stageData.distanceSensors[i];
                if (sensor == null)
                    continue;

                for (int s = 0; s < result.Segments.Count; s++)
                    best = Mathf.Min(best, DistancePointToSegmentEndPoints(sensor.position, result.Segments[s]));
            }

            return best == int.MaxValue ? 0 : best;
        }

        private static bool DoesLaserActivateAnySensor(StageData stageData, LaserSolveResult result)
        {
            if (stageData == null || result == null || stageData.distanceSensors == null)
                return false;

            for (int i = 0; i < stageData.distanceSensors.Count; i++)
            {
                DistanceSensorData sensor = stageData.distanceSensors[i];
                if (sensor != null && IsDistanceSensorActivated(sensor, result))
                    return true;
            }

            return false;
        }

        private static bool HasDistanceSensorMechanics(StageData stageData)
        {
            return stageData != null && stageData.distanceSensors != null && stageData.distanceSensors.Count > 0;
        }

        private static bool HasDistanceAmplifierMechanics(StageData stageData)
        {
            if (stageData == null || stageData.objects == null)
                return false;

            for (int i = 0; i < stageData.objects.Count; i++)
            {
                StageObjectData obj = stageData.objects[i];
                if (obj != null &&
                    obj.objectType == PuzzleObjectType.Lens &&
                    obj.lensType == LensType.DistanceAmplifier &&
                    obj.distanceBoost > 0)
                    return true;
            }

            return false;
        }

        private static int GetNearestLaserDistanceToDistanceAmplifier(StageData stageData, SolverState state, LaserSolveResult result)
        {
            int best = int.MaxValue;

            if (state == null || state.Objects == null)
                return 0;

            for (int i = 0; i < state.Objects.Length; i++)
            {
                ObjectSnapshot obj = state.Objects[i];
                if (obj == null || obj.ObjectType != PuzzleObjectType.Lens || obj.LensType != LensType.DistanceAmplifier || obj.DistanceBoost <= 0)
                    continue;

                for (int s = 0; s < result.Segments.Count; s++)
                    best = Mathf.Min(best, DistancePointToSegmentEndPoints(obj.Position, result.Segments[s]));

                best = Mathf.Min(best, ManhattanDistance(state.PlayerPosition, obj.Position));
            }

            return best == int.MaxValue ? 0 : best;
        }

        private static int GetPlayerDistanceToNearestUsefulPosition(StageData stageData, SolverState state, List<StageTargetData> targets)
        {
            int best = int.MaxValue;

            for (int i = 0; i < state.Objects.Length; i++)
            {
                ObjectSnapshot obj = state.Objects[i];
                if (obj == null || (!CanPush(obj) && !CanRotate(obj)))
                    continue;

                best = Mathf.Min(best, ManhattanDistance(state.PlayerPosition, obj.Position));
            }

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] == null)
                    continue;

                best = Mathf.Min(best, ManhattanDistance(state.PlayerPosition, targets[i].position));
            }

            if (stageData.distanceSensors != null)
            {
                for (int i = 0; i < stageData.distanceSensors.Count; i++)
                {
                    DistanceSensorData sensor = stageData.distanceSensors[i];
                    if (sensor == null)
                        continue;

                    best = Mathf.Min(best, ManhattanDistance(state.PlayerPosition, sensor.position));
                }
            }

            if (best == int.MaxValue)
                return 0;

            return best;
        }

        private static int ManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
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

            if (!TryApplyAction(stageData, parentNode.State, action, out SolverState nextState, out bool solvedByAction, out bool stateChanged))
                return;

            string key = BuildStateKey(nextState);
            bool alreadyVisited = !stateChanged || !visited.Add(key);

            if (alreadyVisited && !solvedByAction)
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

            if (solvedByAction || IsSolvedByLaser(stageData, nextState))
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

            List<Vector2Int> walls = stageData.wallPositions != null
                ? new List<Vector2Int>(stageData.wallPositions)
                : new List<Vector2Int>();

            return new SolverState
            {
                PlayerPosition = stageData.playerStartPosition,
                FacingDirection = stageData.playerStartDirection,
                Objects = objects.ToArray(),
                WallPositions = walls
            };
        }

        private static bool TryApplyAction(StageData stageData, SolverState state, AutoAction action, out SolverState nextState, out bool solvedByAction, out bool stateChanged)
        {
            nextState = state.Clone();
            solvedByAction = false;
            stateChanged = false;

            if (action.Kind == AutoActionKind.Move)
            {
                bool applied = TryApplyMove(stageData, state, nextState, action.Direction);
                stateChanged = applied && BuildStateKey(state) != BuildStateKey(nextState);
                return applied;
            }

            if (action.Kind == AutoActionKind.FireLaser)
                return TryApplyLaserFire(stageData, state, nextState, out solvedByAction, out stateChanged);

            bool clockwise = action.Kind == AutoActionKind.RotateClockwise;
            bool rotated = TryApplyRotate(state, nextState, clockwise);
            stateChanged = rotated && BuildStateKey(state) != BuildStateKey(nextState);
            return rotated;
        }

        private static bool TryApplyLaserFire(StageData stageData, SolverState sourceState, SolverState nextState, out bool solvedByAction, out bool stateChanged)
        {
            LaserSolveResult result = SimulateLaser(stageData, sourceState, sourceState.PlayerPosition, sourceState.FacingDirection);
            solvedByAction = IsLaserResultSolved(stageData, result);

            ApplyDistanceSensorEffects(stageData, nextState, result);
            stateChanged = BuildStateKey(sourceState) != BuildStateKey(nextState);

            return solvedByAction || stateChanged;
        }

        private static void ApplyDistanceSensorEffects(StageData stageData, SolverState state, LaserSolveResult result)
        {
            if (stageData == null || state == null || result == null || stageData.distanceSensors == null)
                return;

            for (int i = 0; i < stageData.distanceSensors.Count; i++)
            {
                DistanceSensorData sensor = stageData.distanceSensors[i];
                if (sensor == null || !IsDistanceSensorActivated(sensor, result))
                    continue;

                if (sensor.activateTransformZone && !string.IsNullOrWhiteSpace(sensor.transformZoneId))
                    ApplyTransformZone(stageData, state, sensor.transformZoneId);

                if (sensor.triggers == null)
                    continue;

                for (int t = 0; t < sensor.triggers.Count; t++)
                    ApplyDistanceSensorTrigger(stageData, state, sensor.triggers[t]);
            }
        }

        private static bool IsDistanceSensorActivated(DistanceSensorData sensor, LaserSolveResult result)
        {
            Vector2 point = new Vector2(sensor.position.x, sensor.position.y);
            float radius = Mathf.Max(0.01f, sensor.detectionRadius);

            for (int i = 0; i < result.Segments.Count; i++)
            {
                Segment segment = result.Segments[i];
                if (DistancePointToSegment(point, segment.Start, segment.End) <= radius)
                    return true;
            }

            return false;
        }

        private static float DistancePointToSegment(Vector2 point, Vector2Int segmentStart, Vector2Int segmentEnd)
        {
            Vector2 a = segmentStart;
            Vector2 b = segmentEnd;
            Vector2 ab = b - a;
            float sqrMagnitude = ab.sqrMagnitude;
            if (sqrMagnitude <= 0.0001f)
                return Vector2.Distance(point, a);

            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / sqrMagnitude);
            Vector2 closest = a + ab * t;
            return Vector2.Distance(point, closest);
        }

        private static void ApplyDistanceSensorTrigger(StageData stageData, SolverState state, DistanceSensorTriggerData trigger)
        {
            if (trigger == null)
                return;

            switch (trigger.triggerKind)
            {
                case DistanceSensorTriggerKind.MoveWall:
                    TryMoveWall(stageData, state, trigger.wallPosition, trigger.wallMoveTargetPosition);
                    break;

                case DistanceSensorTriggerKind.ChangePrismDirection:
                    TrySetObjectDirectionAt(state, trigger.prismPosition, trigger.prismDirection);
                    break;

                case DistanceSensorTriggerKind.ChangeMirrorState:
                    TrySetMirrorStateAt(state, trigger.mirrorPosition, trigger.mirrorDirection, trigger.mirrorShape);
                    break;

                case DistanceSensorTriggerKind.ActivateTransformZone:
                    ApplyTransformZone(stageData, state, trigger.transformZoneId);
                    break;
            }
        }

        private static bool TryMoveWall(StageData stageData, SolverState state, Vector2Int fromPosition, Vector2Int toPosition)
        {
            if (state == null || state.WallPositions == null)
                return false;

            if (!stageData.IsInside(fromPosition) || !stageData.IsInside(toPosition) || fromPosition == toPosition)
                return false;

            int wallIndex = state.WallPositions.IndexOf(fromPosition);
            if (wallIndex < 0)
                return false;

            if (HasWall(state, toPosition) || FindObjectIndexAt(state, toPosition) >= 0 || HasTarget(stageData, toPosition))
                return false;

            state.WallPositions[wallIndex] = toPosition;
            return true;
        }

        private static bool TrySetObjectDirectionAt(SolverState state, Vector2Int position, GridDirection direction)
        {
            int objectIndex = FindObjectIndexAt(state, position);
            if (objectIndex < 0)
                return false;

            state.Objects[objectIndex].Direction = direction;
            return true;
        }

        private static bool TrySetMirrorStateAt(SolverState state, Vector2Int position, GridDirection direction, MirrorShape mirrorShape)
        {
            int objectIndex = FindObjectIndexAt(state, position);
            if (objectIndex < 0)
                return false;

            ObjectSnapshot obj = state.Objects[objectIndex];
            if (obj.ObjectType != PuzzleObjectType.Mirror)
                return false;

            obj.Direction = direction;
            obj.MirrorShape = mirrorShape;
            return true;
        }

        private static void ApplyTransformZone(StageData stageData, SolverState state, string zoneId)
        {
            TransformZoneData zone = FindTransformZone(stageData, zoneId);
            if (zone != null)
                ApplyTransformZone(stageData, state, zone);
        }

        private static TransformZoneData FindTransformZone(StageData stageData, string zoneId)
        {
            if (stageData == null || stageData.transformZones == null || string.IsNullOrWhiteSpace(zoneId))
                return null;

            for (int i = 0; i < stageData.transformZones.Count; i++)
            {
                TransformZoneData zone = stageData.transformZones[i];
                if (zone != null && zone.zoneId == zoneId)
                    return zone;
            }

            return null;
        }

        private static void ApplyTransformZone(StageData stageData, SolverState state, TransformZoneData zone)
        {
            if (stageData == null || state == null || zone == null)
                return;

            List<int> affectedObjectIndices = new List<int>();
            for (int i = 0; i < state.Objects.Length; i++)
            {
                if (IsPositionInsideZone(state.Objects[i].Position, zone))
                    affectedObjectIndices.Add(i);
            }

            if (affectedObjectIndices.Count <= 0)
                return;

            Dictionary<int, Vector2Int> nextPositions = new Dictionary<int, Vector2Int>();
            Dictionary<int, GridDirection> nextDirections = new Dictionary<int, GridDirection>();
            Dictionary<int, MirrorShape> nextShapes = new Dictionary<int, MirrorShape>();
            HashSet<Vector2Int> occupiedByAffected = new HashSet<Vector2Int>();
            HashSet<Vector2Int> reserved = new HashSet<Vector2Int>();

            for (int i = 0; i < affectedObjectIndices.Count; i++)
                occupiedByAffected.Add(state.Objects[affectedObjectIndices[i]].Position);

            for (int i = 0; i < affectedObjectIndices.Count; i++)
            {
                int objectIndex = affectedObjectIndices[i];
                ObjectSnapshot obj = state.Objects[objectIndex];
                Vector2Int nextPosition = TransformPosition(obj.Position, zone);
                GridDirection nextDirection = TransformDirection(obj.Direction, zone);
                MirrorShape nextShape = TransformMirrorShape(obj.MirrorShape, zone);

                if (!stageData.IsInside(nextPosition) || HasWall(state, nextPosition))
                    return;

                if (reserved.Contains(nextPosition))
                    return;

                int otherIndex = FindObjectIndexAt(state, nextPosition);
                if (otherIndex >= 0 && !occupiedByAffected.Contains(nextPosition))
                    return;

                reserved.Add(nextPosition);
                nextPositions[objectIndex] = nextPosition;
                nextDirections[objectIndex] = nextDirection;
                nextShapes[objectIndex] = nextShape;
            }

            foreach (KeyValuePair<int, Vector2Int> pair in nextPositions)
            {
                ObjectSnapshot obj = state.Objects[pair.Key];
                obj.Position = pair.Value;
                obj.Direction = nextDirections[pair.Key];
                obj.MirrorShape = nextShapes[pair.Key];
            }
        }

        private static bool IsPositionInsideZone(Vector2Int position, TransformZoneData zone)
        {
            Vector2Int min = GetZoneMinCell(zone);
            int maxX = min.x + Mathf.Max(1, zone.width) - 1;
            int maxY = min.y + Mathf.Max(1, zone.height) - 1;

            return position.x >= min.x && position.x <= maxX && position.y >= min.y && position.y <= maxY;
        }

        private static Vector2Int GetZoneMinCell(TransformZoneData zone)
        {
            int width = Mathf.Max(1, zone.width);
            int height = Mathf.Max(1, zone.height);
            int minX = zone.center.x - width / 2;
            int minY = zone.center.y - height / 2;

            if (width % 2 == 0 && zone.offsetX > 0)
                minX += 1;

            if (height % 2 == 0 && zone.offsetY > 0)
                minY += 1;

            return new Vector2Int(minX, minY);
        }

        private static Vector2Int TransformPosition(Vector2Int position, TransformZoneData zone)
        {
            Vector2Int relative = position - zone.center;

            if (zone.zoneType == TransformZoneType.Rotate90)
            {
                Vector2Int rotated = zone.clockwise
                    ? new Vector2Int(relative.y, -relative.x)
                    : new Vector2Int(-relative.y, relative.x);

                return zone.center + rotated;
            }

            if (zone.zoneType == TransformZoneType.Mirror)
            {
                Vector2Int mirrored = zone.mirrorAxis == MirrorAxis.Vertical
                    ? new Vector2Int(-relative.x, relative.y)
                    : new Vector2Int(relative.x, -relative.y);

                return zone.center + mirrored;
            }

            return position;
        }

        private static GridDirection TransformDirection(GridDirection direction, TransformZoneData zone)
        {
            if (zone.zoneType == TransformZoneType.Rotate90)
                return zone.clockwise ? direction.RotateClockwise() : direction.RotateCounterClockwise();

            if (zone.zoneType == TransformZoneType.Mirror)
            {
                if (zone.mirrorAxis == MirrorAxis.Vertical)
                {
                    return direction switch
                    {
                        GridDirection.Left => GridDirection.Right,
                        GridDirection.Right => GridDirection.Left,
                        _ => direction
                    };
                }

                return direction switch
                {
                    GridDirection.Up => GridDirection.Down,
                    GridDirection.Down => GridDirection.Up,
                    _ => direction
                };
            }

            return direction;
        }

        private static MirrorShape TransformMirrorShape(MirrorShape shape, TransformZoneData zone)
        {
            if (shape == MirrorShape.None)
                return shape;

            if (zone.zoneType != TransformZoneType.Mirror)
                return shape;

            if (shape == MirrorShape.NormalL)
                return MirrorShape.ReverseL;

            if (shape == MirrorShape.ReverseL)
                return MirrorShape.NormalL;

            return shape;
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

            if (HasWall(state, position))
                return false;

            if (FindObjectIndexAt(state, position) >= 0)
                return false;

            return true;
        }

        private static bool IsEmpty(StageData stageData, SolverState state, Vector2Int position)
        {
            if (!stageData.IsInside(position))
                return false;

            if (HasWall(state, position))
                return false;

            if (FindObjectIndexAt(state, position) >= 0)
                return false;

            return true;
        }

        private static bool HasWall(SolverState state, Vector2Int position)
        {
            return state != null && state.WallPositions != null && state.WallPositions.Contains(position);
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
            return IsLaserResultSolved(stageData, result);
        }

        private static bool IsLaserResultSolved(StageData stageData, LaserSolveResult result)
        {
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
                {
                    if (stageData.useLaserDistanceLimit && stageData.laserMaxDistance > 0 &&
                        TryDetectTouchingTargetOnLaserEnd(stageData, result, currentPosition, currentDirection, currentColor, beam.BeamId))
                    {
                        result.HasExactDistanceTargetHit = true;
                    }

                    break;
                }

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

                string stateKey = $"{nextPosition.x},{nextPosition.y},{(int)currentDirection},{(int)currentColor},{remainingDistance}";
                if (!visitedStates.Add(stateKey))
                    break;

                if (HasWall(state, nextPosition))
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

        private static bool TryDetectTouchingTargetOnLaserEnd(StageData stageData, LaserSolveResult result, Vector2Int endPosition, LaserDirection endDirection, LaserColorKind color, int beamId)
        {
            Vector2Int forwardPosition = endPosition + endDirection.ToVector();
            return TryReachTargetFromLaserEnd(stageData, result, endPosition, forwardPosition, color, beamId);
        }

        private static bool TryReachTargetFromLaserEnd(StageData stageData, LaserSolveResult result, Vector2Int endPosition, Vector2Int targetPosition, LaserColorKind color, int beamId)
        {
            if (stageData == null || result == null)
                return false;

            if (!stageData.IsInside(targetPosition) || !HasTarget(stageData, targetPosition))
                return false;

            result.TargetHits.Add(new TargetHit
            {
                Position = targetPosition,
                Color = color,
                BeamId = beamId,
                HitIndex = result.TargetHits.Count
            });

            return true;
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
                    {
                        int boost = Mathf.Max(0, obj.DistanceBoost);
                        if (boost > 0)
                        {
                            remainingDistance += boost;
                            result.DistanceAmplifierHitCount++;
                        }
                    }
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
            if (state.WallPositions != null)
            {
                List<Vector2Int> sortedWalls = new List<Vector2Int>(state.WallPositions);
                sortedWalls.Sort((a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
                for (int i = 0; i < sortedWalls.Count; i++)
                    builder.Append('W').Append(sortedWalls[i].x).Append(',').Append(sortedWalls[i].y).Append(';');
            }
            builder.Append('|');
            for (int i = 0; i < state.Objects.Length; i++)
            {
                ObjectSnapshot obj = state.Objects[i];
                builder.Append(obj.Position.x).Append(',').Append(obj.Position.y).Append(',').Append((int)obj.Direction).Append(',').Append((int)obj.MirrorShape).Append(';');
            }
            return builder.ToString();
        }
    }
}
