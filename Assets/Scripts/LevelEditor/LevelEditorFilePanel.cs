using TMPro;
using UnityEngine;

namespace LevelEditor
{
    public class LevelEditorFilePanel : MonoBehaviour
    {
        [SerializeField] private LevelEditorController controller;
        [SerializeField] private TMP_InputField exportDirectoryInput;
        [SerializeField] private TMP_InputField exportFileNameInput;
        [SerializeField] private TMP_InputField loadFilePathInput;

        private void Awake()
        {
            if (controller == null)
                controller = FindFirstObjectByType<LevelEditorController>();
        }

        public void ApplyExportDirectory()
        {
            if (controller != null && exportDirectoryInput != null)
                controller.SetExportDirectory(exportDirectoryInput.text);
        }

        public void ApplyExportFileName()
        {
            if (controller != null && exportFileNameInput != null)
                controller.SetExportFileName(exportFileNameInput.text);
        }

        public void ApplyLoadFilePath()
        {
            if (controller != null && loadFilePathInput != null)
                controller.SetLoadFilePath(loadFilePathInput.text);
        }

        public void ExportLevel()
        {
            ApplyExportDirectory();
            ApplyExportFileName();
            controller?.ExportLevel();
        }

        public void LoadLevel()
        {
            ApplyLoadFilePath();
            controller?.LoadLevelFromInputPath();
        }

        public void OpenExportDirectory()
        {
            controller?.OpenExportDirectory();
        }
    }
}
