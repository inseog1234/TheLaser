using System.Collections.Generic;
using UnityEngine;
using Core;
using Grid;

namespace Laser
{
    public class LaserSimulator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridManager gridManager;

        [Header("Option")]
        [SerializeField] private int maxStepCount = 100;

        public LaserResult Simulate(Vector2Int startPosition, GridDirection startDirection)
        {
            LaserResult result = new LaserResult();

            if (gridManager == null)
            {
                Debug.LogWarning("[LaserSimulator] GridManager가 없습니다.");
                return result;
            }

            HashSet<LaserState> visitedStates = new();

            Vector2Int currentPosition = startPosition;
            GridDirection currentDirection = startDirection;

            visitedStates.Add(new LaserState(currentPosition, currentDirection));

            result.AddNode(LaserPathNode.Start(startPosition, startDirection));

            for (int step = 0; step < maxStepCount; step++)
            {
                Vector2Int nextPosition = currentPosition + currentDirection.ToVector();

                if (!gridManager.IsInside(nextPosition))
                {
                    AddEndNode(result, currentPosition, currentDirection);
                    result.SetHitWall(nextPosition);
                    break;
                }

                LaserState nextState = new LaserState(nextPosition, currentDirection);

                if (visitedStates.Contains(nextState))
                {
                    result.AddNode(LaserPathNode.Loop(nextPosition, currentDirection));
                    AddEndNode(result, currentPosition, currentDirection);
                    result.SetLoopDetected(nextPosition);
                    break;
                }

                visitedStates.Add(nextState);

                if (gridManager.HasWall(nextPosition))
                {
                    result.AddNode(LaserPathNode.Blocked(nextPosition, currentDirection));
                    AddEndNode(result, currentPosition, currentDirection);
                    result.SetHitWall(nextPosition);
                    break;
                }

                if (gridManager.HasTarget(nextPosition))
                {
                    result.AddNode(LaserPathNode.Target(nextPosition, currentDirection));
                    AddEndNode(result, currentPosition, currentDirection);
                    result.SetReachedTarget(nextPosition);
                    break;
                }

                GridObject gridObject = gridManager.GetObjectAt(nextPosition);

                if (gridObject != null)
                {
                    if (gridObject.TryReflectLaser(currentDirection, out GridDirection reflectedDirection))
                    {
                        result.AddNode(LaserPathNode.Corner(nextPosition, currentDirection, reflectedDirection));

                        currentPosition = nextPosition;
                        currentDirection = reflectedDirection;
                        continue;
                    }

                    result.AddNode(LaserPathNode.Blocked(nextPosition, currentDirection));
                    AddEndNode(result, currentPosition, currentDirection);
                    result.SetHitObjectAndStopped(nextPosition);
                    break;
                }

                result.AddNode(LaserPathNode.Straight(nextPosition, currentDirection));

                currentPosition = nextPosition;
            }

            if (!result.ReachedTarget &&
                !result.HitWall &&
                !result.HitObjectAndStopped &&
                !result.LoopDetected)
            {
                AddEndNode(result, currentPosition, currentDirection);
                result.SetLoopDetected(currentPosition);

                Debug.LogWarning("[LaserSimulator] 최대 레이저 계산 횟수를 초과했습니다.");
            }

            return result;
        }

        private void AddEndNode(LaserResult result, Vector2Int position, GridDirection incomingDirection)
        {
            int index = FindLastNodeIndexAtPosition(result, position);

            if (index < 0)
            {
                result.AddNode(LaserPathNode.End(position, incomingDirection));
                return;
            }

            LaserPathNode lastNodeAtPosition = result.PathNodes[index];

            if (lastNodeAtPosition.NodeType == LaserPathNodeType.Corner)
            {
                result.PathNodes[index] = LaserPathNode.CornerEnd(
                    lastNodeAtPosition.Position,
                    lastNodeAtPosition.IncomingDirection,
                    lastNodeAtPosition.OutgoingDirection
                );

                return;
            }

            if (lastNodeAtPosition.NodeType == LaserPathNodeType.Straight)
            {
                result.PathNodes[index] = LaserPathNode.End(
                    lastNodeAtPosition.Position,
                    lastNodeAtPosition.IncomingDirection
                );

                return;
            }

            if (lastNodeAtPosition.NodeType == LaserPathNodeType.End ||
                lastNodeAtPosition.NodeType == LaserPathNodeType.CornerEnd)
            {
                return;
            }

            result.AddNode(LaserPathNode.End(position, incomingDirection));
        }

        private int FindLastNodeIndexAtPosition(LaserResult result, Vector2Int position)
        {
            for (int i = result.PathNodes.Count - 1; i >= 0; i--)
            {
                if (result.PathNodes[i].Position == position)
                    return i;
            }

            return -1;
        }
    }
}