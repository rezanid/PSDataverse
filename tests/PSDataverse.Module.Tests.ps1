[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path $_ -PathType Leaf })]
    [string]$ModulePath
)

BeforeAll {
    $script:expectedCommands = @(
        'Clear-DataverseTable'
        'Connect-Dataverse'
        'Disconnect-Dataverse'
        'Export-DataverseOptionSet'
        'Export-DataverseRows'
        'Get-DataverseAttributes'
        'Get-DataverseConnection'
        'Get-DataverseRow'
        'Get-DataverseTableMetadata'
        'Get-DataverseTableRowCount'
        'Import-DataverseRows'
        'Invoke-DataverseAction'
        'Invoke-DataverseFunction'
        'Invoke-DataverseRequest'
    'New-DataverseRow'
    'Remove-DataverseRow'
    'Send-DataverseOperation'
    'Set-DataverseDefaultConnection'
    'Set-DataverseRow'
    'Test-DataverseConnection'
    )
    Import-Module $ModulePath -Force
}

Describe 'PSDataverse packaged module contract' {
    It 'has a valid manifest and imports the packaged binary' {
        { Test-ModuleManifest -Path $ModulePath -ErrorAction Stop } | Should -Not -Throw
        Split-Path (Get-Module PSDataverse).Path -Parent |
            Should -Be (Split-Path (Resolve-Path $ModulePath).Path -Parent)
    }

    It 'exports exactly the documented command surface' {
        $actual = @(Get-Command -Module PSDataverse | Sort-Object Name | Select-Object -ExpandProperty Name)
        $actual | Should -Be $expectedCommands
    }

    It 'exposes every supported authentication parameter set' {
        $sets = (Get-Command Connect-Dataverse).ParameterSets
        @($sets.Name | Sort-Object) | Should -Be @(
            'AccessToken'
            'Certificate'
            'ClientSecret'
            'ClientSecretProvider'
            'ConnectionString'
            'DeviceCode'
            'IntegratedWindows'
            'Interactive'
            'TokenProvider'
            'Url'
        )
        ($sets | Where-Object Name -EQ 'Url').Parameters.Name | Should -Contain 'OnPremise'
        ($sets | Where-Object Name -EQ 'ClientSecret').Parameters.Name | Should -Contain 'ClientId'
        ($sets | Where-Object Name -EQ 'Certificate').Parameters.Name | Should -Contain 'CertificateThumbprint'
        ($sets | Where-Object Name -EQ 'AccessToken').Parameters.Name | Should -Contain 'ExpiresOn'
        ($sets | Where-Object Name -EQ 'Interactive').Parameters.Name | Should -Contain 'ForceAuthentication'
        ($sets | Where-Object Name -EQ 'Interactive').Parameters.Name | Should -Contain 'UseWebAccountManager'
        (Get-Command Connect-Dataverse).Parameters.UseWebAccountManager.Aliases | Should -Contain 'UseWam'
        ($sets | Where-Object Name -EQ 'DeviceCode').Parameters.Name | Should -Not -Contain 'ForceAuthentication'
    }

    It 'accepts operations and objects from the pipeline' {
        $command = Get-Command Invoke-DataverseRequest
        $command.ParameterSets.Name | Should -Contain 'Operation'
        $command.ParameterSets.Name | Should -Contain 'Object'
        $command.ParameterSets.Name | Should -Contain 'Request'
        $command.Parameters.InputOperation.Attributes.ValueFromPipeline | Should -Contain $true
        $command.Parameters.InputObject.Attributes.ValueFromPipeline | Should -Contain $true
        $command.Parameters.Uri.Attributes.ValueFromPipeline | Should -Contain $true
    }

    It 'keeps Send-DataverseOperation as an alias for the new low-level command' {
        (Get-Command Send-DataverseOperation).CommandType | Should -Be 'Alias'
        (Get-Command Send-DataverseOperation).ResolvedCommandName | Should -Be 'Invoke-DataverseRequest'
    }

    It 'offers direct request parameters without constructing an operation object' {
        $request = (Get-Command Invoke-DataverseRequest).ParameterSets |
            Where-Object Name -EQ 'Request'
        $request.Parameters.Name | Should -Contain 'Uri'
        $request.Parameters.Name | Should -Contain 'Method'
        $request.Parameters.Name | Should -Contain 'Body'
        $request.Parameters.Name | Should -Contain 'Headers'
        $request.Parameters.Name | Should -Contain 'ContentId'
    }

    It 'offers batch and table output controls with validation metadata' {
        $command = Get-Command Invoke-DataverseRequest
        $command.Parameters.OutputTable.ParameterType | Should -Be ([switch])
        $command.Parameters.BatchSize.Aliases | Should -Contain 'BatchCapacity'
        $command.Parameters.MaxDop.Aliases | Should -Contain 'ThrottleLimit'
    }

    It 'manages named access-token connections without exposing the token' {
        $token = ConvertTo-SecureString 'not-a-real-token' -AsPlainText -Force
        try {
            $primary = Connect-Dataverse https://primary.crm.dynamics.com `
                -AccessToken $token -ExpiresOn (Get-Date).AddHours(1) -Name primary
            $secondary = Connect-Dataverse https://secondary.crm.dynamics.com `
                -AccessToken $token -ExpiresOn (Get-Date).AddHours(1) -Name secondary -NoDefault

            @(Get-DataverseConnection).Name | Should -Be @('primary', 'secondary')
            $primary.IsDefault | Should -BeTrue
            $secondary.IsDefault | Should -BeFalse
            Disconnect-Dataverse -All -WhatIf
            @(Get-DataverseConnection) | Should -HaveCount 2
            (Set-DataverseDefaultConnection secondary -PassThru).Name | Should -Be 'secondary'
            (Get-DataverseConnection secondary).IsDefault | Should -BeTrue
            $primary.PSObject.Properties.Name | Should -Not -Contain 'AccessToken'
        }
        finally {
            Disconnect-Dataverse -All -Confirm:$false -InformationAction Ignore
        }
    }

    It 'supports WhatIf on commands that mutate or export data' -ForEach @(
        'Clear-DataverseTable'
        'Export-DataverseOptionSet'
        'Export-DataverseRows'
        'Get-DataverseAttributes'
        'Import-DataverseRows'
        'Invoke-DataverseAction'
        'New-DataverseRow'
        'Remove-DataverseRow'
        'Set-DataverseRow'
    ) {
        (Get-Command $_).Parameters.Keys | Should -Contain 'WhatIf'
    }

    It 'tests a missing connection without throwing' {
        Disconnect-Dataverse -All -Confirm:$false -InformationAction Ignore
        Test-DataverseConnection | Should -BeFalse
        $detail = Test-DataverseConnection -Detailed
        $detail.IsConnected | Should -BeFalse
        $detail.Error | Should -Not -BeNullOrEmpty
    }

    It 'returns a stable connection error when invoked while disconnected' {
        Disconnect-Dataverse -All -Confirm:$false -InformationAction Ignore
        $errors = @()
        Send-DataverseOperation 'accounts' -ErrorAction SilentlyContinue -ErrorVariable errors

        $errors | Should -HaveCount 1
        $errors[0].FullyQualifiedErrorId | Should -Match '^DVERR-1001'
        $errors[0].CategoryInfo.Category | Should -Be 'ConnectionError'
    }

    It 'distinguishes a missing named connection from no default connection' {
        $errors = @()
        Send-DataverseOperation 'accounts' -ConnectionName missing `
            -ErrorAction SilentlyContinue -ErrorVariable errors

        $errors | Should -HaveCount 1
        $errors[0].FullyQualifiedErrorId | Should -Match '^DVERR-1004'
        $errors[0].CategoryInfo.Category | Should -Be 'ConnectionError'
    }
}
