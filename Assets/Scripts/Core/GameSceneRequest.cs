using UnityEngine;

namespace Core
{
    public static class GameSceneRequest
    {
        public static bool HasRequest { get; private set; }
        public static bool IsCustomLevel { get; private set; }
        public static bool IsEditorTestPlay { get; private set; }
        public static string StageFilePath { get; private set; }
        public static string ReturnSceneName { get; private set; }

        public static StageData EditorTestStageData { get; private set; }
        public static bool HasEditorTestStageData => EditorTestStageData != null;

        private static StageData editorReturnStageData;
        private static string editorReturnFilePath;
        private static string editorReturnSaveDirectory;
        private static string editorReturnSaveFileName;

        public static bool HasEditorReturnStageData => editorReturnStageData != null;

        public static void RequestBuiltInStage(string stageFilePath)
        {
            HasRequest = true;
            IsCustomLevel = false;
            IsEditorTestPlay = false;
            StageFilePath = stageFilePath;
            ReturnSceneName = "Title";
            EditorTestStageData = null;
        }

        public static void RequestCustomStage(string stageFilePath)
        {
            HasRequest = true;
            IsCustomLevel = true;
            IsEditorTestPlay = false;
            StageFilePath = stageFilePath;
            ReturnSceneName = "Title";
            EditorTestStageData = null;
        }

        public static void RequestEditorTestStage(string stageFilePath, string returnSceneName)
        {
            HasRequest = true;
            IsCustomLevel = true;
            IsEditorTestPlay = true;
            StageFilePath = stageFilePath;
            ReturnSceneName = string.IsNullOrWhiteSpace(returnSceneName) ? "LevelEditor" : returnSceneName;
            EditorTestStageData = null;
        }

        public static void RequestEditorTestStage(StageData stageData, string returnSceneName, string currentFilePath, string saveDirectory, string saveFileName)
        {
            StageData safeCopy = stageData != null ? stageData.Clone() : null;

            HasRequest = safeCopy != null;
            IsCustomLevel = true;
            IsEditorTestPlay = true;
            StageFilePath = string.Empty;
            ReturnSceneName = string.IsNullOrWhiteSpace(returnSceneName) ? "LevelEditor" : returnSceneName;
            EditorTestStageData = safeCopy;

            editorReturnStageData = safeCopy != null ? safeCopy.Clone() : null;
            editorReturnFilePath = currentFilePath ?? string.Empty;
            editorReturnSaveDirectory = saveDirectory ?? string.Empty;
            editorReturnSaveFileName = saveFileName ?? string.Empty;
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
            StageFilePath = string.Empty;
            ReturnSceneName = string.Empty;
            EditorTestStageData = null;
        }

        public static void Clear()
        {
            ClearGameplayRequest();
        }
    }
}
