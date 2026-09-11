function ConvertFrom-DataverseResponseContent {
    [CmdletBinding()]
    param([Parameter(Mandatory, ValueFromPipeline)]$Response)

    process {
        if ([string]::IsNullOrWhiteSpace($Response.Content)) {
            return $Response
        }
        $content = $Response.Content | ConvertFrom-Json
        if ($content.PSObject.Properties.Name -contains 'value') {
            return $content.value
        }
        $content
    }
}

function Get-DataverseRequestConnectionParameters {
    [CmdletBinding()]
    param($Connection, [string]$ConnectionName)

    if ($null -ne $Connection -and ![string]::IsNullOrWhiteSpace($ConnectionName)) {
        throw 'Specify either Connection or ConnectionName, not both.'
    }
    $parameters = @{}
    if ($null -ne $Connection) { $parameters.Connection = $Connection }
    if (![string]::IsNullOrWhiteSpace($ConnectionName)) { $parameters.ConnectionName = $ConnectionName }
    $parameters
}

function Test-DataverseConnection {
    [CmdletBinding()]
    param(
        [PSDataverse.DataverseConnection]$Connection,
        [string]$ConnectionName,
        [switch]$Detailed
    )

    $started = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $connectionParameters = Get-DataverseRequestConnectionParameters $Connection $ConnectionName
        $response = Invoke-DataverseRequest -Uri WhoAmI @connectionParameters -ErrorAction Stop
        $identity = $response | ConvertFrom-DataverseResponseContent
        if (!$Detailed) { return $true }
        [pscustomobject]@{
            IsConnected = $true
            UserId = $identity.UserId
            BusinessUnitId = $identity.BusinessUnitId
            OrganizationId = $identity.OrganizationId
            Duration = $started.Elapsed
            Error = $null
        }
    }
    catch {
        if (!$Detailed) { return $false }
        [pscustomobject]@{
            IsConnected = $false
            UserId = $null
            BusinessUnitId = $null
            OrganizationId = $null
            Duration = $started.Elapsed
            Error = $_.Exception.Message
        }
    }
}

function Get-DataverseRow {
    [CmdletBinding(DefaultParameterSetName = 'List')]
    param(
        [Parameter(Mandatory, Position = 0)][ValidatePattern('^[A-Za-z_][A-Za-z0-9_]*$')][string]$TableSetName,
        [Parameter(Mandatory, Position = 1, ParameterSetName = 'ById')][guid]$Id,
        [Parameter(ParameterSetName = 'List')][string[]]$Select,
        [Parameter(ParameterSetName = 'List')][string]$Filter,
        [Parameter(ParameterSetName = 'List')][ValidateRange(1, 5000)][int]$Top,
        [Parameter(ParameterSetName = 'List')][switch]$All,
        [PSDataverse.DataverseConnection]$Connection,
        [string]$ConnectionName
    )

    $uri = if ($PSCmdlet.ParameterSetName -eq 'ById') { "$TableSetName($Id)" } else { $TableSetName }
    if ($PSCmdlet.ParameterSetName -eq 'List') {
        $query = @()
        if ($Select) { $query += '$select=' + ($Select -join ',') }
        if ($Filter) { $query += '$filter=' + [uri]::EscapeDataString($Filter) }
        if ($Top) { $query += '$top=' + $Top }
        if ($query.Count) { $uri += '?' + ($query -join '&') }
    }
    $connectionParameters = Get-DataverseRequestConnectionParameters $Connection $ConnectionName
    Invoke-DataverseRequest -Uri $uri -AutoPaginate:$All @connectionParameters |
        ConvertFrom-DataverseResponseContent
}

function New-DataverseRow {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory, Position = 0)][ValidatePattern('^[A-Za-z_][A-Za-z0-9_]*$')][string]$TableSetName,
        [Parameter(Mandatory, Position = 1, ValueFromPipeline)]$Body,
        [PSDataverse.DataverseConnection]$Connection,
        [string]$ConnectionName
    )
    process {
        if (!$PSCmdlet.ShouldProcess($TableSetName, 'Create Dataverse row')) { return }
        $connectionParameters = Get-DataverseRequestConnectionParameters $Connection $ConnectionName
        Invoke-DataverseRequest -Uri $TableSetName -Method POST -Body $Body `
            -Headers @{ Prefer = 'return=representation' } @connectionParameters |
            ConvertFrom-DataverseResponseContent
    }
}

function Set-DataverseRow {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory, Position = 0)][ValidatePattern('^[A-Za-z_][A-Za-z0-9_]*$')][string]$TableSetName,
        [Parameter(Mandatory, Position = 1)][guid]$Id,
        [Parameter(Mandatory, Position = 2, ValueFromPipeline)]$Body,
        [string]$IfMatch = '*',
        [switch]$PassThru,
        [PSDataverse.DataverseConnection]$Connection,
        [string]$ConnectionName
    )
    process {
        $uri = "$TableSetName($Id)"
        if (!$PSCmdlet.ShouldProcess($uri, 'Update Dataverse row')) { return }
        $headers = @{ 'If-Match' = $IfMatch }
        if ($PassThru) { $headers.Prefer = 'return=representation' }
        $connectionParameters = Get-DataverseRequestConnectionParameters $Connection $ConnectionName
        $response = Invoke-DataverseRequest -Uri $uri -Method PATCH -Body $Body `
            -Headers $headers @connectionParameters
        if ($PassThru) { $response | ConvertFrom-DataverseResponseContent }
    }
}

function Remove-DataverseRow {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param(
        [Parameter(Mandatory, Position = 0)][ValidatePattern('^[A-Za-z_][A-Za-z0-9_]*$')][string]$TableSetName,
        [Parameter(Mandatory, Position = 1, ValueFromPipeline, ValueFromPipelineByPropertyName)][guid]$Id,
        [string]$IfMatch = '*',
        [PSDataverse.DataverseConnection]$Connection,
        [string]$ConnectionName
    )
    process {
        $uri = "$TableSetName($Id)"
        if (!$PSCmdlet.ShouldProcess($uri, 'Delete Dataverse row')) { return }
        $connectionParameters = Get-DataverseRequestConnectionParameters $Connection $ConnectionName
        Invoke-DataverseRequest -Uri $uri -Method DELETE -Headers @{ 'If-Match' = $IfMatch } @connectionParameters
    }
}

function Get-DataverseTableMetadata {
    [CmdletBinding(DefaultParameterSetName = 'LogicalName')]
    param(
        [Parameter(Mandatory, Position = 0, ValueFromPipeline, ParameterSetName = 'LogicalName')]
        [ValidatePattern('^[A-Za-z_][A-Za-z0-9_]*$')][string]$LogicalName,
        [Parameter(Mandatory, Position = 0, ParameterSetName = 'TableSetName')]
        [ValidatePattern('^[A-Za-z_][A-Za-z0-9_]*$')][string]$TableSetName,
        [switch]$IncludeColumns,
        [PSDataverse.DataverseConnection]$Connection,
        [string]$ConnectionName
    )
    process {
        $connectionParameters = Get-DataverseRequestConnectionParameters $Connection $ConnectionName
        if ($PSCmdlet.ParameterSetName -eq 'TableSetName') {
            $escapedSetName = $TableSetName.Replace("'", "''")
            $lookupUri = "EntityDefinitions?`$select=LogicalName,EntitySetName,PrimaryIdAttribute,PrimaryNameAttribute&`$filter=EntitySetName%20eq%20'$escapedSetName'"
            $matches = @(Invoke-DataverseRequest -Uri $lookupUri @connectionParameters |
                ConvertFrom-DataverseResponseContent)
            if ($matches.Count -ne 1) {
                throw "Expected one table with entity set name '$TableSetName', but found $($matches.Count)."
            }
            if (!$IncludeColumns) { return $matches[0] }
            $LogicalName = $matches[0].LogicalName
        }
        $escapedName = $LogicalName.Replace("'", "''")
        $uri = "EntityDefinitions(LogicalName='$escapedName')"
        if ($IncludeColumns) { $uri += '/Attributes' }
        Invoke-DataverseRequest -Uri $uri @connectionParameters | ConvertFrom-DataverseResponseContent
    }
}

function New-DataverseTable {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory, Position = 0)][ValidatePattern('^[A-Za-z][A-Za-z0-9_]*$')][string]$SchemaName,
        [Parameter(Mandatory, Position = 1)][ValidateNotNullOrEmpty()][string]$DisplayName,
        [Parameter(Mandatory, Position = 2)][ValidateNotNullOrEmpty()][string]$DisplayCollectionName,
        [ValidatePattern('^[A-Za-z][A-Za-z0-9_]*$')][string]$PrimaryNameSchemaName,
        [ValidateRange(1, 4000)][int]$PrimaryNameMaxLength = 200,
        [ValidateSet('UserOwned', 'OrganizationOwned')][string]$OwnershipType = 'UserOwned',
        [ValidateRange(0, 2147483647)][int]$LanguageCode = 1033,
        [string]$SolutionUniqueName,
        [PSDataverse.DataverseConnection]$Connection,
        [string]$ConnectionName
    )

    if (!$PrimaryNameSchemaName) { $PrimaryNameSchemaName = "${SchemaName}Name" }
    if (!$PSCmdlet.ShouldProcess($SchemaName, 'Create Dataverse table')) { return }

    $label = {
        param([string]$Text)
        @{
            '@odata.type' = 'Microsoft.Dynamics.CRM.Label'
            LocalizedLabels = @(@{
                '@odata.type' = 'Microsoft.Dynamics.CRM.LocalizedLabel'
                Label = $Text
                LanguageCode = $LanguageCode
            })
        }
    }
    $body = @{
        '@odata.type' = 'Microsoft.Dynamics.CRM.EntityMetadata'
        SchemaName = $SchemaName
        DisplayName = & $label $DisplayName
        DisplayCollectionName = & $label $DisplayCollectionName
        OwnershipType = $OwnershipType
        IsActivity = $false
        HasActivities = $false
        HasNotes = $false
        Attributes = @(@{
            '@odata.type' = 'Microsoft.Dynamics.CRM.StringAttributeMetadata'
            SchemaName = $PrimaryNameSchemaName
            IsPrimaryName = $true
            RequiredLevel = @{ Value = 'None'; CanBeChanged = $true; ManagedPropertyLogicalName = 'canmodifyrequirementlevelsettings' }
            MaxLength = $PrimaryNameMaxLength
            FormatName = @{ Value = 'Text' }
            DisplayName = & $label $DisplayName
        })
    }
    $headers = @{}
    if ($SolutionUniqueName) { $headers['MSCRM.SolutionUniqueName'] = $SolutionUniqueName }
    $connectionParameters = Get-DataverseRequestConnectionParameters $Connection $ConnectionName
    Invoke-DataverseRequest -Uri EntityDefinitions -Method POST -Body $body -Headers $headers @connectionParameters
}

function Remove-DataverseTable {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param(
        [Parameter(Mandatory, Position = 0, ValueFromPipeline)][ValidatePattern('^[A-Za-z_][A-Za-z0-9_]*$')][string]$LogicalName,
        [PSDataverse.DataverseConnection]$Connection,
        [string]$ConnectionName
    )
    process {
        $escapedName = $LogicalName.Replace("'", "''")
        $uri = "EntityDefinitions(LogicalName='$escapedName')"
        if (!$PSCmdlet.ShouldProcess($LogicalName, 'Delete Dataverse table and all of its data')) { return }
        $connectionParameters = Get-DataverseRequestConnectionParameters $Connection $ConnectionName
        Invoke-DataverseRequest -Uri $uri -Method DELETE @connectionParameters
    }
}

function ConvertTo-DataverseMultipleTarget {
    [CmdletBinding()]
    param([Parameter(Mandatory, ValueFromPipeline)]$Row, [Parameter(Mandatory)][string]$TableLogicalName)
    process {
        $target = [ordered]@{ '@odata.type' = "Microsoft.Dynamics.CRM.$TableLogicalName" }
        if ($Row -is [System.Collections.IDictionary]) {
            foreach ($entry in $Row.GetEnumerator()) { $target[$entry.Key] = $entry.Value }
        } else {
            foreach ($property in $Row.PSObject.Properties) { $target[$property.Name] = $property.Value }
        }
        $target
    }
}

function Invoke-DataverseMultipleOperation {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateSet('CreateMultiple', 'UpdateMultiple', 'UpsertMultiple')][string]$ActionName,
        [Parameter(Mandatory)][string]$TableSetName,
        [Parameter(Mandatory)][string]$TableLogicalName,
        [Parameter(Mandatory)][object[]]$Rows,
        [Parameter(Mandatory)][int]$ChunkSize,
        [Parameter(Mandatory)][int]$MaxDop,
        [PSDataverse.DataverseConnection]$Connection,
        [string]$ConnectionName
    )
    $connectionParameters = Get-DataverseRequestConnectionParameters $Connection $ConnectionName
    if (!$TableLogicalName) {
        $metadata = Get-DataverseTableMetadata -TableSetName $TableSetName @connectionParameters
        $TableLogicalName = $metadata.LogicalName
    }
    $chunkCount = [math]::Ceiling($Rows.Count / [double]$ChunkSize)
    $successfulChunkNumbers = [Collections.Generic.List[int]]::new()
    $failedContentIds = [Collections.Generic.List[string]]::new()
    $chunkByContentId = @{}
    $chunkNumber = 0
    $operations = for ($offset = 0; $offset -lt $Rows.Count; $offset += $ChunkSize) {
        $last = [math]::Min($offset + $ChunkSize - 1, $Rows.Count - 1)
        $chunkNumber++
        $contentId = "${ActionName}_$chunkNumber"
        $inputRows = @($Rows[$offset..$last])
        $targets = @($Rows[$offset..$last] |
            ConvertTo-DataverseMultipleTarget -TableLogicalName $TableLogicalName)
        $failureContext = [PSDataverse.MultipleOperationFailureContext]@{
            ActionName = $ActionName
            TableSetName = $TableSetName
            TableLogicalName = $TableLogicalName
            ChunkNumber = $chunkNumber
            ChunkCount = $chunkCount
            StartIndex = $offset
            EndIndex = $last
            StartRow = $offset + 1
            EndRow = $last + 1
            ContentId = $contentId
            InputRows = $inputRows
            SuccessfulChunkNumbers = $successfulChunkNumbers
            FailedContentIds = $failedContentIds
        }
        $chunkByContentId[$contentId] = $failureContext
        @{
            ContentId = $contentId
            Method = 'POST'
            Uri = "$TableSetName/Microsoft.Dynamics.CRM.$ActionName"
            Value = @{ Targets = $targets }
            FailureContext = $failureContext
        }
    }

    $requestErrors = @()
    $operations | Invoke-DataverseRequest -MaxDop $MaxDop @connectionParameters `
        -ErrorAction SilentlyContinue -ErrorVariable requestErrors |
        ForEach-Object {
            $context = $chunkByContentId[$_.ContentId]
            if ($null -ne $context -and !$successfulChunkNumbers.Contains($context.ChunkNumber)) {
                $successfulChunkNumbers.Add($context.ChunkNumber)
            }
            $_
        } |
        ConvertFrom-DataverseResponseContent
    foreach ($requestError in $requestErrors) {
        $PSCmdlet.WriteError($requestError)
    }
}

function Invoke-DataverseCreateMultiple {
    [CmdletBinding(SupportsShouldProcess, DefaultParameterSetName = 'ResolveLogicalName')]
    param(
        [Parameter(Mandatory, Position = 0)][ValidatePattern('^[A-Za-z_][A-Za-z0-9_]*$')][string]$TableSetName,
        [Parameter(Mandatory, Position = 1, ParameterSetName = 'ExplicitLogicalName')]
        [ValidatePattern('^[A-Za-z_][A-Za-z0-9_]*$')][string]$TableLogicalName,
        [Parameter(Mandatory, Position = 1, ParameterSetName = 'ResolveLogicalName')]
        [Parameter(Mandatory, Position = 2, ParameterSetName = 'ExplicitLogicalName')]
        [ValidateNotNullOrEmpty()][object[]]$Rows,
        [ValidateRange(1, 1000)][int]$ChunkSize = 100,
        [ValidateRange(0, 1024)][int]$MaxDop = 0,
        [PSDataverse.DataverseConnection]$Connection,
        [string]$ConnectionName
    )
    if (!$PSCmdlet.ShouldProcess($TableSetName, "Create $($Rows.Count) Dataverse rows with CreateMultiple")) { return }
    Invoke-DataverseMultipleOperation CreateMultiple $TableSetName $TableLogicalName $Rows `
        $ChunkSize $MaxDop $Connection $ConnectionName
}

function Invoke-DataverseUpdateMultiple {
    [CmdletBinding(SupportsShouldProcess, DefaultParameterSetName = 'ResolveLogicalName')]
    param(
        [Parameter(Mandatory, Position = 0)][ValidatePattern('^[A-Za-z_][A-Za-z0-9_]*$')][string]$TableSetName,
        [Parameter(Mandatory, Position = 1, ParameterSetName = 'ExplicitLogicalName')]
        [ValidatePattern('^[A-Za-z_][A-Za-z0-9_]*$')][string]$TableLogicalName,
        [Parameter(Mandatory, Position = 1, ParameterSetName = 'ResolveLogicalName')]
        [Parameter(Mandatory, Position = 2, ParameterSetName = 'ExplicitLogicalName')]
        [ValidateNotNullOrEmpty()][object[]]$Rows,
        [ValidateRange(1, 1000)][int]$ChunkSize = 100,
        [ValidateRange(0, 1024)][int]$MaxDop = 0,
        [PSDataverse.DataverseConnection]$Connection,
        [string]$ConnectionName
    )
    if (!$PSCmdlet.ShouldProcess($TableSetName, "Update $($Rows.Count) Dataverse rows with UpdateMultiple")) { return }
    Invoke-DataverseMultipleOperation UpdateMultiple $TableSetName $TableLogicalName $Rows `
        $ChunkSize $MaxDop $Connection $ConnectionName
}

function Invoke-DataverseUpsertMultiple {
    [CmdletBinding(SupportsShouldProcess, DefaultParameterSetName = 'ResolveLogicalName')]
    param(
        [Parameter(Mandatory, Position = 0)][ValidatePattern('^[A-Za-z_][A-Za-z0-9_]*$')][string]$TableSetName,
        [Parameter(Mandatory, Position = 1, ParameterSetName = 'ExplicitLogicalName')]
        [ValidatePattern('^[A-Za-z_][A-Za-z0-9_]*$')][string]$TableLogicalName,
        [Parameter(Mandatory, Position = 1, ParameterSetName = 'ResolveLogicalName')]
        [Parameter(Mandatory, Position = 2, ParameterSetName = 'ExplicitLogicalName')]
        [ValidateNotNullOrEmpty()][object[]]$Rows,
        [ValidateRange(1, 1000)][int]$ChunkSize = 100,
        [ValidateRange(0, 1024)][int]$MaxDop = 0,
        [PSDataverse.DataverseConnection]$Connection,
        [string]$ConnectionName
    )
    if (!$PSCmdlet.ShouldProcess($TableSetName, "Upsert $($Rows.Count) Dataverse rows with UpsertMultiple")) { return }
    Invoke-DataverseMultipleOperation UpsertMultiple $TableSetName $TableLogicalName $Rows `
        $ChunkSize $MaxDop $Connection $ConnectionName
}

function Invoke-DataverseAction {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory, Position = 0)][ValidatePattern('^[A-Za-z_][A-Za-z0-9_.]*$')][string]$Name,
        [Parameter(Position = 1)]$Parameters,
        [string]$BoundUri,
        [PSDataverse.DataverseConnection]$Connection,
        [string]$ConnectionName
    )
    $uri = if ($BoundUri) { "$($BoundUri.TrimEnd('/'))/$Name" } else { $Name }
    if (!$PSCmdlet.ShouldProcess($uri, 'Invoke Dataverse action')) { return }
    $connectionParameters = Get-DataverseRequestConnectionParameters $Connection $ConnectionName
    $request = @{ Uri = $uri; Method = 'POST'; Body = if ($null -eq $Parameters) { @{} } else { $Parameters } } + $connectionParameters
    Invoke-DataverseRequest @request | ConvertFrom-DataverseResponseContent
}

function Invoke-DataverseFunction {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)][ValidatePattern('^[A-Za-z_][A-Za-z0-9_.]*$')][string]$Name,
        [hashtable]$Parameters,
        [string]$BoundUri,
        [PSDataverse.DataverseConnection]$Connection,
        [string]$ConnectionName
    )
    $functionCall = "$Name()"
    if ($Parameters.Count) {
        $aliases = @()
        $values = @()
        $index = 0
        foreach ($entry in $Parameters.GetEnumerator()) {
            if ($entry.Key -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') { throw "Invalid function parameter name '$($entry.Key)'." }
            $alias = "p$index"
            $aliases += "$($entry.Key)=@$alias"
            $json = $entry.Value | ConvertTo-Json -Compress -Depth 20
            $values += "@$alias=$([uri]::EscapeDataString($json))"
            $index++
        }
        $functionCall = $Name + '(' + ($aliases -join ',') + ')?' + ($values -join '&')
    }
    $uri = if ($BoundUri) { "$($BoundUri.TrimEnd('/'))/$functionCall" } else { $functionCall }
    $connectionParameters = Get-DataverseRequestConnectionParameters $Connection $ConnectionName
    Invoke-DataverseRequest -Uri $uri @connectionParameters | ConvertFrom-DataverseResponseContent
}

function Export-DataverseRows {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory, Position = 0)][ValidatePattern('^[A-Za-z_][A-Za-z0-9_]*$')][string]$TableSetName,
        [Parameter(Mandatory, Position = 1)][string]$Path,
        [string[]]$Select,
        [string]$Filter,
        [ValidateSet('Csv', 'Json')][string]$Format = 'Csv',
        [ValidateRange(2, 100)][int]$JsonDepth = 20,
        [PSDataverse.DataverseConnection]$Connection,
        [string]$ConnectionName
    )
    if (!$PSCmdlet.ShouldProcess($Path, "Export rows from $TableSetName")) { return }
    $request = @{ TableSetName = $TableSetName; All = $true; Connection = $Connection; ConnectionName = $ConnectionName }
    if ($Select) { $request.Select = $Select }
    if ($Filter) { $request.Filter = $Filter }
    if ($Format -eq 'Csv') {
        Get-DataverseRow @request | Export-Csv -LiteralPath $Path -NoTypeInformation -Encoding utf8
        return
    }
    @(Get-DataverseRow @request) | ConvertTo-Json -Depth $JsonDepth | Set-Content -LiteralPath $Path -Encoding utf8
}

function Import-DataverseRows {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory, Position = 0)][ValidatePattern('^[A-Za-z_][A-Za-z0-9_]*$')][string]$TableSetName,
        [Parameter(Mandatory, Position = 1)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$Path,
        [ValidateSet('Csv', 'Json')][string]$Format,
        [ValidateSet('Individual', 'Batch', 'Bulk')][string]$Mode = 'Batch',
        [ValidateRange(1, 1000)][int]$BatchSize = 10,
        [ValidateRange(1, 1000)][int]$ChunkSize = 100,
        [ValidateRange(0, 1024)][int]$MaxDop = 0,
        [ValidatePattern('^[A-Za-z_][A-Za-z0-9_]*$')][string]$TableLogicalName,
        [PSDataverse.DataverseConnection]$Connection,
        [string]$ConnectionName
    )
    if (!$PSCmdlet.ShouldProcess($TableSetName, "Import Dataverse rows from $Path")) { return }
    if (!$Format) { $Format = if ([IO.Path]::GetExtension($Path) -eq '.json') { 'Json' } else { 'Csv' } }
    $rows = if ($Format -eq 'Json') {
        @(Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
    } else {
        @(Import-Csv -LiteralPath $Path)
    }
    $connectionParameters = Get-DataverseRequestConnectionParameters $Connection $ConnectionName
    if ($Mode -eq 'Bulk') {
        if (!$TableLogicalName) {
            $metadata = Get-DataverseTableMetadata -TableSetName $TableSetName @connectionParameters
            $TableLogicalName = $metadata.LogicalName
        }
        Invoke-DataverseCreateMultiple $TableSetName $TableLogicalName $rows `
            -ChunkSize $ChunkSize -MaxDop $MaxDop @connectionParameters -Confirm:$false
        return
    }
    $contentId = 0
    $operations = $rows | ForEach-Object {
        $contentId++
        @{ ContentId = $contentId.ToString(); Method = 'POST'; Uri = $TableSetName; Value = $_ }
    }
    if ($Mode -eq 'Individual') {
        $operations | Invoke-DataverseRequest -MaxDop $MaxDop @connectionParameters
    } else {
        $operations | Invoke-DataverseRequest -BatchSize $BatchSize -MaxDop $MaxDop @connectionParameters
    }
}
