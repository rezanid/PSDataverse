namespace PSDataverse;
public static class Globals
{
    public const string VariableNameAuthResult = "Dataverse-AuthResult";
    public const string VariableNameAccessToken = "Dataverse-AuthToken";
    public const string VariableNameAccessTokenExpiresOn = "Dataverse-AuthTokenExpiresOn";
    public const string VariableNameOperationProcessor = "Dataverse-OperationProcessor";
    public const string VariableNameBatchProcessor = "Dataverse-BatchProcessor";
    public const string VariableNameServiceProvider = "Dataverse-ServiceProvider";
    public const string VariableNameConnectionString = "Dataverse-ConnectionString";
    public const string PolicyNameHttp = "httpPolicy";
    public const string ErrorIdAuthenticationFailed = "DVERR-901";
    public const string ErrorIdBatchFailure = "DVERR-1001";
    public const string ErrorIdNotConnected = "DVERR-1001";
    public const string ErrorIdConnectionExpired = "DVERR-1002";
    public const string ErrorIdMissingOperation = "DVERR-1003";
    public const string ErrorIdOperationException = "DVERR-1010";
}
