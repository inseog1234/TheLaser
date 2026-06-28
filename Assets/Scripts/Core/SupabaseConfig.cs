namespace Core
{
    public static class SupabaseConfig
    {
        public const string SupabaseUrl = "https://tsvodtpoudczpxfsbktm.supabase.co";
        public const string AnonPublicKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InRzdm9kdHBvdWRjenB4ZnNia3RtIiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODI2NTY4NDUsImV4cCI6MjA5ODIzMjg0NX0.LDHdfP4hmPe7fFFeLtQ6QroVeM0WjTQl15qtdmRUqfA";
        public const string CustomLevelTableName = "custom_level_posts";
        public const int ListLimit = 100;

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
