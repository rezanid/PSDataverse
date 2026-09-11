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
        'Invoke-DataverseCreateMultiple'
        'Invoke-DataverseFunction'
        'Invoke-DataverseRequest'
        'Invoke-DataverseUpdateMultiple'
        'Invoke-DataverseUpsertMultiple'
        'New-DataverseRow'
        'New-DataverseTable'
        'Remove-DataverseRow'
        'Remove-DataverseTable'
        'Send-DataverseOperation'
        'Set-DataverseDefaultConnection'
        'Set-DataverseRow'
        'Test-DataverseBulkOperationSupport'
        'Test-DataverseConnection'
    )
    $script:documentedCommands = @(
        $script:expectedCommands | Where-Object { $_ -ne 'Send-DataverseOperation' }
    )
    Import-Module $ModulePath -Force
}

Describe 'PSDataverse packaged module contract' {
    It 'has a valid manifest and imports the packaged binary' {
        { Test-ModuleManifest -Path $ModulePath -ErrorAction Stop } | Should -Not -Throw
        (Test-ModuleManifest -Path $ModulePath).PowerShellVersion | Should -Be ([version]'7.6')
        Split-Path (Get-Module PSDataverse).Path -Parent |
            Should -Be (Split-Path (Resolve-Path $ModulePath).Path -Parent)
    }

    It 'exports exactly the documented command surface' {
        $actual = @(Get-Command -Module PSDataverse | Sort-Object Name | Select-Object -ExpandProperty Name)
        $actual | Should -Be $expectedCommands
    }

    It 'packages external help for every command' {
        $moduleRoot = Split-Path (Resolve-Path $ModulePath).Path -Parent
        (Join-Path $moduleRoot 'en-US/PSDataverse.dll-Help.xml') | Should -Exist
        (Join-Path $moduleRoot 'PSFunctions/en-US/PSDataverse.PowerShell-Help.xml') |
            Should -Exist

        foreach ($name in $documentedCommands) {
            $help = Get-Help $name -Full
            $help.Name | Should -Be $name
            [string]$help.Synopsis | Should -Not -BeNullOrEmpty
            [string]$help.Description.Text | Should -Not -BeNullOrEmpty
            @($help.Examples.Example).Count | Should -BeGreaterThan 0
            ($help | Out-String) | Should -Not -Match '{{[^}]+}}'
        }
    }

    It 'does not package the help generator as a runtime dependency' {
        $moduleRoot = Split-Path (Resolve-Path $ModulePath).Path -Parent
        @(Get-ChildItem $moduleRoot -Recurse -File |
            Where-Object Name -Match 'PlatyPS').Count | Should -Be 0
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
        'Invoke-DataverseCreateMultiple'
        'Invoke-DataverseUpdateMultiple'
        'Invoke-DataverseUpsertMultiple'
        'New-DataverseRow'
        'New-DataverseTable'
        'Remove-DataverseRow'
        'Remove-DataverseTable'
        'Set-DataverseRow'
    ) {
        (Get-Command $_).Parameters.Keys | Should -Contain 'WhatIf'
    }

    It 'offers explicit import transports and concurrent multiple-operation chunks' {
        $import = Get-Command Import-DataverseRows
        $import.Parameters.Keys | Should -Contain 'Mode'
        $import.Parameters.Keys | Should -Contain 'BatchSize'
        $import.Parameters.Keys | Should -Contain 'ChunkSize'
        $import.Parameters.Keys | Should -Contain 'MaxDop'
        @($import.Parameters.Mode.Attributes.ValidValues) | Should -Be @('Individual', 'Batch', 'Bulk')

        foreach ($name in 'Invoke-DataverseCreateMultiple', 'Invoke-DataverseUpdateMultiple', 'Invoke-DataverseUpsertMultiple') {
            $command = Get-Command $name
            $command.Parameters.Keys | Should -Contain 'ChunkSize'
            $command.Parameters.Keys | Should -Contain 'MaxDop'
        }
    }

    It 'creates structured multiple-operation errors without printing input rows' {
        $inputRows = @(
            [pscustomobject]@{ Name = 'sensitive-row-3' }
            [pscustomobject]@{ Name = 'sensitive-row-4' }
        )
        $context = [PSDataverse.MultipleOperationFailureContext]@{
            ActionName = 'UpdateMultiple'
            TableSetName = 'new_examples'
            TableLogicalName = 'new_example'
            ChunkNumber = 2
            ChunkCount = 3
            StartIndex = 2
            EndIndex = 3
            StartRow = 3
            EndRow = 4
            ContentId = 'UpdateMultiple_2'
            InputRows = $inputRows
            SuccessfulChunkNumbers = [Collections.Generic.List[int]]@(1, 3)
        }

        $message = $context.CreateErrorMessage('Generic SQL error')
        $message | Should -Match 'chunk 2 of 3'
        $message | Should -Match 'input rows 3-4'
        $message | Should -Not -Match 'sensitive-row'
        $context.ContentId | Should -Be 'UpdateMultiple_2'
        $context.InputRows | Should -Be $inputRows
        $context.SuccessfulChunkNumbers | Should -Be @(1, 3)
    }

    It 'exposes bulk capability inspection parameter sets' {
        $command = Get-Command Test-DataverseBulkOperationSupport
        @($command.ParameterSets.Name | Sort-Object) | Should -Be @('LogicalName', 'TableSetName')
        foreach ($set in $command.ParameterSets) {
            $set.Parameters.Name | Should -Contain 'Operation'
            $set.Parameters.Name | Should -Contain 'Detailed'
            $set.Parameters.Name | Should -Contain 'Refresh'
        }
    }

    It 'blocks a definitively unsupported multiple operation before sending rows' {
        InModuleScope PSDataverse {
            Mock Test-DataverseBulkOperationSupport {
                [pscustomobject]@{
                    TableLogicalName = 'unsupported_table'
                    Operation = 'CreateMultiple'
                    Supported = $false
                }
            }
            Mock Invoke-DataverseRequest { throw 'A write request must not be sent.' }

            {
                Invoke-DataverseMultipleOperation CreateMultiple unsupported_tables unsupported_table `
                    @(@{ unsupported_tableid = [guid]::NewGuid() }) 100 1
            } | Should -Throw -ErrorId 'DVERR-1021,Invoke-DataverseMultipleOperation'
            Should -Invoke Invoke-DataverseRequest -Times 0 -Exactly
        }
    }

    It 'preserves existing behavior when capability inspection is unavailable' {
        InModuleScope PSDataverse {
            Mock Test-DataverseBulkOperationSupport { return }
            Mock Invoke-DataverseRequest {
                [pscustomobject]@{ ContentId = 'CreateMultiple_1'; Content = $null }
            }

            $result = Invoke-DataverseMultipleOperation CreateMultiple new_examples new_example `
                @(@{ new_exampleid = [guid]::NewGuid() }) 100 1

            Should -Invoke Invoke-DataverseRequest -Times 1 -Exactly
            $result.ContentId | Should -Be 'CreateMultiple_1'
        }
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
