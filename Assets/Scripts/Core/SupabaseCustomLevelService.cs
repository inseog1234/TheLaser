using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Core
{
    public sealed class SupabaseCustomLevelService : MonoBehaviour
    {
        private static SupabaseCustomLevelService instance;

        public static SupabaseCustomLevelService Instance
        {
            get
            {
                if (instance != null)
                    return instance;

                SupabaseCustomLevelService existing = UnityEngine.Object.FindFirstObjectByType<SupabaseCustomLevelService>();
                if (existing != null)
                {
                    instance = existing;
                    DontDestroyOnLoad(existing.gameObject);
                    return instance;
                }

                GameObject obj = new GameObject("SupabaseCustomLevelService");
                instance = obj.AddComponent<SupabaseCustomLevelService>();
                DontDestroyOnLoad(obj);
                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public IEnumerator UploadCustomLevel(string stageFilePath, StageData stageData, string nickname, string postTitle, string postDescription, Action<bool, string> onComplete)
        {
            if (!SupabaseConfig.IsConfigured)
            {
                onComplete?.Invoke(false, "Supabase URL 또는 anon key가 아직 임시값입니다.");
                yield break;
            }

            if (stageData == null)
            {
                onComplete?.Invoke(false, "업로드 실패: 스테이지 데이터가 없습니다.");
                yield break;
            }

            if (!stageData.HasSolution)
            {
                onComplete?.Invoke(false, "업로드 실패: 답안이 없는 맵입니다.");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(stageFilePath) || !File.Exists(stageFilePath))
            {
                onComplete?.Invoke(false, "업로드 실패: 먼저 .tls로 저장해야 합니다.");
                yield break;
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(stageFilePath);
            }
            catch (Exception e)
            {
                onComplete?.Invoke(false, "업로드 실패: 파일 읽기 실패 - " + e.Message);
                yield break;
            }

            string body = BuildUploadJson(stageFilePath, stageData, bytes, nickname, postTitle, postDescription);
            string url = BuildRestUrl(SupabaseConfig.CustomLevelTableName);

            using UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            ApplySupabaseHeaders(request);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Prefer", "return=minimal");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onComplete?.Invoke(false, BuildNetworkError("게시 실패", request));
                yield break;
            }

            onComplete?.Invoke(true, "게시 완료!");
        }

        public IEnumerator FetchCustomLevels(Action<bool, string, List<CustomLevelPostData>> onComplete)
        {
            if (!SupabaseConfig.IsConfigured)
            {
                onComplete?.Invoke(false, "Supabase URL 또는 anon key가 아직 임시값입니다.", new List<CustomLevelPostData>());
                yield break;
            }

            string columns = "id,created_at,nickname,post_title,post_description,tls_file_name,tls_base64,tls_size_bytes,stage_name,width,height,move_limit,solution_action_count";
            string url = BuildRestUrl(SupabaseConfig.CustomLevelTableName) + "?select=" + columns + "&order=created_at.desc&limit=" + SupabaseConfig.ListLimit;

            using UnityWebRequest request = UnityWebRequest.Get(url);
            ApplySupabaseHeaders(request);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onComplete?.Invoke(false, BuildNetworkError("목록 불러오기 실패", request), new List<CustomLevelPostData>());
                yield break;
            }

            List<CustomLevelPostData> posts = ParsePostList(request.downloadHandler.text);
            onComplete?.Invoke(true, "목록 불러오기 완료", posts);
        }

        public bool SaveDownloadedLevel(CustomLevelPostData post, out string savedPath, out string error)
        {
            savedPath = string.Empty;
            error = string.Empty;

            if (post == null)
            {
                error = "다운로드 실패: 선택된 게시물이 없습니다.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(post.tls_base64))
            {
                error = "다운로드 실패: 게시물에 맵 데이터가 없습니다.";
                return false;
            }

            try
            {
                byte[] bytes = Convert.FromBase64String(post.tls_base64);
                Directory.CreateDirectory(StageFilePaths.MyCustomLevelsDirectory);
                string safeFileName = StageFilePaths.NormalizeStageFileName(SanitizeFileName(string.IsNullOrWhiteSpace(post.tls_file_name) ? post.DisplayTitle : post.tls_file_name));
                savedPath = GetUniquePath(Path.Combine(StageFilePaths.MyCustomLevelsDirectory, safeFileName));
                File.WriteAllBytes(savedPath, bytes);
                return true;
            }
            catch (Exception e)
            {
                error = "다운로드 실패: " + e.Message;
                return false;
            }
        }

        private static string BuildUploadJson(string stageFilePath, StageData stageData, byte[] bytes, string nickname, string postTitle, string postDescription)
        {
            string fileName = Path.GetFileName(stageFilePath);
            string base64 = Convert.ToBase64String(bytes);
            int solutionCount = stageData.solutionActions != null ? stageData.solutionActions.Count : 0;
            StringBuilder builder = new StringBuilder(4096);
            builder.Append('{');
            AppendJson(builder, "nickname", nickname, true);
            AppendJson(builder, "post_title", postTitle, true);
            AppendJson(builder, "post_description", postDescription, true);
            AppendJson(builder, "tls_file_name", fileName, true);
            AppendJson(builder, "tls_base64", base64, true);
            AppendJson(builder, "tls_size_bytes", bytes.Length, true);
            AppendJson(builder, "stage_name", string.IsNullOrWhiteSpace(stageData.stageName) ? fileName : stageData.stageName, true);
            AppendJson(builder, "width", stageData.width, true);
            AppendJson(builder, "height", stageData.height, true);
            AppendJson(builder, "move_limit", stageData.moveLimit, true);
            AppendJson(builder, "solution_action_count", solutionCount, false);
            builder.Append('}');
            return builder.ToString();
        }

        private static void AppendJson(StringBuilder builder, string key, string value, bool comma)
        {
            builder.Append('"').Append(EscapeJson(key)).Append("\":\"").Append(EscapeJson(value ?? string.Empty)).Append('"');
            if (comma)
                builder.Append(',');
        }

        private static void AppendJson(StringBuilder builder, string key, int value, bool comma)
        {
            builder.Append('"').Append(EscapeJson(key)).Append("\":").Append(value);
            if (comma)
                builder.Append(',');
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        private static void ApplySupabaseHeaders(UnityWebRequest request)
        {
            request.SetRequestHeader("apikey", SupabaseConfig.AnonPublicKey);
            request.SetRequestHeader("Authorization", "Bearer " + SupabaseConfig.AnonPublicKey);
        }

        private static string BuildRestUrl(string tableName)
        {
            return SupabaseConfig.SupabaseUrl.TrimEnd('/') + "/rest/v1/" + tableName;
        }

        private static string BuildNetworkError(string prefix, UnityWebRequest request)
        {
            string detail = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            if (string.IsNullOrWhiteSpace(detail))
                detail = request.error;
            return prefix + ": " + request.responseCode + " / " + detail;
        }

        private static List<CustomLevelPostData> ParsePostList(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
                return new List<CustomLevelPostData>();

            string wrapped = "{\"items\":" + rawJson + "}";
            CustomLevelPostList list = JsonUtility.FromJson<CustomLevelPostList>(wrapped);
            if (list == null || list.items == null)
                return new List<CustomLevelPostData>();

            return new List<CustomLevelPostData>(list.items);
        }

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "DownloadedLevel.tls";

            string result = fileName;
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                result = result.Replace(invalid[i], '_');
            return result;
        }

        private static string GetUniquePath(string path)
        {
            if (!File.Exists(path))
                return path;

            string directory = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            for (int i = 2; i < 1000; i++)
            {
                string candidate = Path.Combine(directory, name + "_" + i + extension);
                if (!File.Exists(candidate))
                    return candidate;
            }

            return Path.Combine(directory, name + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + extension);
        }
    }
}
