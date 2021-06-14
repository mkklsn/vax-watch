namespace vaccine_watcher
{
    public static class Constants
    {
        public static string COSMOS_DB_CONNECTION_STRING = "CosmosDBConnection";
        public static string COSMOS_DB_DATABASE_NAME = "VaxWatchDb";
        public static string COSMOS_DB_CONTAINER_NAME = "VaxWatchContainer";
        public static string COSMOS_DB_PARTITION_KEY = $"/{nameof(User.CountryCode)}";
    }
}