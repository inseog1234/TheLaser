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

        [Header("Prism")]
        public PrismType prismType = PrismType.Splitter;
        public PrismSplitterMode splitterMode = PrismSplitterMode.ForwardLeftRight;
        public LaserColorKind prismColor = LaserColorKind.Red;
        public RefractionMode refractionMode = RefractionMode.Clockwise45;

        [Header("Lens")]
        public LensType lensType = LensType.DistanceAmplifier;
        [Min(0)] public int distanceBoost = 5;
    }
}
