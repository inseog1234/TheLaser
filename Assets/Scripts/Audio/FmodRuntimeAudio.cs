using System;
using System.Reflection;
using UnityEngine;

namespace Audio
{
    public class FmodRuntimeAudio : MonoBehaviour
    {
        public static FmodRuntimeAudio Instance { get; private set; }

        [Header("Option")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        public const string BgmTitle = "event:/BGM/TitleBGM";
        public const string BgmChapter01 = "event:/BGM/Chapter01";
        public const string BgmChapter02 = "event:/BGM/Chapter02";
        public const string BgmChapter03 = "event:/BGM/Chapter03";
        public const string BgmChapter04 = "event:/BGM/Chapter04";
        public const string BgmChapter05 = "event:/BGM/Chapter05";
        public const string BgmChapter06 = "event:/BGM/Chapter06";
        public const string BgmChapter07 = "event:/BGM/Chapter07";
        public const string BgmChapter08 = "event:/BGM/Chapter08";
        public const string BgmChapter09 = "event:/BGM/Chapter09";
        public const string BgmChapter10 = "event:/BGM/Chapter10";
        public const string BgmEditor = "event:/BGM/EditorBGM";

        public const string SfxUiClick = "event:/SFX/UIClick";
        public const string SfxUiOpen = "event:/SFX/UIOpen";
        public const string SfxUiClose = "event:/SFX/UIClose";
        public const string SfxUiCancel = "event:/SFX/UICancel";
        public const string SfxUiConfirmation = "event:/SFX/UIConfirmation";
        public const string SfxEditorObjPlaced = "event:/SFX/EditorObjPlaced";
        public const string SfxEditorObjRemove = "event:/SFX/EditorObjRemove";
        public const string SfxEditorSelect1 = "event:/SFX/EditorSelect1";
        public const string SfxEditorSelect2 = "event:/SFX/EditorSelect2";
        public const string SfxEditorCheckBox = "event:/SFX/EditorCheckBox";
        public const string SfxEditorTrigger = "event:/SFX/EditorTrigger";
        public const string SfxStageClear = "event:/SFX/StageClear";
        public const string SfxFunc = "event:/SFX/Func";
        public const string SfxObjPushAndRotate = "event:/SFX/ObjPushAndRotate";

        private const string MasterVolumeKey = "TheLaser_MasterVolume";
        private const string BgmVolumeKey = "TheLaser_BgmVolume";
        private const string SfxVolumeKey = "TheLaser_SfxVolume";

        private object currentBgmInstance;
        private string currentBgmEventPath;

        public string CurrentBgmEventPath => currentBgmEventPath;

        public static FmodRuntimeAudio EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            FmodRuntimeAudio existing = FindFirstObjectByType<FmodRuntimeAudio>();
            if (existing != null)
                return existing;

            GameObject audioObject = new GameObject("FmodRuntimeAudio");
            return audioObject.AddComponent<FmodRuntimeAudio>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }

        public void PlayBgm(string eventPath)
        {
            eventPath = NormalizeEventPath(eventPath);

            if (currentBgmEventPath == eventPath && currentBgmInstance != null)
            {
                ApplySavedVolumes();
                return;
            }

            StopBgm();
            if (string.IsNullOrWhiteSpace(eventPath))
                return;

            try
            {
                Type runtimeManager = GetRuntimeManagerType();
                if (runtimeManager == null)
                {
                    Debug.Log($"[FmodRuntimeAudio] FMOD 없음. BGM 요청만 기록: {eventPath}");
                    return;
                }

                MethodInfo createInstance = runtimeManager.GetMethod("CreateInstance", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                currentBgmInstance = createInstance?.Invoke(null, new object[] { eventPath });
                currentBgmEventPath = eventPath;
                currentBgmInstance?.GetType().GetMethod("start")?.Invoke(currentBgmInstance, null);
                ApplySavedVolumes();
            }
            catch (Exception exception)
            {
                string message = exception.InnerException != null ? exception.InnerException.Message : exception.Message;
                Debug.LogWarning($"[FmodRuntimeAudio] BGM 재생 실패 ({eventPath}): {message}");
            }
        }

        public void PlaySfx(string eventPath)
        {
            eventPath = NormalizeEventPath(eventPath);
            if (string.IsNullOrWhiteSpace(eventPath))
                return;

            try
            {
                Type runtimeManager = GetRuntimeManagerType();
                if (runtimeManager == null)
                {
                    Debug.Log($"[FmodRuntimeAudio] FMOD 없음. SFX 요청만 기록: {eventPath}");
                    return;
                }

                MethodInfo playOneShot = runtimeManager.GetMethod("PlayOneShot", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                if (playOneShot != null)
                {
                    playOneShot.Invoke(null, new object[] { eventPath });
                    return;
                }

                MethodInfo createInstance = runtimeManager.GetMethod("CreateInstance", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                object instance = createInstance?.Invoke(null, new object[] { eventPath });
                instance?.GetType().GetMethod("start")?.Invoke(instance, null);
                instance?.GetType().GetMethod("release")?.Invoke(instance, null);
            }
            catch (Exception exception)
            {
                string message = exception.InnerException != null ? exception.InnerException.Message : exception.Message;
                Debug.LogWarning($"[FmodRuntimeAudio] SFX 재생 실패 ({eventPath}): {message}");
            }
        }

        public void StopBgm()
        {
            if (currentBgmInstance == null)
                return;

            try
            {
                Type stopModeType = Type.GetType("FMOD.Studio.STOP_MODE, FMODUnity") ?? Type.GetType("FMOD.Studio.STOP_MODE, FMOD");
                object immediate = stopModeType != null ? Enum.Parse(stopModeType, "IMMEDIATE") : null;
                MethodInfo stopMethod = currentBgmInstance.GetType().GetMethod("stop");
                if (stopMethod != null && immediate != null)
                    stopMethod.Invoke(currentBgmInstance, new[] { immediate });
                currentBgmInstance.GetType().GetMethod("release")?.Invoke(currentBgmInstance, null);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[FmodRuntimeAudio] BGM 정지 실패: {exception.Message}");
            }

            currentBgmInstance = null;
            currentBgmEventPath = string.Empty;
        }

        public void ApplySavedVolumes()
        {
            float master = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
            float bgm = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, 1f));
            float sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));

            SetBusVolume("bus:/", master);
            SetBusVolume("bus:/BGM", bgm);
            SetBusVolume("bus:/SFX", sfx);
        }

        public void SetMasterVolume(float value)
        {
            PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            ApplySavedVolumes();
        }

        public void SetBgmVolume(float value)
        {
            PlayerPrefs.SetFloat(BgmVolumeKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            ApplySavedVolumes();
        }

        public void SetSfxVolume(float value)
        {
            PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            ApplySavedVolumes();
        }

        private void SetBusVolume(string busPath, float volume)
        {
            try
            {
                Type runtimeManager = GetRuntimeManagerType();
                if (runtimeManager == null)
                    return;

                MethodInfo getBus = runtimeManager.GetMethod("GetBus", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                object bus = getBus?.Invoke(null, new object[] { busPath });
                bus?.GetType().GetMethod("setVolume")?.Invoke(bus, new object[] { Mathf.Clamp01(volume) });
            }
            catch (Exception exception)
            {
                string message = exception.InnerException != null ? exception.InnerException.Message : exception.Message;
                Debug.LogWarning($"[FmodRuntimeAudio] Bus 볼륨 적용 실패 ({busPath}): {message}");
            }
        }

        private Type GetRuntimeManagerType()
        {
            return Type.GetType("FMODUnity.RuntimeManager, FMODUnity");
        }

        public static string NormalizeEventPath(string eventPath)
        {
            if (string.IsNullOrWhiteSpace(eventPath))
                return string.Empty;

            string value = eventPath.Trim().Replace("\\", "/");
            if (value.StartsWith("event:/", StringComparison.OrdinalIgnoreCase))
                return value;

            string fileName = value;
            int slashIndex = fileName.LastIndexOf('/');
            if (slashIndex >= 0)
                fileName = fileName.Substring(slashIndex + 1);

            int dotIndex = fileName.LastIndexOf('.');
            if (dotIndex > 0)
                fileName = fileName.Substring(0, dotIndex);

            if (fileName.StartsWith("Chapter", StringComparison.OrdinalIgnoreCase))
                return $"event:/BGM/{fileName}";

            if (fileName.Equals("TitleBGM", StringComparison.OrdinalIgnoreCase))
                return BgmTitle;

            if (fileName.Equals("EditorBGM", StringComparison.OrdinalIgnoreCase))
                return BgmEditor;

            return $"event:/SFX/{fileName}";
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                StopBgm();
                Instance = null;
            }
        }
    }
}
