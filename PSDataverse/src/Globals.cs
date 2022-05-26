namespace DataverseModule
{
    public static class Globals
    {
        public const int TooManyRequests = 429;
        public const string VariableNameAccessToken = "Dataverse-AuthToken";
        public const string VariableNameAccessTokenExpiresOn = "Dataverse-AuthTokenExpiresOn";
        public const string VariableNameOperationProcessor = "Dataverse-OperationProcessor";
        public const string VariableNameBatchProcessor = "Dataverse-BatchProcessor";
        public const string VariableNameServiceProvider = "Dataverse-ServiceProvider";
        public const string VariableNameConnectionString = "Dataverse-ConnectionString";
        public const string PolicyNameHttp = "httpPolicy";
        public const string ErrorIdBatchFailure = "DVERR-1001";
        public const string ErrorIdNotConnected = "DVERR-101";
        public const string ErrorIdConnectionExpired = "DVERR-102";
        public const string ErrorIdMissingOperation = "DVERR-103";
        public const string ErrorIdOperationException = "DVERR-110";
    }
}
