using UnityEngine;
using Core;

namespace Grid
{
    public class GridCell
    {
        public Vector2Int Position { get; private set; }
        public CellType CellType { get; private set; }
        public GridObject CurrentObject { get; private set; }

        public bool HasObject => CurrentObject != null;
        public bool IsWall => CellType == CellType.Wall;
        public bool IsTarget => CellType == CellType.Target;

        public bool IsWalkable
        {
            get
            {
                if (IsWall)
                    return false;

                if (HasObject)
                    return false;

                return true;
            }
        }

        public GridCell(Vector2Int position, CellType cellType)
        {
            Position = position;
            CellType = cellType;
        }

        public void SetCellType(CellType cellType)
        {
            CellType = cellType;
        }

        public void SetObject(GridObject gridObject)
        {
            CurrentObject = gridObject;
        }

        public void ClearObject()
        {
            CurrentObject = null;
        }
    }
}