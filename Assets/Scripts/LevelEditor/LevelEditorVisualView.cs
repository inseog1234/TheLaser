using System;
using System.Collections.Generic;
using UnityEngine;
using Core;

namespace LevelEditor
{
    [Serializable]
    public class LevelEditorVisualEntry
    {
        public GameObject visualObject;
        public LevelEditorPlacementKind placementKind = LevelEditorPlacementKind.PuzzleObject;

        [Header("Object Filter")]
        public bool useObjectTypeFilter;
        public PuzzleObjectType objectType;
        public bool useManipulationFilter;
        public ManipulationType manipulationType;
        public bool useMirrorShapeFilter;
        public MirrorShape mirrorShape;
        public bool usePrismTypeFilter;
        public PrismType prismType;
        public bool useLensTypeFilter;
        public LensType lensType;

        [Header("Target Filter")]
        public bool useTargetTypeFilter;
        public TargetType targetType;
    }

    public class LevelEditorVisualView : MonoBehaviour
    {
        [SerializeField] private List<LevelEditorVisualEntry> visualEntries = new();

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void SetEntry(LevelEditorPaletteEntry paletteEntry, GridDirection direction)
        {
            DisableAll();

            LevelEditorVisualEntry visualEntry = FindBestVisualEntry(paletteEntry);
            if (visualEntry == null || visualEntry.visualObject == null)
                return;

            visualEntry.visualObject.SetActive(true);
            transform.rotation = Quaternion.Euler(0f, 0f, direction.ToAngleZ());
        }

        private LevelEditorVisualEntry FindBestVisualEntry(LevelEditorPaletteEntry paletteEntry)
        {
            if (paletteEntry == null)
                return null;

            LevelEditorVisualEntry best = null;
            int bestScore = -1;

            for (int i = 0; i < visualEntries.Count; i++)
            {
                LevelEditorVisualEntry entry = visualEntries[i];

                if (entry == null || entry.visualObject == null)
                    continue;

                if (entry.placementKind != paletteEntry.placementKind)
                    continue;

                if (!IsMatched(entry, paletteEntry))
                    continue;

                int score = CalculateScore(entry);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = entry;
                }
            }

            return best;
        }

        private bool IsMatched(LevelEditorVisualEntry visualEntry, LevelEditorPaletteEntry paletteEntry)
        {
            if (paletteEntry.placementKind == LevelEditorPlacementKind.PuzzleObject)
            {
                StageObjectData data = paletteEntry.objectData;
                if (data == null)
                    return false;

                if (visualEntry.useObjectTypeFilter && visualEntry.objectType != data.objectType)
                    return false;

                if (visualEntry.useManipulationFilter && visualEntry.manipulationType != data.manipulationType)
                    return false;

                if (visualEntry.useMirrorShapeFilter && visualEntry.mirrorShape != data.mirrorShape)
                    return false;

                if (visualEntry.usePrismTypeFilter && visualEntry.prismType != data.prismType)
                    return false;

                if (visualEntry.useLensTypeFilter && visualEntry.lensType != data.lensType)
                    return false;
            }

            if (paletteEntry.placementKind == LevelEditorPlacementKind.AdvancedTarget)
            {
                StageTargetData data = paletteEntry.targetData;
                if (data == null)
                    return false;

                if (visualEntry.useTargetTypeFilter && visualEntry.targetType != data.targetType)
                    return false;
            }

            return true;
        }

        private int CalculateScore(LevelEditorVisualEntry entry)
        {
            int score = 0;
            if (entry.useObjectTypeFilter) score += 10;
            if (entry.useManipulationFilter) score += 10;
            if (entry.useMirrorShapeFilter) score += 10;
            if (entry.usePrismTypeFilter) score += 10;
            if (entry.useLensTypeFilter) score += 10;
            if (entry.useTargetTypeFilter) score += 10;
            return score;
        }

        private void DisableAll()
        {
            for (int i = 0; i < visualEntries.Count; i++)
            {
                if (visualEntries[i] != null && visualEntries[i].visualObject != null)
                    visualEntries[i].visualObject.SetActive(false);
            }
        }
    }
}
