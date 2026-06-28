#if UNITY_EDITOR
using System.IO;
using Core;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    public static class BuiltInLevelResourceBuilder
    {
        private const string MenuPath = "The Laser/BuiltInLevels를 Resources로 내장";
        private const string ResourceAssetDirectory = "Assets/Resources/BuiltInLevels";

        [MenuItem(MenuPath)]
        public static void BuildResources()
        {
            Directory.CreateDirectory(ResourceAssetDirectory);

            string sourceDirectory = StageFilePaths.BuiltInLevelsDirectory;
            if (!Directory.Exists(sourceDirectory))
            {
                Debug.LogWarning($"[BuiltInLevelResourceBuilder] BuiltInLevels 폴더가 없습니다: {sourceDirectory}");
                return;
            }

            string[] oldFiles = Directory.GetFiles(ResourceAssetDirectory, "*.bytes", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < oldFiles.Length; i++)
                File.Delete(oldFiles[i]);

            string[] tlsFiles = Directory.GetFiles(sourceDirectory, "*.tls", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < tlsFiles.Length; i++)
            {
                string fileName = Path.GetFileNameWithoutExtension(tlsFiles[i]) + ".bytes";
                string destination = Path.Combine(ResourceAssetDirectory, fileName);
                File.Copy(tlsFiles[i], destination, true);
            }

            AssetDatabase.Refresh();
            Debug.Log($"[BuiltInLevelResourceBuilder] {tlsFiles.Length}개 스테이지를 Resources/BuiltInLevels에 내장했습니다.");
        }
    }
}
#endif
