using System.IO;
using UnityEngine;

namespace Core
{
    public static class StageFilePaths
    {
        public const string StageExtension = ".tls";
        public const string BuiltInLevelsDirectoryName = "BuiltInLevels";
        public const string MyCustomLevelsDirectoryName = "MyCostomLevels";
        public const string BuiltInLevelsResourcePath = "BuiltInLevels";
        public const string BuiltInResourcePrefix = "builtin:";

        public static string ExeRootDirectory
        {
            get
            {
#if UNITY_EDITOR
                return Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
#else
                return Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
#endif
            }
        }

        public static string BuiltInLevelsDirectory => Path.Combine(ExeRootDirectory, BuiltInLevelsDirectoryName);
        public static string MyCustomLevelsDirectory => Path.Combine(ExeRootDirectory, MyCustomLevelsDirectoryName);

        public static void EnsureDefaultDirectories()
        {
#if UNITY_EDITOR
            Directory.CreateDirectory(BuiltInLevelsDirectory);
#endif
            Directory.CreateDirectory(MyCustomLevelsDirectory);
        }

        public static string NormalizeStageFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "Stage" + StageExtension;

            return Path.GetExtension(fileName) == StageExtension ? fileName : fileName + StageExtension;
        }

        public static bool IsBuiltInResourcePath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && path.StartsWith(BuiltInResourcePrefix, System.StringComparison.OrdinalIgnoreCase);
        }

        public static string ToBuiltInResourcePath(string resourceKey)
        {
            if (string.IsNullOrWhiteSpace(resourceKey))
                return string.Empty;

            if (IsBuiltInResourcePath(resourceKey))
                return resourceKey;

            return BuiltInResourcePrefix + resourceKey;
        }

        public static string ExtractBuiltInResourceKey(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            if (IsBuiltInResourcePath(path))
                return path.Substring(BuiltInResourcePrefix.Length);

            return path;
        }

        public static string GetBuiltInResourceLoadPath(string resourceKey)
        {
            string key = ExtractBuiltInResourceKey(resourceKey);
            if (string.IsNullOrWhiteSpace(key))
                return BuiltInLevelsResourcePath;

            return BuiltInLevelsResourcePath + "/" + Path.GetFileNameWithoutExtension(key);
        }
    }
}
