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
        private const string ClientIdPlayerPrefsKey = "TheLaser_CustomLevel_ClientId";
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

        public string ClientId => GetOrCreateClientId();

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
            if (!TryValidateUpload(stageFilePath, stageData, out byte[] bytes, out string error))
            {
                onComplete?.Invoke(false, error);
                yield break;
            }

            string body = BuildUploadJson(stageFilePath, stageData, bytes, nickname, postTitle, postDescription);
            string url = BuildRestUrl(SupabaseConfig.CustomLevelTableName);

            using UnityWebRequest request = CreateJsonRequest(url, UnityWebRequest.kHttpVerbPOST, body);
            request.SetRequestHeader("Prefer", "return=minimal");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onComplete?.Invoke(false, BuildNetworkError("게시 실패", request));
                yield break;
            }

            onComplete?.Invoke(true, "게시 완료!");
        }

        public IEnumerator UpdateCustomLevelPost(CustomLevelPostData post, string postTitle, string postDescription, string stageFilePath, StageData stageData, Action<bool, string> onComplete)
        {
            if (!SupabaseConfig.IsConfigured)
            {
                onComplete?.Invoke(false, "Supabase URL 또는 anon key가 아직 임시값입니다.");
                yield break;
            }

            if (post == null || string.IsNullOrWhiteSpace(post.id))
            {
                onComplete?.Invoke(false, "수정 실패: 선택된 게시물이 없습니다.");
                yield break;
            }

            if (!IsMine(post))
            {
                onComplete?.Invoke(false, "수정 실패: 이 기기에서 올린 게시물만 수정할 수 있습니다.");
                yield break;
            }

            byte[] bytes = null;
            if (!string.IsNullOrWhiteSpace(stageFilePath))
            {
                if (!TryValidateUpload(stageFilePath, stageData, out bytes, out string error))
                {
                    onComplete?.Invoke(false, error.Replace("업로드", "수정"));
                    yield break;
                }
            }

            string body = BuildUpdateJson(stageFilePath, stageData, bytes, postTitle, postDescription);
            string url = BuildRestUrl(SupabaseConfig.CustomLevelTableName) + "?id=eq." + EscapeUrl(post.id) + "&uploader_client_id=eq." + EscapeUrl(ClientId);

            using UnityWebRequest request = CreateJsonRequest(url, "PATCH", body);
            request.SetRequestHeader("Prefer", "return=minimal");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onComplete?.Invoke(false, BuildNetworkError("수정 실패", request));
                yield break;
            }

            onComplete?.Invoke(true, "수정 완료!");
        }

        public IEnumerator DeleteCustomLevelPost(CustomLevelPostData post, Action<bool, string> onComplete)
        {
            if (!SupabaseConfig.IsConfigured)
            {
                onComplete?.Invoke(false, "Supabase URL 또는 anon key가 아직 임시값입니다.");
                yield break;
            }

            if (post == null || string.IsNullOrWhiteSpace(post.id))
            {
                onComplete?.Invoke(false, "삭제 실패: 선택된 게시물이 없습니다.");
                yield break;
            }

            if (!IsMine(post))
            {
                onComplete?.Invoke(false, "삭제 실패: 이 기기에서 올린 게시물만 삭제할 수 있습니다.");
                yield break;
            }

            string url = BuildRestUrl(SupabaseConfig.CustomLevelTableName) + "?id=eq." + EscapeUrl(post.id) + "&uploader_client_id=eq." + EscapeUrl(ClientId);
            using UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbDELETE);
            request.downloadHandler = new DownloadHandlerBuffer();
            ApplySupabaseHeaders(request);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onComplete?.Invoke(false, BuildNetworkError("삭제 실패", request));
                yield break;
            }

            onComplete?.Invoke(true, "삭제 완료!");
        }

        public IEnumerator FetchCustomLevels(Action<bool, string, List<CustomLevelPostData>> onComplete)
        {
            yield return FetchCustomLevelsInternal(false, onComplete);
        }

        public IEnumerator FetchMyCustomLevels(Action<bool, string, List<CustomLevelPostData>> onComplete)
        {
            yield return FetchCustomLevelsInternal(true, onComplete);
        }

        private IEnumerator FetchCustomLevelsInternal(bool onlyMine, Action<bool, string, List<CustomLevelPostData>> onComplete)
        {
            if (!SupabaseConfig.IsConfigured)
            {
                onComplete?.Invoke(false, "Supabase URL 또는 anon key가 아직 임시값입니다.", new List<CustomLevelPostData>());
                yield break;
            }

            string columns = "id,created_at,updated_at,uploader_client_id,nickname,post_title,post_description,tls_file_name,tls_base64,tls_size_bytes,stage_name,width,height,move_limit,solution_action_count,like_count";
            string url = BuildRestUrl(SupabaseConfig.CustomLevelTableName) + "?select=" + columns + "&order=created_at.desc&limit=" + SupabaseConfig.ListLimit;
            if (onlyMine)
                url += "&uploader_client_id=eq." + EscapeUrl(ClientId);

            using UnityWebRequest request = UnityWebRequest.Get(url);
            ApplySupabaseHeaders(request);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onComplete?.Invoke(false, BuildNetworkError("목록 불러오기 실패", request), new List<CustomLevelPostData>());
                yield break;
            }

            List<CustomLevelPostData> posts = ParsePostList(request.downloadHandler.text);
            yield return FillMyLikeStates(posts);
            onComplete?.Invoke(true, "목록 불러오기 완료", posts);
        }

        public IEnumerator ToggleLike(CustomLevelPostData post, Action<bool, string, CustomLevelPostData> onComplete)
        {
            yield return ToggleLikeInternal(post, onComplete);
        }

        private IEnumerator InsertLike(string postId, Action<bool, string> onComplete)
        {
            string body = "{\"post_id\":\"" + EscapeJson(postId) + "\",\"client_id\":\"" + EscapeJson(ClientId) + "\"}";
            string url = BuildRestUrl(SupabaseConfig.CustomLevelLikeTableName);
            using UnityWebRequest request = CreateJsonRequest(url, UnityWebRequest.kHttpVerbPOST, body);
            request.SetRequestHeader("Prefer", "resolution=ignore-duplicates,return=minimal");
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success && request.responseCode != 409)
                onComplete?.Invoke(false, BuildNetworkError("좋아요 실패", request));
            else
                onComplete?.Invoke(true, string.Empty);
        }

        private IEnumerator DeleteLike(string postId, Action<bool, string> onComplete)
        {
            string url = BuildRestUrl(SupabaseConfig.CustomLevelLikeTableName) + "?post_id=eq." + EscapeUrl(postId) + "&client_id=eq." + EscapeUrl(ClientId);
            using UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbDELETE);
            request.downloadHandler = new DownloadHandlerBuffer();
            ApplySupabaseHeaders(request);
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
                onComplete?.Invoke(false, BuildNetworkError("좋아요 취소 실패", request));
            else
                onComplete?.Invoke(true, string.Empty);
        }

        private IEnumerator ToggleLikeInternal(CustomLevelPostData post, Action<bool, string, CustomLevelPostData> onComplete)
        {
            if (!SupabaseConfig.IsConfigured)
            {
                onComplete?.Invoke(false, "Supabase URL 또는 anon key가 아직 임시값입니다.", post);
                yield break;
            }

            if (post == null || string.IsNullOrWhiteSpace(post.id))
            {
                onComplete?.Invoke(false, "좋아요 실패: 선택된 게시물이 없습니다.", post);
                yield break;
            }

            bool nextLiked = !post.liked_by_me;
            bool success = false;
            string message = string.Empty;

            if (nextLiked)
                yield return InsertLike(post.id, (resultSuccess, resultMessage) => { success = resultSuccess; message = resultMessage; });
            else
                yield return DeleteLike(post.id, (resultSuccess, resultMessage) => { success = resultSuccess; message = resultMessage; });

            if (!success)
            {
                onComplete?.Invoke(false, message, post);
                yield break;
            }

            post.liked_by_me = nextLiked;
            post.like_count = Mathf.Max(0, post.like_count + (nextLiked ? 1 : -1));
            yield return PatchLikeCount(post.id, post.like_count, (resultSuccess, resultMessage) => { success = resultSuccess; message = resultMessage; });

            if (!success)
            {
                onComplete?.Invoke(false, message, post);
                yield break;
            }

            onComplete?.Invoke(true, nextLiked ? "좋아요!" : "좋아요 취소", post);
        }

        private IEnumerator PatchLikeCount(string postId, int likeCount, Action<bool, string> onComplete)
        {
            string body = "{\"like_count\":" + Mathf.Max(0, likeCount) + "}";
            string url = BuildRestUrl(SupabaseConfig.CustomLevelTableName) + "?id=eq." + EscapeUrl(postId);
            using UnityWebRequest request = CreateJsonRequest(url, "PATCH", body);
            request.SetRequestHeader("Prefer", "return=minimal");
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
                onComplete?.Invoke(false, BuildNetworkError("좋아요 수 갱신 실패", request));
            else
                onComplete?.Invoke(true, string.Empty);
        }

        private IEnumerator FillMyLikeStates(List<CustomLevelPostData> posts)
        {
            if (posts == null || posts.Count <= 0)
                yield break;

            string ids = BuildPostIdInFilter(posts);
            if (string.IsNullOrWhiteSpace(ids))
                yield break;

            string url = BuildRestUrl(SupabaseConfig.CustomLevelLikeTableName) + "?select=post_id,client_id&client_id=eq." + EscapeUrl(ClientId) + "&post_id=in.(" + ids + ")";
            using UnityWebRequest request = UnityWebRequest.Get(url);
            ApplySupabaseHeaders(request);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                yield break;

            HashSet<string> likedIds = ParseLikedPostIds(request.downloadHandler.text);
            for (int i = 0; i < posts.Count; i++)
                posts[i].liked_by_me = likedIds.Contains(posts[i].id);
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

        public bool IsMine(CustomLevelPostData post)
        {
            return post != null && string.Equals(post.uploader_client_id, ClientId, StringComparison.OrdinalIgnoreCase);
        }

        private bool TryValidateUpload(string stageFilePath, StageData stageData, out byte[] bytes, out string error)
        {
            bytes = null;
            error = string.Empty;

            if (!SupabaseConfig.IsConfigured)
            {
                error = "Supabase URL 또는 anon key가 아직 임시값입니다.";
                return false;
            }

            if (stageData == null)
            {
                error = "업로드 실패: 스테이지 데이터가 없습니다.";
                return false;
            }

            if (!stageData.HasSolution)
            {
                error = "업로드 실패: 답안이 없는 맵입니다.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(stageFilePath) || !File.Exists(stageFilePath))
            {
                error = "업로드 실패: 먼저 .tls로 저장해야 합니다.";
                return false;
            }

            try
            {
                bytes = File.ReadAllBytes(stageFilePath);
                return true;
            }
            catch (Exception e)
            {
                error = "업로드 실패: 파일 읽기 실패 - " + e.Message;
                return false;
            }
        }

        private static string BuildUploadJson(string stageFilePath, StageData stageData, byte[] bytes, string nickname, string postTitle, string postDescription)
        {
            string fileName = Path.GetFileName(stageFilePath);
            string base64 = Convert.ToBase64String(bytes);
            int solutionCount = stageData.solutionActions != null ? stageData.solutionActions.Count : 0;
            StringBuilder builder = new StringBuilder(base64.Length + 1024);
            builder.Append('{');
            AppendJson(builder, "uploader_client_id", Instance.ClientId, true);
            AppendJson(builder, "nickname", TrimToLimit(nickname, 30), true);
            AppendJson(builder, "post_title", TrimToLimit(postTitle, SupabaseConfig.PostTitleCharacterLimit), true);
            AppendJson(builder, "post_description", TrimToLimit(postDescription, SupabaseConfig.PostDescriptionCharacterLimit), true);
            AppendJson(builder, "tls_file_name", fileName, true);
            AppendJson(builder, "tls_base64", base64, true);
            AppendJson(builder, "tls_size_bytes", bytes.Length, true);
            AppendJson(builder, "stage_name", string.IsNullOrWhiteSpace(stageData.stageName) ? fileName : stageData.stageName, true);
            AppendJson(builder, "width", stageData.width, true);
            AppendJson(builder, "height", stageData.height, true);
            AppendJson(builder, "move_limit", stageData.moveLimit, true);
            AppendJson(builder, "solution_action_count", solutionCount, true);
            AppendJson(builder, "like_count", 0, false);
            builder.Append('}');
            return builder.ToString();
        }

        private static string BuildUpdateJson(string stageFilePath, StageData stageData, byte[] bytes, string postTitle, string postDescription)
        {
            StringBuilder builder = new StringBuilder(bytes != null ? bytes.Length + 1024 : 1024);
            builder.Append('{');
            AppendJson(builder, "post_title", TrimToLimit(postTitle, SupabaseConfig.PostTitleCharacterLimit), true);
            AppendJson(builder, "post_description", TrimToLimit(postDescription, SupabaseConfig.PostDescriptionCharacterLimit), bytes != null);

            if (bytes != null)
            {
                string fileName = Path.GetFileName(stageFilePath);
                string base64 = Convert.ToBase64String(bytes);
                int solutionCount = stageData.solutionActions != null ? stageData.solutionActions.Count : 0;
                AppendJson(builder, "tls_file_name", fileName, true);
                AppendJson(builder, "tls_base64", base64, true);
                AppendJson(builder, "tls_size_bytes", bytes.Length, true);
                AppendJson(builder, "stage_name", string.IsNullOrWhiteSpace(stageData.stageName) ? fileName : stageData.stageName, true);
                AppendJson(builder, "width", stageData.width, true);
                AppendJson(builder, "height", stageData.height, true);
                AppendJson(builder, "move_limit", stageData.moveLimit, true);
                AppendJson(builder, "solution_action_count", solutionCount, false);
            }

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

        private static string EscapeUrl(string value)
        {
            return UnityWebRequest.EscapeURL(value ?? string.Empty);
        }

        private static UnityWebRequest CreateJsonRequest(string url, string method, string body)
        {
            UnityWebRequest request = new UnityWebRequest(url, method);
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body ?? "{}");
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            ApplySupabaseHeaders(request);
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
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

        private static HashSet<string> ParseLikedPostIds(string rawJson)
        {
            HashSet<string> result = new HashSet<string>();
            if (string.IsNullOrWhiteSpace(rawJson))
                return result;

            string wrapped = "{\"items\":" + rawJson + "}";
            CustomLevelLikeList list = JsonUtility.FromJson<CustomLevelLikeList>(wrapped);
            if (list == null || list.items == null)
                return result;

            for (int i = 0; i < list.items.Length; i++)
            {
                if (list.items[i] != null && !string.IsNullOrWhiteSpace(list.items[i].post_id))
                    result.Add(list.items[i].post_id);
            }

            return result;
        }

        private static string BuildPostIdInFilter(List<CustomLevelPostData> posts)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < posts.Count; i++)
            {
                if (posts[i] == null || string.IsNullOrWhiteSpace(posts[i].id))
                    continue;

                if (builder.Length > 0)
                    builder.Append(',');
                builder.Append(posts[i].id);
            }

            return builder.ToString();
        }

        private static string GetOrCreateClientId()
        {
            string id = PlayerPrefs.GetString(ClientIdPlayerPrefsKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(id))
                return id;

            id = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(ClientIdPlayerPrefsKey, id);
            PlayerPrefs.Save();
            return id;
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

        private static string TrimToLimit(string value, int limit)
        {
            if (string.IsNullOrEmpty(value) || limit <= 0 || value.Length <= limit)
                return value ?? string.Empty;

            return value.Substring(0, limit);
        }
    }
}
