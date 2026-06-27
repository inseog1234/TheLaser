using System;
using System.Collections.Generic;
using System.IO;
using Core;
using Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace UI.Title
{
    public class TitleMenuController : MonoBehaviour
    {
        private sealed class StageEntry
        {
            public string Path;
            public StageData Data;
        }

        [SerializeField] private string gameSceneName = "Game";
        [SerializeField] private string editorSceneName = "LevelEditor";

        private TMP_FontAsset font;
        private Sprite whiteSprite;
        private Canvas canvas;
        private RectTransform root;
        private RectTransform stagePopup;
        private RectTransform customPopup;
        private RectTransform customLevelPopup;
        private RectTransform settingsPopup;
        private string selectedCustomLevelPath;
        private FmodRuntimeAudio audioController;

        private void Awake()
        {
            EnsureEventSystem();
            StageFilePaths.EnsureDefaultDirectories();
            font = Resources.Load<TMP_FontAsset>("Font/TMP/PF스타더스트 3");
            whiteSprite = CreateWhiteSprite();
            audioController = FmodRuntimeAudio.EnsureInstance();
            audioController.ApplySavedVolumes();
            audioController.PlayBgm(FmodRuntimeAudio.BgmTitle);
            BuildUI();
        }

        private void EnsureEventSystem()
        {
            EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject obj = new GameObject("EventSystem");
                eventSystem = obj.AddComponent<EventSystem>();
            }

            StandaloneInputModule oldModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (oldModule != null)
                Destroy(oldModule);

            InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            if (inputModule.actionsAsset == null)
                inputModule.AssignDefaultActions();
        }

        private void BuildUI()
        {
            GameObject canvasObject = new GameObject("TitleCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();
            root = canvasObject.GetComponent<RectTransform>();

            Image background = CreatePanel("Background", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.035f, 0.04f, 0.06f, 1f)).GetComponent<Image>();
            background.raycastTarget = false;

            RectTransform menu = CreatePanel("MainMenu", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-230f, -270f), new Vector2(230f, 270f), new Color(0.08f, 0.09f, 0.13f, 0.92f));
            AddVertical(menu, 24, 24, 28, 28, 16);
            AddText(menu, "THE LASER", 54, TextAlignmentOptions.Center, Color.white, true);
            AddButton(menu, "게임 시작", ShowStagePopup, 380f, 58f);
            AddButton(menu, "커스텀", ShowCustomPopup, 380f, 58f);
            AddButton(menu, "설정", ShowSettingsPopup, 380f, 58f);
            AddButton(menu, "게임 끝내기", QuitGame, 380f, 58f);

            BuildStagePopup();
            BuildCustomPopup();
            BuildCustomLevelPopup();
            BuildSettingsPopup();
            HideAllPopups(false);
        }

        private void BuildStagePopup()
        {
            stagePopup = CreateModal("StageListPopup", 480f, 760f, "스테이지 목록");
            ScrollRect scrollRect = CreateScroll(stagePopup, "StageScroll", out RectTransform content);
            scrollRect.GetComponent<LayoutElement>().preferredHeight = 600f;

            List<StageEntry> stages = LoadBuiltInStageEntries();
            Dictionary<int, List<StageEntry>> byChapter = new();
            for (int i = 0; i < stages.Count; i++)
            {
                int chapter = Mathf.Max(1, stages[i].Data.chapterIndex);
                if (!byChapter.ContainsKey(chapter))
                    byChapter.Add(chapter, new List<StageEntry>());
                byChapter[chapter].Add(stages[i]);
            }

            List<int> keys = new List<int>(byChapter.Keys);
            keys.Sort();
            if (keys.Count <= 0)
                AddText(content, "BuiltInLevels 폴더에 .tls가 없음", 24, TextAlignmentOptions.Center, Color.white, true);

            for (int i = 0; i < keys.Count; i++)
                AddChapterBlock(content, keys[i], byChapter[keys[i]]);

            AddButton(stagePopup, "닫기", HideAllPopups, 420f, 48f);
        }

        private void AddChapterBlock(RectTransform parent, int chapterIndex, List<StageEntry> stages)
        {
            stages.Sort((a, b) => a.Data.stageIndexInChapter.CompareTo(b.Data.stageIndexInChapter));
            RectTransform block = CreatePanel($"Chapter_{chapterIndex}", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.06f, 0.07f, 0.095f, 0.95f));
            AddVertical(block, 12, 12, 12, 12, 8).childForceExpandHeight = false;
            int rowCount = Mathf.Max(1, Mathf.CeilToInt(stages.Count / 5f));
            float stageButtonAreaHeight = rowCount * 46f + Mathf.Max(0, rowCount - 1) * 10f;
            block.gameObject.AddComponent<LayoutElement>().preferredHeight = 88f + stageButtonAreaHeight;
            string chapterName = stages.Count > 0 ? stages[0].Data.chapterName : $"챕터 {chapterIndex}";
            AddText(block, $"챕터 {chapterIndex}\n{chapterName}", 24, TextAlignmentOptions.Left, Color.white, true);

            RectTransform row = CreateUIObject("StageButtons", block);
            GridLayoutGroup grid = row.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(70f, 46f);
            grid.spacing = new Vector2(10f, 10f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = stageButtonAreaHeight;

            for (int i = 0; i < stages.Count; i++)
            {
                StageEntry entry = stages[i];
                bool unlocked = StageProgressManager.IsStageUnlocked(entry.Data);
                Button button = AddButton(row, entry.Data.stageIndexInChapter.ToString(), () => StartStage(entry), 70f, 46f);
                button.interactable = unlocked;
            }
        }

        private void BuildCustomPopup()
        {
            customPopup = CreateModal("CustomPopup", 520f, 320f, "커스텀");
            AddButton(customPopup, "레벨 플레이", ShowCustomLevelPopup, 420f, 58f);
            AddButton(customPopup, "레벨 만들기", OpenLevelEditor, 420f, 58f);
            AddButton(customPopup, "닫기", HideAllPopups, 420f, 44f);
        }

        private void BuildCustomLevelPopup()
        {
            customLevelPopup = CreateModal("CustomLevelPopup", 720f, 620f, "내 커스텀 레벨");
            ScrollRect scroll = CreateScroll(customLevelPopup, "CustomLevelScroll", out RectTransform content);
            scroll.GetComponent<LayoutElement>().preferredHeight = 420f;
            BuildCustomLevelList(content);
            AddButton(customLevelPopup, "플레이", PlaySelectedCustomLevel, 620f, 54f);
            AddButton(customLevelPopup, "닫기", HideAllPopups, 620f, 42f);
        }

        private void BuildCustomLevelList(RectTransform content)
        {
            string[] files = Directory.Exists(StageFilePaths.MyCustomLevelsDirectory) ? Directory.GetFiles(StageFilePaths.MyCustomLevelsDirectory, "*.tls", SearchOption.TopDirectoryOnly) : Array.Empty<string>();
            if (files.Length <= 0)
            {
                AddText(content, "./MyCostomLevels 폴더에 .tls 파일이 없음", 20, TextAlignmentOptions.Center, Color.white, true);
                return;
            }

            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                AddButton(content, Path.GetFileName(file), () => selectedCustomLevelPath = file, 620f, 46f);
            }
        }

        private void BuildSettingsPopup()
        {
            settingsPopup = CreateModal("SettingsPopup", 600f, 460f, "설정");
            AddVolumeSlider(settingsPopup, "마스터 볼륨", "TheLaser_MasterVolume", 1f);
            AddVolumeSlider(settingsPopup, "배경음", "TheLaser_BgmVolume", 1f);
            AddVolumeSlider(settingsPopup, "효과음", "TheLaser_SfxVolume", 1f);
            AddButton(settingsPopup, "닫기", HideAllPopups, 500f, 48f);
        }

        private void AddVolumeSlider(RectTransform parent, string label, string key, float defaultValue)
        {
            AddText(parent, label, 20, TextAlignmentOptions.Left, Color.white, false);
            Slider slider = CreateSlider(parent, PlayerPrefs.GetFloat(key, defaultValue));
            slider.onValueChanged.AddListener(value =>
            {
                PlayerPrefs.SetFloat(key, value);
                PlayerPrefs.Save();
                if (audioController != null)
                    audioController.ApplySavedVolumes();
            });
        }

        private List<StageEntry> LoadBuiltInStageEntries()
        {
            List<StageEntry> result = new();
            if (!Directory.Exists(StageFilePaths.BuiltInLevelsDirectory))
                return result;

            string[] files = Directory.GetFiles(StageFilePaths.BuiltInLevelsDirectory, "*.tls", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                if (StageBinarySerializer.TryLoad(files[i], out StageData data))
                    result.Add(new StageEntry { Path = files[i], Data = data });
            }
            return result;
        }

        private void StartStage(StageEntry entry)
        {
            if (entry == null || entry.Data == null || !StageProgressManager.IsStageUnlocked(entry.Data))
                return;

            PlayUiConfirmation();
            GameSceneRequest.RequestBuiltInStage(entry.Path);
            SceneFadeController.Instance.LoadScene(gameSceneName);
        }

        private void PlaySelectedCustomLevel()
        {
            if (string.IsNullOrWhiteSpace(selectedCustomLevelPath))
                return;

            PlayUiConfirmation();
            GameSceneRequest.RequestCustomStage(selectedCustomLevelPath);
            SceneFadeController.Instance.LoadScene(gameSceneName);
        }

        private void OpenLevelEditor()
        {
            PlayUiConfirmation();
            SceneFadeController.Instance.LoadScene(editorSceneName);
        }

        private void ShowStagePopup()
        {
            HideAllPopups(false);
            if (stagePopup != null)
                stagePopup.gameObject.SetActive(true);
            PlayUiOpen();
        }

        private void ShowCustomPopup()
        {
            HideAllPopups(false);
            if (customPopup != null)
                customPopup.gameObject.SetActive(true);
            PlayUiOpen();
        }

        private void ShowCustomLevelPopup()
        {
            HideAllPopups(false);
            if (customLevelPopup != null)
                customLevelPopup.gameObject.SetActive(true);
            PlayUiOpen();
        }

        private void ShowSettingsPopup()
        {
            HideAllPopups(false);
            if (settingsPopup != null)
                settingsPopup.gameObject.SetActive(true);
            PlayUiOpen();
        }

        private void HideAllPopups()
        {
            HideAllPopups(true);
        }

        private void HideAllPopups(bool playSound)
        {
            bool closedAny = false;

            if (stagePopup != null && stagePopup.gameObject.activeSelf)
            {
                stagePopup.gameObject.SetActive(false);
                closedAny = true;
            }

            if (customPopup != null && customPopup.gameObject.activeSelf)
            {
                customPopup.gameObject.SetActive(false);
                closedAny = true;
            }

            if (customLevelPopup != null && customLevelPopup.gameObject.activeSelf)
            {
                customLevelPopup.gameObject.SetActive(false);
                closedAny = true;
            }

            if (settingsPopup != null && settingsPopup.gameObject.activeSelf)
            {
                settingsPopup.gameObject.SetActive(false);
                closedAny = true;
            }

            if (playSound && closedAny)
                PlayUiClose();
        }

        private void QuitGame()
        {
            PlayUiConfirmation();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void PlayUiClick()
        {
            PlaySfx(FmodRuntimeAudio.SfxUiClick);
        }

        private void PlayUiOpen()
        {
            PlaySfx(FmodRuntimeAudio.SfxUiOpen);
        }

        private void PlayUiClose()
        {
            PlaySfx(FmodRuntimeAudio.SfxUiClose);
        }

        private void PlayUiConfirmation()
        {
            PlaySfx(FmodRuntimeAudio.SfxUiConfirmation);
        }

        private void PlaySfx(string eventPath)
        {
            if (audioController == null)
                audioController = FmodRuntimeAudio.EnsureInstance();

            audioController?.PlaySfx(eventPath);
        }

        private RectTransform CreateModal(string name, float width, float height, string title)
        {
            RectTransform panel = CreatePanel(name, root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-width * 0.5f, -height * 0.5f), new Vector2(width * 0.5f, height * 0.5f), new Color(0.075f, 0.085f, 0.115f, 0.98f));
            AddVertical(panel, 18, 18, 18, 18, 12).childForceExpandHeight = false;
            AddText(panel, title, 32, TextAlignmentOptions.Center, Color.white, true);
            return panel;
        }

        private ScrollRect CreateScroll(RectTransform parent, string name, out RectTransform content)
        {
            RectTransform viewport = CreatePanel(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.035f, 0.04f, 0.055f, 0.9f));
            viewport.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            content = CreateUIObject("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(8f, 0f);
            content.offsetMax = new Vector2(-8f, 0f);
            AddVertical(content, 6, 6, 6, 6, 8).childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return scroll;
        }

        private RectTransform CreatePanel(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            RectTransform rect = CreateUIObject(name, parent);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = whiteSprite;
            image.color = color;
            return rect;
        }

        private RectTransform CreateUIObject(string name, Transform parent)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            return obj.AddComponent<RectTransform>();
        }

        private VerticalLayoutGroup AddVertical(RectTransform parent, int left, int right, int top, int bottom, int spacing)
        {
            VerticalLayoutGroup layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(left, right, top, bottom);
            layout.spacing = spacing;
            layout.childForceExpandWidth = true;
            return layout;
        }

        private TMP_Text AddText(RectTransform parent, string text, int size, TextAlignmentOptions alignment, Color color, bool wrap)
        {
            RectTransform rect = CreateUIObject("Text", parent);
            TMP_Text tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.enableWordWrapping = wrap;
            tmp.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = wrap ? size * 2.2f : size * 1.4f;
            return tmp;
        }

        private Button AddButton(RectTransform parent, string label, UnityEngine.Events.UnityAction onClick, float width, float height)
        {
            RectTransform rect = CreatePanel("Button", parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.16f, 0.18f, 0.25f, 1f));
            LayoutElement le = rect.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            Button button = rect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() =>
            {
                PlayUiClick();
                onClick?.Invoke();
            });
            AddText(rect, label, 22, TextAlignmentOptions.Center, Color.white, false).rectTransform.anchorMin = Vector2.zero;
            rect.GetChild(0).GetComponent<RectTransform>().anchorMin = Vector2.zero;
            rect.GetChild(0).GetComponent<RectTransform>().anchorMax = Vector2.one;
            rect.GetChild(0).GetComponent<RectTransform>().offsetMin = Vector2.zero;
            rect.GetChild(0).GetComponent<RectTransform>().offsetMax = Vector2.zero;
            return button;
        }

        private Slider CreateSlider(RectTransform parent, float value)
        {
            RectTransform root = CreateUIObject("Slider", parent);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;
            RectTransform background = CreatePanel("Background", root, Vector2.zero, Vector2.one, new Vector2(0f, 12f), new Vector2(0f, -12f), new Color(0.03f, 0.035f, 0.045f, 1f));
            RectTransform fill = CreatePanel("Fill", root, Vector2.zero, new Vector2(value, 1f), new Vector2(0f, 12f), new Vector2(0f, -12f), new Color(0.25f, 0.65f, 1f, 1f));
            RectTransform handle = CreatePanel("Handle", root, new Vector2(value, 0.5f), new Vector2(value, 0.5f), new Vector2(-8f, -16f), new Vector2(8f, 16f), Color.white);
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            return slider;
        }

        private Sprite CreateWhiteSprite()
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
