using System.IO;
using Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LevelEditor
{
    public partial class LevelEditorController
    {
        public void OpenExportDirectory()
        {
            Directory.CreateDirectory(ExportDirectory);
            string path = Path.GetFullPath(ExportDirectory).Replace("\\", "/");
            Application.OpenURL("file:///" + path);
        }

        private void PickLoadFile()
        {
            if (!CanOpenNativeFileDialog())
                return;

            BeginNativeFileDialog();
            string path = NativeFileDialogUtility.OpenTlsFilePanel("불러올 .tls 선택", StageFilePaths.MyCustomLevelsDirectory);
            EndNativeFileDialog();

            if (!string.IsNullOrWhiteSpace(path))
            {
                selectedLoadFilePath = path;
                if (loadPathInput != null)
                    loadPathInput.SetTextWithoutNotify(path);

                ClearCurrentUiSelection();
            }
        }

        private void PickSaveFolder()
        {
            if (!CanOpenNativeFileDialog())
                return;

            BeginNativeFileDialog();
            string path = NativeFileDialogUtility.OpenFolderPanel("저장할 폴더 선택", StageFilePaths.MyCustomLevelsDirectory);
            EndNativeFileDialog();

            if (!string.IsNullOrWhiteSpace(path))
            {
                selectedSaveDirectory = path;
                if (saveDirectoryInput != null)
                    saveDirectoryInput.SetTextWithoutNotify(path);

                ClearCurrentUiSelection();
            }
        }

        private bool CanOpenNativeFileDialog()
        {
            if (isNativeFileDialogOpen)
                return false;

            return Time.unscaledTime - lastNativeFileDialogClosedTime > 0.25f;
        }

        private void BeginNativeFileDialog()
        {
            isNativeFileDialogOpen = true;
            ClearCurrentUiSelection();
        }

        private void EndNativeFileDialog()
        {
            isNativeFileDialogOpen = false;
            lastNativeFileDialogClosedTime = Time.unscaledTime;
            ClearCurrentUiSelection();
        }

        private void ClearCurrentUiSelection()
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
