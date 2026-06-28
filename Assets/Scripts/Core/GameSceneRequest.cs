using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    public static class GameSceneRequest
    {
        public static bool HasRequest { get; private set; }
        public static bool IsCustomLevel { get; private set; }
        public static bool IsEditorTestPlay { get; private set; }
        public static bool IsEditorBatchSolutionProcessing { get; private set; }
        public static string StageFilePath { get; private set; }
        public static string ReturnSceneName { get; private set; }

        public static StageData EditorTestStageData { get; private set; }
        public static bool HasEditorTestStageData => EditorTestStageData != null;

        private static StageData editorReturnStageData;
        private static string editorReturnFilePath;
        private static string editorReturnSaveDirectory;
        private static string editorReturnSaveFileName;
        private static List<StageSolutionActionData> pendingEditorSolutionActions;

        private static List<string> batchSolutionFilePaths = new();
        private static int batchSolutionIndex;
        private static int batchSolutionSolvedCount;
        private static int batchSolutionFailedCount;
        private static string batchSolutionLastMessage = string.Empty;

        public static bool HasEditorReturnStageData => editorReturnStageData != null;
        public static bool HasPendingEditorSolution => pendingEditorSolutionActions != null && pendingEditorSolutionActions.Count > 0;
        public static bool HasBatchSolutionRequest => IsEditorBatchSolutionProcessing && batchSolutionFilePaths != null && batchSolutionFilePaths.Count > 0;
        public static int BatchSolutionTotalCount => batchSolutionFilePaths != null ? batchSolutionFilePaths.Count : 0;
        public static int BatchSolutionCurrentIndex => batchSolutionIndex;
        public static int BatchSolutionDisplayIndex => Mathf.Clamp(batchSolutionIndex + 1, 1, Mathf.Max(1, BatchSolutionTotalCount));
        public static int BatchSolutionSolvedCount => batchSolutionSolvedCount;
        public static int BatchSolutionFailedCount => batchSolutionFailedCount;
        public static string BatchSolutionLastMessage => batchSolutionLastMessage;
        public static string CurrentBatchSolutionFilePath => HasBatchSolutionRequest && batchSolutionIndex >= 0 && batchSolutionIndex < batchSolutionFilePaths.Count ? batchSolutionFilePaths[batchSolutionIndex] : string.Empty;

        public static void RequestBuiltInStage(string stageFilePath)
        {
            HasRequest = true;
            IsCustomLevel = false;
            IsEditorTestPlay = false;
            IsEditorBatchSolutionProcessing = false;
            StageFilePath = stageFilePath;
            ReturnSceneName = "Title";
            EditorTestStageData = null;
            pendingEditorSolutionActions = null;
        }

        public static void RequestCustomStage(string stageFilePath)
        {
            HasRequest = true;
            IsCustomLevel = true;
            IsEditorTestPlay = false;
            IsEditorBatchSolutionProcessing = false;
            StageFilePath = stageFilePath;
            ReturnSceneName = "Title";
            EditorTestStageData = null;
            pendingEditorSolutionActions = null;
        }

        public static void RequestEditorTestStage(string stageFilePath, string returnSceneName)
        {
            HasRequest = true;
            IsCustomLevel = true;
            IsEditorTestPlay = true;
            IsEditorBatchSolutionProcessing = false;
            StageFilePath = stageFilePath;
            ReturnSceneName = string.IsNullOrWhiteSpace(returnSceneName) ? "LevelEditor" : returnSceneName;
            EditorTestStageData = null;
            pendingEditorSolutionActions = null;
        }

        public static void RequestEditorTestStage(StageData stageData, string returnSceneName, string currentFilePath, string saveDirectory, string saveFileName)
        {
            StageData safeCopy = stageData != null ? stageData.Clone() : null;

            HasRequest = safeCopy != null;
            IsCustomLevel = true;
            IsEditorTestPlay = true;
            IsEditorBatchSolutionProcessing = false;
            StageFilePath = string.Empty;
            ReturnSceneName = string.IsNullOrWhiteSpace(returnSceneName) ? "LevelEditor" : returnSceneName;
            EditorTestStageData = safeCopy;
            pendingEditorSolutionActions = null;

            editorReturnStageData = safeCopy != null ? safeCopy.Clone() : null;
            editorReturnFilePath = currentFilePath ?? string.Empty;
            editorReturnSaveDirectory = saveDirectory ?? string.Empty;
            editorReturnSaveFileName = saveFileName ?? string.Empty;
        }

        public static void RequestEditorBatchSolution(List<string> filePaths, string returnSceneName)
        {
            batchSolutionFilePaths = new List<string>();

            if (filePaths != null)
            {
                for (int i = 0; i < filePaths.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(filePaths[i]) && !batchSolutionFilePaths.Contains(filePaths[i]))
                        batchSolutionFilePaths.Add(filePaths[i]);
                }
            }

            batchSolutionIndex = 0;
            batchSolutionSolvedCount = 0;
            batchSolutionFailedCount = 0;
            batchSolutionLastMessage = string.Empty;

            HasRequest = batchSolutionFilePaths.Count > 0;
            IsCustomLevel = true;
            IsEditorTestPlay = false;
            IsEditorBatchSolutionProcessing = batchSolutionFilePaths.Count > 0;
            StageFilePath = CurrentBatchSolutionFilePath;
            ReturnSceneName = string.IsNullOrWhiteSpace(returnSceneName) ? "LevelEditor" : returnSceneName;
            EditorTestStageData = null;
            pendingEditorSolutionActions = null;
        }

        public static void ReportCurrentBatchSolutionResult(bool success, string message)
        {
            if (success)
                batchSolutionSolvedCount++;
            else
                batchSolutionFailedCount++;

            batchSolutionLastMessage = message ?? string.Empty;
        }

        public static bool MoveToNextBatchSolutionStage()
        {
            if (!HasBatchSolutionRequest)
                return false;

            batchSolutionIndex++;

            if (batchSolutionIndex >= batchSolutionFilePaths.Count)
            {
                HasRequest = false;
                IsCustomLevel = false;
                IsEditorTestPlay = false;
                IsEditorBatchSolutionProcessing = false;
                StageFilePath = string.Empty;
                EditorTestStageData = null;
                return false;
            }

            HasRequest = true;
            IsCustomLevel = true;
            IsEditorTestPlay = false;
            IsEditorBatchSolutionProcessing = true;
            StageFilePath = CurrentBatchSolutionFilePath;
            EditorTestStageData = null;
            return true;
        }

        public static bool TryConsumeBatchSolutionResult(out int totalCount, out int solvedCount, out int failedCount, out string lastMessage)
        {
            totalCount = batchSolutionFilePaths != null ? batchSolutionFilePaths.Count : 0;
            solvedCount = batchSolutionSolvedCount;
            failedCount = batchSolutionFailedCount;
            lastMessage = batchSolutionLastMessage;

            bool hasResult = totalCount > 0 && !IsEditorBatchSolutionProcessing;

            if (hasResult)
            {
                batchSolutionFilePaths = new List<string>();
                batchSolutionIndex = 0;
                batchSolutionSolvedCount = 0;
                batchSolutionFailedCount = 0;
                batchSolutionLastMessage = string.Empty;
            }

            return hasResult;
        }

        public static void SetEditorTestRecordedSolution(List<StageSolutionActionData> actions)
        {
            pendingEditorSolutionActions = CloneSolutionActionList(actions);
        }

        public static bool TryConsumePendingEditorSolution(out List<StageSolutionActionData> actions)
        {
            actions = CloneSolutionActionList(pendingEditorSolutionActions);
            bool hasActions = actions != null && actions.Count > 0;
            pendingEditorSolutionActions = null;
            return hasActions;
        }

        public static bool TryConsumeEditorReturnStage(out StageData stageData, out string currentFilePath, out string saveDirectory, out string saveFileName)
        {
            stageData = editorReturnStageData != null ? editorReturnStageData.Clone() : null;
            currentFilePath = editorReturnFilePath;
            saveDirectory = editorReturnSaveDirectory;
            saveFileName = editorReturnSaveFileName;

            bool hasData = stageData != null;

            editorReturnStageData = null;
            editorReturnFilePath = string.Empty;
            editorReturnSaveDirectory = string.Empty;
            editorReturnSaveFileName = string.Empty;

            return hasData;
        }

        public static void ClearGameplayRequest()
        {
            HasRequest = false;
            IsCustomLevel = false;
            IsEditorTestPlay = false;
            IsEditorBatchSolutionProcessing = false;
            StageFilePath = string.Empty;
            ReturnSceneName = string.Empty;
            EditorTestStageData = null;
        }

        public static void Clear()
        {
            ClearGameplayRequest();
            pendingEditorSolutionActions = null;
            batchSolutionFilePaths = new List<string>();
            batchSolutionIndex = 0;
            batchSolutionSolvedCount = 0;
            batchSolutionFailedCount = 0;
            batchSolutionLastMessage = string.Empty;
        }

        private static List<StageSolutionActionData> CloneSolutionActionList(List<StageSolutionActionData> source)
        {
            if (source == null)
                return new List<StageSolutionActionData>();

            List<StageSolutionActionData> result = new List<StageSolutionActionData>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null)
                    result.Add(source[i].Clone());
            }

            return result;
        }
    }
}
