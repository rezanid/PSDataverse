namespace PSDataverse;

public enum DataverseAuthenticationKind
{
    Interactive,
    IntegratedWindows,
    DeviceCode,
    ClientSecret,
    Certificate,
    AccessToken,
    TokenProvider,
    OnPremises
}
