namespace PSDataverse;

public enum DataverseAuthenticationKind
{
    Interactive,
    Wam,
    IntegratedWindows,
    DeviceCode,
    ClientSecret,
    Certificate,
    AccessToken,
    TokenProvider,
    OnPremises
}
