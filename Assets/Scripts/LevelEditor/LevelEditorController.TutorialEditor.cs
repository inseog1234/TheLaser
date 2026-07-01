using System;
using System.Collections.Generic;
using Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LevelEditor
{
    public partial class LevelEditorController
    {
        private void BuildTutorialEditPopup()
        {
            tutorialEditPopup = CreateModalPanel("TutorialEditPopup", 740f, 650f, "튜토리얼 수정");
            tutorialPageInfoText = AddText(tutorialEditPopup, "", 18, TextAlignmentOptions.Left, new Color(0.8f, 0.86f, 0.95f, 1f));
            tutorialPageInfoText.enableWordWrapping = true;
            tutorialPageDropdown = AddDropdownRow(tutorialEditPopup, "페이지", TutorialPageDisplayNames(), 0, index => LoadTutorialPageToInputs(index));
            tutorialTitleInput = AddInputRow(tutorialEditPopup, "튜토리얼 이름", "", value => ApplyTutorialPageInputs());
            tutorialDescriptionInput = AddLargeInputRow(tutorialEditPopup, "설명글", "", TutorialDescriptionCharacterLimit, value => ApplyTutorialPageInputs());
            tutorialDescriptionCounterText = AddText(tutorialEditPopup, "0 / " + TutorialDescriptionCharacterLimit, 15, TextAlignmentOptions.Left, new Color(0.72f, 0.82f, 0.92f, 1f));
            AddButton(tutorialEditPopup, "페이지 추가", AddTutorialPageFromInputs, 620f, 48f);
            AddButton(tutorialEditPopup, "현재 페이지 삭제", RemoveSelectedTutorialPage, 620f, 42f);
            AddButton(tutorialEditPopup, "닫기", () => { ApplyTutorialPageInputs(); HideAllPopups(); }, 620f, 42f);
            tutorialEditPopup.gameObject.SetActive(false);
        }

        private void ShowTutorialEditPopup()
        {
            if (editingStageData == null)
                return;

            HideAllPopups();
            EnsureTutorialPageList();
            RefreshTutorialPageDropdown();
            LoadTutorialPageToInputs(Mathf.Clamp(tutorialPageDropdown != null ? tutorialPageDropdown.value : 0, 0, Mathf.Max(0, editingStageData.tutorialPages.Count - 1)));

            if (tutorialEditPopup != null)
                tutorialEditPopup.gameObject.SetActive(true);

            PlayEditorSfx(FmodRuntimeAudio.SfxUiOpen);
        }

        private void EnsureTutorialPageList()
        {
            if (editingStageData.tutorialPages == null)
                editingStageData.tutorialPages = new List<string>();
        }

        private List<string> TutorialPageDisplayNames()
        {
            List<string> options = new List<string>();
            int count = editingStageData != null && editingStageData.tutorialPages != null ? editingStageData.tutorialPages.Count : 0;

            if (count <= 0)
            {
                options.Add("새 페이지");
                return options;
            }

            for (int i = 0; i < count; i++)
                options.Add($"페이지 {i + 1}");

            return options;
        }

        private void RefreshTutorialPageDropdown()
        {
            if (tutorialPageDropdown == null)
                return;

            List<string> options = TutorialPageDisplayNames();
            int current = Mathf.Clamp(tutorialPageDropdown.value, 0, Mathf.Max(0, options.Count - 1));
            tutorialPageDropdown.ClearOptions();
            tutorialPageDropdown.AddOptions(options);
            tutorialPageDropdown.SetValueWithoutNotify(current);
            tutorialPageDropdown.RefreshShownValue();
            RefreshTutorialPageInfo();
        }

        private void RefreshTutorialPageInfo()
        {
            if (tutorialPageInfoText == null || editingStageData == null)
                return;

            int count = editingStageData.tutorialPages != null ? editingStageData.tutorialPages.Count : 0;
            tutorialPageInfoText.text = count > 0 ? $"현재 튜토리얼 페이지: {count}개" : "아직 튜토리얼 페이지가 없습니다. 내용을 입력하고 페이지 추가를 누르세요.";
        }

        private void LoadTutorialPageToInputs(int index)
        {
            EnsureTutorialPageList();

            string title = "";
            string body = "";

            if (editingStageData.tutorialPages.Count > 0)
                SplitTutorialPage(editingStageData.tutorialPages[Mathf.Clamp(index, 0, editingStageData.tutorialPages.Count - 1)], out title, out body);

            if (tutorialTitleInput != null)
                tutorialTitleInput.SetTextWithoutNotify(title);

            if (tutorialDescriptionInput != null)
                tutorialDescriptionInput.SetTextWithoutNotify(body);

            RefreshTutorialDescriptionCounter();
            RefreshTutorialPageInfo();
        }

        private void ApplyTutorialPageInputs()
        {
            if (editingStageData == null || tutorialPageDropdown == null)
                return;

            EnsureTutorialPageList();

            if (editingStageData.tutorialPages.Count <= 0)
            {
                RefreshTutorialDescriptionCounter();
                RefreshTutorialPageInfo();
                return;
            }

            int index = Mathf.Clamp(tutorialPageDropdown.value, 0, editingStageData.tutorialPages.Count - 1);
            editingStageData.tutorialPages[index] = ComposeTutorialPage(tutorialTitleInput != null ? tutorialTitleInput.text : "", tutorialDescriptionInput != null ? tutorialDescriptionInput.text : "");
            editingStageData.hasTutorial = editingStageData.tutorialPages.Count > 0;
            RefreshTutorialDescriptionCounter();
            RefreshRuntimeSettingsPanel();
        }

        private void AddTutorialPageFromInputs()
        {
            if (editingStageData == null)
                return;

            EnsureTutorialPageList();
            string page = ComposeTutorialPage(tutorialTitleInput != null ? tutorialTitleInput.text : "", tutorialDescriptionInput != null ? tutorialDescriptionInput.text : "");
            if (string.IsNullOrWhiteSpace(page))
                page = $"튜토리얼 {editingStageData.tutorialPages.Count + 1}";

            editingStageData.tutorialPages.Add(page);
            editingStageData.hasTutorial = true;
            RefreshTutorialPageDropdown();

            if (tutorialPageDropdown != null)
            {
                tutorialPageDropdown.SetValueWithoutNotify(editingStageData.tutorialPages.Count - 1);
                tutorialPageDropdown.RefreshShownValue();
            }

            LoadTutorialPageToInputs(editingStageData.tutorialPages.Count - 1);
            RefreshRuntimeSettingsPanel();
            SetStatus("튜토리얼 페이지 추가 완료");
        }

        private void RemoveSelectedTutorialPage()
        {
            if (editingStageData == null || tutorialPageDropdown == null)
                return;

            EnsureTutorialPageList();
            if (editingStageData.tutorialPages.Count <= 0)
                return;

            int index = Mathf.Clamp(tutorialPageDropdown.value, 0, editingStageData.tutorialPages.Count - 1);
            editingStageData.tutorialPages.RemoveAt(index);
            editingStageData.hasTutorial = editingStageData.tutorialPages.Count > 0;
            RefreshTutorialPageDropdown();
            LoadTutorialPageToInputs(Mathf.Clamp(index, 0, Mathf.Max(0, editingStageData.tutorialPages.Count - 1)));
            RefreshRuntimeSettingsPanel();
            SetStatus("튜토리얼 페이지 삭제 완료");
        }

        private static string ComposeTutorialPage(string title, string body)
        {
            title = string.IsNullOrWhiteSpace(title) ? "" : title.Trim();
            body = string.IsNullOrWhiteSpace(body) ? "" : body.Trim();

            if (string.IsNullOrWhiteSpace(title))
                return body;

            if (string.IsNullOrWhiteSpace(body))
                return title;

            return title + "\n" + body;
        }

        private static void SplitTutorialPage(string page, out string title, out string body)
        {
            title = "";
            body = "";

            if (string.IsNullOrWhiteSpace(page))
                return;

            string normalized = page.Replace("\r\n", "\n");
            int lineBreakIndex = normalized.IndexOf('\n');
            if (lineBreakIndex < 0)
            {
                title = normalized.Trim();
                return;
            }

            title = normalized.Substring(0, lineBreakIndex).Trim();
            body = normalized.Substring(lineBreakIndex + 1).Trim();
        }

        private void RefreshTutorialDescriptionCounter()
        {
            if (tutorialDescriptionCounterText == null || tutorialDescriptionInput == null)
                return;

            int length = tutorialDescriptionInput.text != null ? tutorialDescriptionInput.text.Length : 0;
            tutorialDescriptionCounterText.text = length + " / " + TutorialDescriptionCharacterLimit;
            tutorialDescriptionCounterText.color = length >= TutorialDescriptionCharacterLimit ? new Color(1f, 0.55f, 0.45f, 1f) : new Color(0.72f, 0.82f, 0.92f, 1f);
        }

        private TMP_InputField AddLargeInputRow(Transform parent, string label, string value, int characterLimit, Action<string> onChanged)
        {
            RectTransform row = CreateUIObject(label + "LargeInputRow", parent);
            VerticalLayoutGroup vertical = AddVerticalLayout(row, 0, 0, 0, 0, 4);
            vertical.childForceExpandHeight = false;

            LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 178f;

            TMP_Text labelText = AddText(row, label, 17, TextAlignmentOptions.Left, new Color(0.86f, 0.9f, 1f, 1f));
            labelText.GetComponent<LayoutElement>().preferredHeight = 26f;

            TMP_InputField input = CreateInputField(row, value);
            input.lineType = TMP_InputField.LineType.MultiLineNewline;
            input.characterLimit = Mathf.Max(1, characterLimit);
            input.textComponent.enableWordWrapping = true;
            input.textComponent.overflowMode = TextOverflowModes.Overflow;

            LayoutElement inputLayout = input.GetComponent<LayoutElement>();
            if (inputLayout != null)
                inputLayout.preferredHeight = 132f;

            input.textComponent.rectTransform.offsetMin = new Vector2(8f, 8f);
            input.textComponent.rectTransform.offsetMax = new Vector2(-8f, -8f);
            input.onValueChanged.AddListener(text => { onChanged?.Invoke(text); RefreshTutorialDescriptionCounter(); });
            input.onEndEdit.AddListener(text => { PlayEditorSfx(FmodRuntimeAudio.SfxEditorCheckBox); onChanged?.Invoke(text); RefreshTutorialDescriptionCounter(); });
            runtimeSettingSelectables.Add(input);
            return input;
        }
    }
}
