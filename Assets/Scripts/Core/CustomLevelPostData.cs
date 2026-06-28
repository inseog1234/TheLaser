using System;

namespace Core
{
    [Serializable]
    public class CustomLevelPostData
    {
        public string id;
        public string created_at;
        public string nickname;
        public string post_title;
        public string post_description;
        public string tls_file_name;
        public string tls_base64;
        public int tls_size_bytes;
        public string stage_name;
        public int width;
        public int height;
        public int move_limit;
        public int solution_action_count;

        public string DisplayTitle => string.IsNullOrWhiteSpace(post_title) ? "제목 없음" : post_title;
        public string DisplayNickname => string.IsNullOrWhiteSpace(nickname) ? "익명" : nickname;
        public string DisplayDescription => string.IsNullOrWhiteSpace(post_description) ? "설명 없음" : post_description;
    }

    [Serializable]
    public class CustomLevelPostList
    {
        public CustomLevelPostData[] items;
    }
}
