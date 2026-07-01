using Audio;
using Core;
using UnityEngine;

namespace UI.InGame
{
    public partial class InGameStageFlowController
    {
        private void ShowTutorialPopup()
        {
            tutorialPageIndex = 0;
            tutorialOpen = true;
            if (playerController != null) playerController.SetControlsEnabled(false);
            HideAllPopups();
            tutorialPopup.gameObject.SetActive(true);
            RefreshTutorialPopup();
        }

        private void NextTutorialPage()
        {
            tutorialPageIndex++;
            if (currentStage == null || currentStage.tutorialPages == null || tutorialPageIndex >= currentStage.tutorialPages.Count)
            {
                tutorialOpen = false;
                tutorialPopup.gameObject.SetActive(false);
                if (playerController != null) playerController.SetControlsEnabled(true);
                return;
            }
            RefreshTutorialPopup();
        }

        private void HandlePausePressed()
        {
            if (lastPauseInputFrame == Time.frameCount)
                return;

            lastPauseInputFrame = Time.frameCount;

            if (isJumpingIntoHole)
                return;

            if (tutorialOpen)
            {
                tutorialOpen = false;
                if (tutorialPopup != null)
                    tutorialPopup.gameObject.SetActive(false);
            }

            if (pauseOpen) ClosePause(); else ShowPausePopup();
        }

        private void ShowPausePopup()
        {
            pauseOpen = true;
            Time.timeScale = 0f;
            if (playerController != null) playerController.SetControlsEnabled(false);
            HideAllPopups();

            RectTransform targetPopup = GameSceneRequest.IsEditorTestPlay ? testPausePopup : pausePopup;
            if (targetPopup == null)
                targetPopup = pausePopup;

            if (targetPopup != null)
                targetPopup.gameObject.SetActive(true);

            if (audioController != null)
                audioController.PlaySfx(FmodRuntimeAudio.SfxUiOpen);
        }

        private void ClosePause()
        {
            pauseOpen = false;
            Time.timeScale = 1f;
            HideAllPopups();
            if (playerController != null && !tutorialOpen) playerController.SetControlsEnabled(true);

            if (audioController != null)
                audioController.PlaySfx(FmodRuntimeAudio.SfxUiClose);
        }

        private void ShowSettingsPopup()
        {
            pausePopup.gameObject.SetActive(false);
            testPausePopup.gameObject.SetActive(false);
            settingsPopup.gameObject.SetActive(true);
        }

        private void StopEditorTestPlay()
        {
            Time.timeScale = 1f;
            string returnScene = string.IsNullOrWhiteSpace(GameSceneRequest.ReturnSceneName) ? "LevelEditor" : GameSceneRequest.ReturnSceneName;
            GameSceneRequest.ClearGameplayRequest();
            SceneFadeController.Instance.LoadScene(returnScene);
        }

        private void ReturnToTitle()
        {
            Time.timeScale = 1f;
            GameSceneRequest.Clear();
            SceneFadeController.Instance.LoadScene(titleSceneName);
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void HideAllPopups()
        {
            if (pausePopup != null) pausePopup.gameObject.SetActive(false);
            if (testPausePopup != null) testPausePopup.gameObject.SetActive(false);
            if (settingsPopup != null) settingsPopup.gameObject.SetActive(false);
            if (tutorialPopup != null) tutorialPopup.gameObject.SetActive(false);
        }
    }
}
