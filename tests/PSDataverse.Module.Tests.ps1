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
        'Get-DataverseAttributes'
        'Get-DataverseTableRowCount'
        'Send-DataverseOperation'
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

    It 'exposes the URL and connection-string parameter sets' {
        $sets = (Get-Command Connect-Dataverse).ParameterSets
        $sets.Name | Should -Contain 'Url'
        $sets.Name | Should -Contain 'ConnectionString'
        ($sets | Where-Object Name -EQ 'Url').Parameters.Name | Should -Contain 'OnPremise'
    }

    It 'accepts operations and objects from the pipeline' {
        $command = Get-Command Send-DataverseOperation
        $command.ParameterSets.Name | Should -Contain 'Operation'
        $command.ParameterSets.Name | Should -Contain 'Object'
        $command.Parameters.InputOperation.Attributes.ValueFromPipeline | Should -Contain $true
        $command.Parameters.InputObject.Attributes.ValueFromPipeline | Should -Contain $true
    }

    It 'offers batch and table output controls with validation metadata' {
        $command = Get-Command Send-DataverseOperation
        $command.Parameters.OutputTable.ParameterType | Should -Be ([switch])
        $command.Parameters.BatchSize.Aliases | Should -Contain 'BatchCapacity'
        $command.Parameters.MaxDop.Aliases | Should -Contain 'ThrottleLimit'
    }

    It 'supports WhatIf on commands that mutate or export data' -ForEach @(
        'Clear-DataverseTable'
        'Export-DataverseOptionSet'
        'Get-DataverseAttributes'
    ) {
        (Get-Command $_).Parameters.Keys | Should -Contain 'WhatIf'
    }

    It 'returns a stable connection error when invoked while disconnected' {
        Disconnect-Dataverse -InformationAction Ignore
        $errors = @()
        Send-DataverseOperation 'accounts' -ErrorAction SilentlyContinue -ErrorVariable errors

        $errors | Should -HaveCount 1
        $errors[0].FullyQualifiedErrorId | Should -Match '^DVERR-1001'
        $errors[0].CategoryInfo.Category | Should -Be 'ConnectionError'
    }
}
