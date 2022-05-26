using Microsoft.Extensions.DependencyInjection;
using System;
using System.Management.Automation;
using System.Security.Cryptography.X509Certificates;

namespace DataverseModule
{
    [Cmdlet(VerbsCommunications.Connect, "Dataverse")]
    public class ConnectDataverseCmdlet : PSCmdlet
    {
        [Parameter(Position=0, Mandatory = true, ParameterSetName = "String")]
        public string ConnectionString { get; set; }

        [Parameter(Position=1, Mandatory = false)]
        public int Retry { get; set; }

        [Parameter(Position=0, Mandatory = true, ParameterSetName = "Object")]
        public DataverseConnectionString ConnectionStringObject { get; set; }

        private readonly object _lock = new ();

        protected override void ProcessRecord()
        {
            //TODO: Implement exception handling and logging.
            //TODO: Implement retry logic
            var dataverseCnnStr = ConnectionStringObject ?? DataverseConnectionString.Parse(ConnectionString);
            var certificate = AuthenticationHelper.FindCertificate(dataverseCnnStr.CertificationThumbprint, StoreName.My);
            
            var authResult = AuthenticationHelper.Authenticate(dataverseCnnStr.Authority, dataverseCnnStr.ClientId, dataverseCnnStr.Resource, certificate);
            SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameAccessToken, authResult.AccessToken, ScopedItemOptions.AllScope));
            SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameAccessTokenExpiresOn, authResult.ExpiresOn, ScopedItemOptions.AllScope));
            SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameConnectionString, dataverseCnnStr, ScopedItemOptions.AllScope));

            var serviceProvider = (IServiceProvider)GetVariableValue(Globals.VariableNameServiceProvider);
            if (serviceProvider == null)
            {
                InitializeServiceProvider(new Uri(dataverseCnnStr.Resource, UriKind.Absolute));
            }

            //var processor = new OperationProcessor(NullLogger.Instance, new HttpClientFactory(new Uri(dataverseCnnStr.Resource, UriKind.Absolute), "v9.2"), startup.SetupRetryPolicies(), authResult.AccessToken);
            //SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameOperationProcessor, processor, ScopedItemOptions.Private));

            WriteDebug("AccessToken: " + authResult.AccessToken);
            WriteInformation("Dataverse authenticated successfully.", new string[] { "dataverse" });
            WriteObject("Dataverse authenticated successfully.");
            base.ProcessRecord();
        }

        private void InitializeServiceProvider(Uri baseUrl)
        {
            lock (_lock)
            {
                var serviceProvider = (IServiceProvider)GetVariableValue(Globals.VariableNameServiceProvider);
                if (serviceProvider == null)
                {
                    var startup = new Startup(baseUrl);
                    serviceProvider = startup.ConfigureServices(new ServiceCollection()).BuildServiceProvider();
                    SessionState.PSVariable.Set(
                        new PSVariable(Globals.VariableNameServiceProvider, serviceProvider, ScopedItemOptions.AllScope));
                }
            }
        }
    }
}