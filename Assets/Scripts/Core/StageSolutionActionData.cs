using System;

namespace Core
{
    public enum StageSolutionActionType
    {
        Move = 0,
        RotateClockwise = 1,
        RotateCounterClockwise = 2
    }

    [Serializable]
    public class StageSolutionActionData
    {
        public StageSolutionActionType actionType = StageSolutionActionType.Move;
        public GridDirection direction = GridDirection.Right;

        public StageSolutionActionData Clone()
        {
            return new StageSolutionActionData
            {
                actionType = actionType,
                direction = direction
            };
        }

        public string ToDisplayText()
        {
            switch (actionType)
            {
                case StageSolutionActionType.RotateClockwise:
                    return "시계 회전";
                case StageSolutionActionType.RotateCounterClockwise:
                    return "반시계 회전";
                default:
                    return $"이동/방향전환 {direction}";
            }
        }
    }
}
