namespace Core
{
    public static class SupabaseConfig
    {
        public const string SupabaseUrl = "https://YOUR_PROJECT_ID.supabase.co";
        public const string AnonPublicKey = "YOUR_SUPABASE_ANON_PUBLIC_KEY";
        public const string CustomLevelTableName = "custom_level_posts";
        public const string CustomLevelLikeTableName = "custom_level_likes";
        public const int ListLimit = 100;
        public const int PostTitleCharacterLimit = 60;
        public const int PostDescriptionCharacterLimit = 500;

        public static bool IsConfigured
        {
            get
            {
                return !string.IsNullOrWhiteSpace(SupabaseUrl) &&
                    !string.IsNullOrWhiteSpace(AnonPublicKey) &&
                    !SupabaseUrl.Contains("YOUR_PROJECT_ID") &&
                    !AnonPublicKey.Contains("YOUR_SUPABASE_ANON_PUBLIC_KEY");
            }
        }
    }
}
