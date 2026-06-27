using System;
using UnityEngine;
using Core;

namespace LevelEditor
{
    [Serializable]
    public class LevelEditorPaletteEntry
    {
        [Header("UI")]
        public string label = "Entry";
        public Sprite icon;

        [Header("Placement")]
        public LevelEditorPlacementKind placementKind = LevelEditorPlacementKind.PuzzleObject;
        public bool hasDirection;
        public GridDirection defaultDirection = GridDirection.Up;

        [Header("Object")]
        public StageObjectData objectData = new StageObjectData();

        [Header("Target")]
        public StageTargetData targetData = new StageTargetData();

        [Header("Distance Sensor")]
        public DistanceSensorData distanceSensorData = new DistanceSensorData();

        [Header("Transform Zone")]
        public TransformZoneData transformZoneData = new TransformZoneData();
    }
}
