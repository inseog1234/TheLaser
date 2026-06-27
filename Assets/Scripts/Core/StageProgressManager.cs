using UnityEngine;

namespace Core
{
    public static class StageProgressManager
    {
        private const string ChapterPrefix = "TheLaser_Clear_Chapter_";

        public static int GetClearedStageCount(int chapterIndex)
        {
            return PlayerPrefs.GetInt(ChapterPrefix + chapterIndex, 0);
        }

        public static bool IsStageUnlocked(StageData stageData)
        {
            if (stageData == null)
                return false;

            if (stageData.chapterIndex <= 1 && stageData.stageIndexInChapter <= 1)
                return true;

            if (stageData.stageIndexInChapter <= 1)
                return GetClearedStageCount(stageData.chapterIndex - 1) >= GetRequiredClearCountForNextChapter(stageData.chapterIndex - 1);

            return GetClearedStageCount(stageData.chapterIndex) >= stageData.stageIndexInChapter - 1;
        }

        private static int GetRequiredClearCountForNextChapter(int chapterIndex)
        {
            if (chapterIndex <= 0)
                return 0;

            return 5;
        }

        public static void MarkCleared(StageData stageData)
        {
            if (stageData == null)
                return;

            int current = GetClearedStageCount(stageData.chapterIndex);
            if (stageData.stageIndexInChapter > current)
            {
                PlayerPrefs.SetInt(ChapterPrefix + stageData.chapterIndex, stageData.stageIndexInChapter);
                PlayerPrefs.Save();
            }
        }
    }
}
