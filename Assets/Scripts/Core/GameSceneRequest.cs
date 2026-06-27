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

        public static void RequestBuiltInStage(string stageFilePath)
        {
            HasRequest = true;
            IsCustomLevel = false;
            IsEditorTestPlay = false;
            StageFilePath = stageFilePath;
            ReturnSceneName = "Title";
        }

        public static void RequestCustomStage(string stageFilePath)
        {
            HasRequest = true;
            IsCustomLevel = true;
            IsEditorTestPlay = false;
            StageFilePath = stageFilePath;
            ReturnSceneName = "Title";
        }

        public static void RequestEditorTestStage(string stageFilePath, string returnSceneName)
        {
            HasRequest = true;
            IsCustomLevel = true;
            IsEditorTestPlay = true;
            StageFilePath = stageFilePath;
            ReturnSceneName = string.IsNullOrWhiteSpace(returnSceneName) ? "LevelEditor" : returnSceneName;
        }

        public static void Clear()
        {
            HasRequest = false;
            IsCustomLevel = false;
            IsEditorTestPlay = false;
            StageFilePath = string.Empty;
            ReturnSceneName = string.Empty;
        }
    }
}
