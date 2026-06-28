using System;
using System.Collections;
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
            public string ResourceKey;
            public bool IsBuiltInResource;
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
        private RectTransform customDownloadPopup;
        private RectTransform customDownloadContent;
        private RectTransform customManagePopup;
        private RectTransform customManageContent;
        private RectTransform customEditPopup;
        private RectTransform settingsPopup;
        private string selectedCustomLevelPath;
        private CustomLevelPostData selectedDownloadPost;
        private CustomLevelPostData selectedManagedPost;
        private Button downloadSelectedButton;
        private Button managePlayButton;
        private Button manageDeleteButton;
        private Button manageEditButton;
        private Button editSaveButton;
        private TMP_InputField editTitleInput;
        private TMP_InputField editDescriptionInput;
        private TMP_InputField editMapPathInput;
        private TMP_Text editValidationText;
        private string editSelectedMapPath;
        private StageData editSelectedStageData;
        private TMP_Text titleOnlineNoticeText;
        private Coroutine titleNoticeRoutine;
        private readonly List<CustomLevelPostData> downloadedPosts = new();
        private readonly List<CustomLevelPostData> managedPosts = new();
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
            BuildCustomDownloadPopup();
            BuildCustomManagePopup();
            BuildCustomEditPopup();
            BuildSettingsPopup();
            BuildTitleOnlineNotice();
            HideAllPopups(false);
        }

        private void BuildStagePopup()
        {
            stagePopup = CreateModal("StageListPopup", 480f, 830f, "스테이지 목록");
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
                AddText(content, "내장 스테이지가 없음\nAssets/Resources/BuiltInLevels에 .bytes가 필요함", 24, TextAlignmentOptions.Center, Color.white, true);

            for (int i = 0; i < keys.Count; i++)
                AddChapterBlock(content, keys[i], byChapter[keys[i]]);

            TMP_Text unlockNotice = AddText(stagePopup, "모든 챕터는 5스테이지까지 진행하면 다음 챕터가 해금 됩니다.", 16, TextAlignmentOptions.Center, new Color(0.55f, 0.9f, 1f, 1f), true);
            unlockNotice.GetComponent<LayoutElement>().preferredHeight = 42f;
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
            customPopup = CreateModal("CustomPopup", 520f, 470f, "커스텀");
            AddButton(customPopup, "레벨 플레이", ShowCustomLevelPopup, 420f, 58f);
            AddButton(customPopup, "내 레벨 관리", ShowCustomManagePopup, 420f, 58f);
            AddButton(customPopup, "레벨 다운로드", ShowCustomDownloadPopup, 420f, 58f);
            AddButton(customPopup, "레벨 에디터", OpenLevelEditor, 420f, 58f);
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

        private void BuildCustomDownloadPopup()
        {
            customDownloadPopup = CreateModal("CustomDownloadPopup", 860f, 700f, "레벨 다운로드");
            ScrollRect scroll = CreateScroll(customDownloadPopup, "CustomDownloadScroll", out customDownloadContent);
            scroll.GetComponent<LayoutElement>().preferredHeight = 500f;
            downloadSelectedButton = AddButton(customDownloadPopup, "다운로드", DownloadSelectedOnlineLevel, 760f, 54f);
            downloadSelectedButton.interactable = false;
            AddButton(customDownloadPopup, "새로고침", RefreshOnlineLevelList, 760f, 44f);
            AddButton(customDownloadPopup, "닫기", HideAllPopups, 760f, 42f);
        }

        private void BuildCustomManagePopup()
        {
            customManagePopup = CreateModal("CustomManagePopup", 900f, 760f, "내 레벨 관리");
            ScrollRect scroll = CreateScroll(customManagePopup, "CustomManageScroll", out customManageContent);
            scroll.GetComponent<LayoutElement>().preferredHeight = 470f;
            managePlayButton = AddButton(customManagePopup, "플레이", PlaySelectedManagedPost, 800f, 50f);
            manageDeleteButton = AddButton(customManagePopup, "삭제", DeleteSelectedManagedPost, 800f, 46f);
            manageEditButton = AddButton(customManagePopup, "게시물 수정", ShowEditManagedPostPopup, 800f, 46f);
            AddButton(customManagePopup, "새로고침", RefreshManagedLevelList, 800f, 42f);
            AddButton(customManagePopup, "닫기", HideAllPopups, 800f, 42f);
            SetManageButtonsInteractable(false);
        }

        private void BuildCustomEditPopup()
        {
            customEditPopup = CreateModal("CustomEditPopup", 780f, 650f, "게시물 수정");
            editTitleInput = AddInputRow(customEditPopup, "게시물 이름", "", value => RefreshEditSaveButton());
            editTitleInput.characterLimit = SupabaseConfig.PostTitleCharacterLimit;
            editDescriptionInput = AddInputRow(customEditPopup, "게시물 설명", "", value => RefreshEditSaveButton());
            editDescriptionInput.lineType = TMP_InputField.LineType.MultiLineNewline;
            editDescriptionInput.characterLimit = SupabaseConfig.PostDescriptionCharacterLimit;
            editDescriptionInput.GetComponent<LayoutElement>().preferredHeight = 110f;
            editDescriptionInput.textComponent.enableWordWrapping = true;
            editDescriptionInput.textComponent.overflowMode = TextOverflowModes.Overflow;
            editMapPathInput = AddInputRow(customEditPopup, "교체 맵", "선택 안 함", value => { });
            editMapPathInput.interactable = false;
            AddButton(customEditPopup, "맵 파일 선택", PickEditMapFile, 680f, 46f);
            editValidationText = AddText(customEditPopup, "게시물 이름과 설명을 입력하세요. 맵을 교체하지 않으면 기존 맵을 유지합니다.", 17, TextAlignmentOptions.Left, new Color(0.85f, 0.9f, 1f, 1f), true);
            editValidationText.GetComponent<LayoutElement>().preferredHeight = 70f;
            editSaveButton = AddButton(customEditPopup, "수정 저장", UpdateSelectedManagedPost, 680f, 52f);
            AddButton(customEditPopup, "취소", ShowCustomManagePopup, 680f, 42f);
            RefreshEditSaveButton();
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
            List<BuiltInStageEntry> resourceEntries = BuiltInStageLoader.LoadEntries(true);
            for (int i = 0; i < resourceEntries.Count; i++)
            {
                BuiltInStageEntry resourceEntry = resourceEntries[i];
                if (resourceEntry == null || resourceEntry.Data == null)
                    continue;

                result.Add(new StageEntry
                {
                    Path = StageFilePaths.ToBuiltInResourcePath(resourceEntry.ResourceKey),
                    ResourceKey = resourceEntry.ResourceKey,
                    IsBuiltInResource = true,
                    Data = resourceEntry.Data
                });
            }

#if UNITY_EDITOR
            if (result.Count <= 0 && Directory.Exists(StageFilePaths.BuiltInLevelsDirectory))
            {
                string[] files = Directory.GetFiles(StageFilePaths.BuiltInLevelsDirectory, "*.tls", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < files.Length; i++)
                {
                    if (StageBinarySerializer.TryLoad(files[i], out StageData data))
                        result.Add(new StageEntry { Path = files[i], IsBuiltInResource = false, Data = data });
                }
            }
#endif
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

        private void RefreshCustomLevelPopupList()
        {
            if (customLevelPopup == null)
                return;

            ScrollRect scroll = customLevelPopup.GetComponentInChildren<ScrollRect>();
            if (scroll == null || scroll.content == null)
                return;

            ClearChildren(scroll.content);
            BuildCustomLevelList(scroll.content);
        }

        private void ShowCustomDownloadPopup()
        {
            HideAllPopups(false);
            if (customDownloadPopup != null)
                customDownloadPopup.gameObject.SetActive(true);
            PlayUiOpen();
            RefreshOnlineLevelList();
        }

        private void RefreshOnlineLevelList()
        {
            selectedDownloadPost = null;
            if (downloadSelectedButton != null)
                downloadSelectedButton.interactable = false;

            if (customDownloadContent != null)
            {
                ClearChildren(customDownloadContent);
                AddText(customDownloadContent, "목록 불러오는 중...", 22, TextAlignmentOptions.Center, Color.white, true);
            }

            StartCoroutine(SupabaseCustomLevelService.Instance.FetchCustomLevels((success, message, posts) =>
            {
                if (!success)
                {
                    ShowTitleNotice(message, true);
                    if (customDownloadContent != null)
                    {
                        ClearChildren(customDownloadContent);
                        AddText(customDownloadContent, message, 20, TextAlignmentOptions.Center, new Color(1f, 0.45f, 0.45f, 1f), true);
                    }
                    return;
                }

                downloadedPosts.Clear();
                if (posts != null)
                    downloadedPosts.AddRange(posts);
                RebuildOnlineLevelList();
            }));
        }

        private void RebuildOnlineLevelList()
        {
            if (customDownloadContent == null)
                return;

            ClearChildren(customDownloadContent);
            if (downloadedPosts.Count <= 0)
            {
                AddText(customDownloadContent, "업로드된 레벨이 없습니다.", 22, TextAlignmentOptions.Center, Color.white, true);
                return;
            }

            for (int i = 0; i < downloadedPosts.Count; i++)
            {
                CustomLevelPostData post = downloadedPosts[i];
                RectTransform card = CreatePanel("OnlineLevelCard", customDownloadContent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.08f, 0.095f, 0.13f, 1f));
                card.gameObject.AddComponent<LayoutElement>().preferredHeight = 178f;
                AddVertical(card, 10, 10, 8, 8, 4).childForceExpandHeight = false;
                AddText(card, post.DisplayTitle, 24, TextAlignmentOptions.Left, Color.white, false);
                AddText(card, "닉네임: " + post.DisplayNickname, 17, TextAlignmentOptions.Left, new Color(0.7f, 0.9f, 1f, 1f), false);
                TMP_Text desc = AddText(card, post.DisplayDescription, 16, TextAlignmentOptions.Left, new Color(0.84f, 0.87f, 0.94f, 1f), true);
                desc.GetComponent<LayoutElement>().preferredHeight = 48f;
                RectTransform bottom = CreateUIObject("OnlineCardBottom", card);
                bottom.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
                AddHorizontal(bottom, 0, 0, 0, 0, 8);
                AddButton(bottom, post.LikeText, () => ToggleLike(post), 180f, 36f);
                AddButton(bottom, "선택", () => SelectOnlinePost(post), 140f, 36f);
            }
        }

        private void SelectOnlinePost(CustomLevelPostData post)
        {
            selectedDownloadPost = post;
            if (downloadSelectedButton != null)
                downloadSelectedButton.interactable = selectedDownloadPost != null;
            ShowTitleNotice("선택됨: " + (post != null ? post.DisplayTitle : "없음"), false);
        }

        private void DownloadSelectedOnlineLevel()
        {
            if (selectedDownloadPost == null)
                return;

            ShowTitleNotice("다운로드중...", false);
            bool success = SupabaseCustomLevelService.Instance.SaveDownloadedLevel(selectedDownloadPost, out string path, out string error);
            if (!success)
            {
                ShowTitleNotice(error, true);
                return;
            }

            ShowTitleNotice("다운로드 완료!", false);
            selectedCustomLevelPath = path;
            RefreshCustomLevelPopupList();
        }

        private void ToggleLike(CustomLevelPostData post)
        {
            if (post == null)
                return;

            ShowTitleNotice(post.liked_by_me ? "좋아요 취소 중..." : "좋아요 처리 중...", false);
            StartCoroutine(SupabaseCustomLevelService.Instance.ToggleLike(post, (success, message, updatedPost) =>
            {
                ShowTitleNotice(message, !success);
                if (success)
                    RebuildOnlineLevelList();
            }));
        }

        private void ShowCustomManagePopup()
        {
            HideAllPopups(false);
            if (customManagePopup != null)
                customManagePopup.gameObject.SetActive(true);
            PlayUiOpen();
            RefreshManagedLevelList();
        }

        private void RefreshManagedLevelList()
        {
            selectedManagedPost = null;
            SetManageButtonsInteractable(false);

            if (customManageContent != null)
            {
                ClearChildren(customManageContent);
                AddText(customManageContent, "내 게시물 불러오는 중...", 22, TextAlignmentOptions.Center, Color.white, true);
            }

            StartCoroutine(SupabaseCustomLevelService.Instance.FetchMyCustomLevels((success, message, posts) =>
            {
                if (!success)
                {
                    ShowTitleNotice(message, true);
                    if (customManageContent != null)
                    {
                        ClearChildren(customManageContent);
                        AddText(customManageContent, message, 20, TextAlignmentOptions.Center, new Color(1f, 0.45f, 0.45f, 1f), true);
                    }
                    return;
                }

                managedPosts.Clear();
                if (posts != null)
                    managedPosts.AddRange(posts);
                RebuildManagedLevelList();
            }));
        }

        private void RebuildManagedLevelList()
        {
            if (customManageContent == null)
                return;

            ClearChildren(customManageContent);
            if (managedPosts.Count <= 0)
            {
                AddText(customManageContent, "이 기기에서 업로드한 게시물이 없습니다.", 22, TextAlignmentOptions.Center, Color.white, true);
                return;
            }

            for (int i = 0; i < managedPosts.Count; i++)
            {
                CustomLevelPostData post = managedPosts[i];
                RectTransform card = CreatePanel("ManagedLevelCard", customManageContent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.08f, 0.095f, 0.13f, 1f));
                card.gameObject.AddComponent<LayoutElement>().preferredHeight = 156f;
                AddVertical(card, 10, 10, 8, 8, 4).childForceExpandHeight = false;
                AddText(card, post.DisplayTitle, 23, TextAlignmentOptions.Left, Color.white, false);
                AddText(card, "닉네임: " + post.DisplayNickname + "    " + post.LikeText, 16, TextAlignmentOptions.Left, new Color(0.7f, 0.9f, 1f, 1f), false);
                TMP_Text desc = AddText(card, post.DisplayDescription, 16, TextAlignmentOptions.Left, new Color(0.84f, 0.87f, 0.94f, 1f), true);
                desc.GetComponent<LayoutElement>().preferredHeight = 48f;
                AddButton(card, "선택", () => SelectManagedPost(post), 760f, 32f);
            }
        }

        private void SelectManagedPost(CustomLevelPostData post)
        {
            selectedManagedPost = post;
            SetManageButtonsInteractable(post != null);
            ShowTitleNotice("관리 선택됨: " + (post != null ? post.DisplayTitle : "없음"), false);
        }

        private void SetManageButtonsInteractable(bool interactable)
        {
            if (managePlayButton != null) managePlayButton.interactable = interactable;
            if (manageDeleteButton != null) manageDeleteButton.interactable = interactable;
            if (manageEditButton != null) manageEditButton.interactable = interactable;
        }

        private void PlaySelectedManagedPost()
        {
            if (selectedManagedPost == null)
                return;

            bool success = SupabaseCustomLevelService.Instance.SaveDownloadedLevel(selectedManagedPost, out string path, out string error);
            if (!success)
            {
                ShowTitleNotice(error, true);
                return;
            }

            selectedCustomLevelPath = path;
            PlaySelectedCustomLevel();
        }

        private void DeleteSelectedManagedPost()
        {
            if (selectedManagedPost == null)
                return;

            ShowTitleNotice("삭제 중...", false);
            StartCoroutine(SupabaseCustomLevelService.Instance.DeleteCustomLevelPost(selectedManagedPost, (success, message) =>
            {
                ShowTitleNotice(message, !success);
                if (success)
                    RefreshManagedLevelList();
            }));
        }

        private void ShowEditManagedPostPopup()
        {
            if (selectedManagedPost == null)
                return;

            HideAllPopups(false);
            if (customEditPopup != null)
                customEditPopup.gameObject.SetActive(true);

            editSelectedMapPath = string.Empty;
            editSelectedStageData = null;
            if (editTitleInput != null) editTitleInput.SetTextWithoutNotify(selectedManagedPost.DisplayTitle);
            if (editDescriptionInput != null) editDescriptionInput.SetTextWithoutNotify(selectedManagedPost.DisplayDescription);
            if (editMapPathInput != null) editMapPathInput.SetTextWithoutNotify("선택 안 함");
            RefreshEditSaveButton();
            PlayUiOpen();
        }

        private void PickEditMapFile()
        {
            string path = NativeFileDialogUtility.OpenTlsFilePanel("교체할 .tls 선택", StageFilePaths.MyCustomLevelsDirectory);
            if (string.IsNullOrWhiteSpace(path))
                return;

            editSelectedMapPath = path;
            editSelectedStageData = null;

            if (!StageBinarySerializer.TryLoad(path, out StageData data))
            {
                if (editMapPathInput != null) editMapPathInput.SetTextWithoutNotify(Path.GetFileName(path) + " / 불러오기 실패");
                ShowTitleNotice("맵 파일을 불러올 수 없습니다.", true);
                RefreshEditSaveButton();
                return;
            }

            if (!data.HasSolution)
            {
                if (editMapPathInput != null) editMapPathInput.SetTextWithoutNotify(Path.GetFileName(path) + " / 답안 없음");
                ShowTitleNotice("답안이 없는 맵은 교체 업로드할 수 없습니다.", true);
                RefreshEditSaveButton();
                return;
            }

            editSelectedStageData = data;
            if (editMapPathInput != null) editMapPathInput.SetTextWithoutNotify(path);
            ShowTitleNotice("교체 맵 선택 완료", false);
            RefreshEditSaveButton();
        }

        private void RefreshEditSaveButton()
        {
            bool hasText = selectedManagedPost != null && editTitleInput != null && editDescriptionInput != null &&
                !string.IsNullOrWhiteSpace(editTitleInput.text) && !string.IsNullOrWhiteSpace(editDescriptionInput.text);
            bool mapValid = string.IsNullOrWhiteSpace(editSelectedMapPath) || editSelectedStageData != null;
            if (editSaveButton != null)
                editSaveButton.interactable = hasText && mapValid;

            if (editValidationText != null)
            {
                if (!hasText)
                    editValidationText.text = "게시물 이름과 설명을 입력하세요.";
                else if (!mapValid)
                    editValidationText.text = "교체하려는 맵에 답안이 없습니다. 다른 .tls를 선택하세요.";
                else if (string.IsNullOrWhiteSpace(editSelectedMapPath))
                    editValidationText.text = "수정 준비 완료 / 맵은 기존 파일 유지";
                else
                    editValidationText.text = "수정 준비 완료 / 맵 교체 포함";
            }
        }

        private void UpdateSelectedManagedPost()
        {
            RefreshEditSaveButton();
            if (editSaveButton == null || !editSaveButton.interactable || selectedManagedPost == null)
                return;

            ShowTitleNotice("수정 중...", false);
            string mapPath = string.IsNullOrWhiteSpace(editSelectedMapPath) ? string.Empty : editSelectedMapPath;
            StageData mapData = string.IsNullOrWhiteSpace(mapPath) ? null : editSelectedStageData;
            StartCoroutine(SupabaseCustomLevelService.Instance.UpdateCustomLevelPost(selectedManagedPost, editTitleInput.text.Trim(), editDescriptionInput.text.Trim(), mapPath, mapData, (success, message) =>
            {
                ShowTitleNotice(message, !success);
                if (success)
                {
                    HideAllPopups(false);
                    if (customManagePopup != null)
                        customManagePopup.gameObject.SetActive(true);
                    RefreshManagedLevelList();
                }
            }));
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

            if (customDownloadPopup != null && customDownloadPopup.gameObject.activeSelf)
            {
                customDownloadPopup.gameObject.SetActive(false);
                closedAny = true;
            }

            if (customManagePopup != null && customManagePopup.gameObject.activeSelf)
            {
                customManagePopup.gameObject.SetActive(false);
                closedAny = true;
            }

            if (customEditPopup != null && customEditPopup.gameObject.activeSelf)
            {
                customEditPopup.gameObject.SetActive(false);
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

        private void BuildTitleOnlineNotice()
        {
            RectTransform panel = CreatePanel("TitleOnlineNotice", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -74f), new Vector2(470f, -18f), new Color(0.03f, 0.035f, 0.045f, 0.92f));
            AddVertical(panel, 8, 8, 8, 8, 0);
            titleOnlineNoticeText = AddText(panel, "", 18, TextAlignmentOptions.Left, Color.white, true);
            panel.gameObject.SetActive(false);
        }

        private void ShowTitleNotice(string message, bool error)
        {
            if (titleOnlineNoticeText == null)
                return;

            titleOnlineNoticeText.text = message;
            titleOnlineNoticeText.color = error ? new Color(1f, 0.35f, 0.35f, 1f) : new Color(0.7f, 0.95f, 1f, 1f);
            titleOnlineNoticeText.transform.parent.gameObject.SetActive(true);

            if (titleNoticeRoutine != null)
                StopCoroutine(titleNoticeRoutine);
            titleNoticeRoutine = StartCoroutine(HideTitleNoticeRoutine());
        }

        private System.Collections.IEnumerator HideTitleNoticeRoutine()
        {
            yield return new WaitForSecondsRealtime(2.4f);
            if (titleOnlineNoticeText != null)
                titleOnlineNoticeText.transform.parent.gameObject.SetActive(false);
            titleNoticeRoutine = null;
        }

        private void ClearChildren(Transform parent)
        {
            if (parent == null)
                return;

            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
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

        private HorizontalLayoutGroup AddHorizontal(RectTransform parent, int left, int right, int top, int bottom, int spacing)
        {
            HorizontalLayoutGroup layout = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(left, right, top, bottom);
            layout.spacing = spacing;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
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

        private TMP_InputField AddInputRow(RectTransform parent, string label, string value, Action<string> onEndEdit)
        {
            RectTransform row = CreateUIObject(label + "Row", parent);
            AddHorizontal(row, 0, 0, 0, 0, 8);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;
            TMP_Text labelText = AddText(row, label, 17, TextAlignmentOptions.Left, new Color(0.86f, 0.9f, 1f, 1f), false);
            labelText.GetComponent<LayoutElement>().preferredWidth = 150f;
            TMP_InputField input = CreateInputField(row, value);
            input.onValueChanged.AddListener(text => onEndEdit?.Invoke(text));
            input.onEndEdit.AddListener(text => onEndEdit?.Invoke(text));
            return input;
        }

        private TMP_InputField CreateInputField(RectTransform parent, string value)
        {
            RectTransform rect = CreatePanel("InputField", parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.06f, 0.07f, 0.095f, 1f));
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 38f;
            layout.flexibleWidth = 1f;
            TMP_InputField input = rect.gameObject.AddComponent<TMP_InputField>();
            TMP_Text text = AddText(rect, value, 17, TextAlignmentOptions.Left, Color.white, false);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(8f, 0f);
            text.rectTransform.offsetMax = new Vector2(-8f, 0f);
            input.textComponent = text;
            input.text = value;
            return input;
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
