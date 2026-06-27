using System;
using System.Reflection;
using UnityEngine;

namespace Audio
{
    public class FmodRuntimeAudio : MonoBehaviour
    {
        private object currentBgmInstance;

        public void PlayBgm(string eventPath)
        {
            StopBgm();
            if (string.IsNullOrWhiteSpace(eventPath))
                return;

            try
            {
                Type runtimeManager = Type.GetType("FMODUnity.RuntimeManager, FMODUnity");
                if (runtimeManager == null)
                {
                    Debug.Log($"[FmodRuntimeAudio] FMOD 없음. BGM 요청만 기록: {eventPath}");
                    return;
                }

                MethodInfo createInstance = runtimeManager.GetMethod("CreateInstance", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                currentBgmInstance = createInstance?.Invoke(null, new object[] { eventPath });
                currentBgmInstance?.GetType().GetMethod("start")?.Invoke(currentBgmInstance, null);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[FmodRuntimeAudio] BGM 재생 실패: {exception.Message}");
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
        }

        private void OnDestroy()
        {
            StopBgm();
        }
    }
}
