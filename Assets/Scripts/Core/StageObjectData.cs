using System;
using UnityEngine;

namespace Core
{
    [Serializable]
    public class StageObjectData
    {
        [Header("Object Type")]
        public PuzzleObjectType objectType = PuzzleObjectType.Mirror;

        [Header("Manipulation")]
        public ManipulationType manipulationType = ManipulationType.None;

        [Header("Grid Position")]
        public Vector2Int position;

        [Header("Direction")]
        public GridDirection direction = GridDirection.Up;

        [Header("Mirror")]
        public MirrorShape mirrorShape = MirrorShape.NormalL;
    }
}