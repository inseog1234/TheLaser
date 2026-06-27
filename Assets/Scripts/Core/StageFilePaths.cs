using System.IO;
using UnityEngine;

namespace Core
{
    public static class StageFilePaths
    {
        public const string StageExtension = ".tls";
        public const string BuiltInLevelsDirectoryName = "BuiltInLevels";
        public const string MyCustomLevelsDirectoryName = "MyCostomLevels";

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
            Directory.CreateDirectory(BuiltInLevelsDirectory);
            Directory.CreateDirectory(MyCustomLevelsDirectory);
        }

        public static string NormalizeStageFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "Stage" + StageExtension;

            return Path.GetExtension(fileName) == StageExtension ? fileName : fileName + StageExtension;
        }
    }
}
