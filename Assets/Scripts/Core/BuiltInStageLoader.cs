using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    public sealed class BuiltInStageEntry
    {
        public string ResourceKey;
        public string DisplayName;
        public StageData Data;
    }

    public static class BuiltInStageLoader
    {
        private static readonly List<BuiltInStageEntry> cachedEntries = new List<BuiltInStageEntry>();
        private static bool cacheBuilt;

        public static List<BuiltInStageEntry> LoadEntries(bool forceRefresh = false)
        {
            if (cacheBuilt && !forceRefresh)
                return CloneEntryList(cachedEntries);

            cachedEntries.Clear();
            TextAsset[] assets = Resources.LoadAll<TextAsset>(StageFilePaths.BuiltInLevelsResourcePath);
            for (int i = 0; i < assets.Length; i++)
            {
                TextAsset asset = assets[i];
                if (asset == null || asset.bytes == null || asset.bytes.Length <= 0)
                    continue;

                if (!StageBinarySerializer.TryLoad(asset.bytes, out StageData data, asset.name))
                    continue;

                cachedEntries.Add(new BuiltInStageEntry
                {
                    ResourceKey = asset.name,
                    DisplayName = asset.name + StageFilePaths.StageExtension,
                    Data = data
                });
            }

            cachedEntries.Sort(CompareEntries);
            cacheBuilt = true;
            return CloneEntryList(cachedEntries);
        }

        public static bool TryLoad(string builtInResourcePath, out StageData stageData)
        {
            stageData = null;
            string key = StageFilePaths.ExtractBuiltInResourceKey(builtInResourcePath);
            if (string.IsNullOrWhiteSpace(key))
                return false;

            TextAsset asset = Resources.Load<TextAsset>(StageFilePaths.GetBuiltInResourceLoadPath(key));
            if (asset == null || asset.bytes == null || asset.bytes.Length <= 0)
                return false;

            return StageBinarySerializer.TryLoad(asset.bytes, out stageData, key);
        }

        public static string FindNextBuiltInResourcePath(StageData currentStage)
        {
            if (currentStage == null)
                return string.Empty;

            List<BuiltInStageEntry> entries = LoadEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                StageData data = entries[i].Data;
                if (data == null)
                    continue;

                if (IsStageAfter(data, currentStage))
                    return StageFilePaths.ToBuiltInResourcePath(entries[i].ResourceKey);
            }

            return string.Empty;
        }

        private static bool IsStageAfter(StageData candidate, StageData current)
        {
            if (candidate.chapterIndex > current.chapterIndex)
                return true;

            if (candidate.chapterIndex < current.chapterIndex)
                return false;

            return candidate.stageIndexInChapter > current.stageIndexInChapter;
        }

        private static int CompareEntries(BuiltInStageEntry a, BuiltInStageEntry b)
        {
            StageData da = a != null ? a.Data : null;
            StageData db = b != null ? b.Data : null;
            int ca = da != null ? da.chapterIndex : 0;
            int cb = db != null ? db.chapterIndex : 0;
            int chapterCompare = ca.CompareTo(cb);
            if (chapterCompare != 0)
                return chapterCompare;

            int sa = da != null ? da.stageIndexInChapter : 0;
            int sb = db != null ? db.stageIndexInChapter : 0;
            int stageCompare = sa.CompareTo(sb);
            if (stageCompare != 0)
                return stageCompare;

            return string.Compare(a?.ResourceKey, b?.ResourceKey, StringComparison.OrdinalIgnoreCase);
        }

        private static List<BuiltInStageEntry> CloneEntryList(List<BuiltInStageEntry> source)
        {
            List<BuiltInStageEntry> result = new List<BuiltInStageEntry>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                BuiltInStageEntry entry = source[i];
                if (entry == null)
                    continue;

                result.Add(new BuiltInStageEntry
                {
                    ResourceKey = entry.ResourceKey,
                    DisplayName = entry.DisplayName,
                    Data = entry.Data != null ? entry.Data.Clone() : null
                });
            }

            return result;
        }
    }
}
