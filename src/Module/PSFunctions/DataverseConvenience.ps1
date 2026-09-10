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
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0, ValueFromPipeline)][string]$LogicalName,
        [switch]$IncludeColumns,
        [PSDataverse.DataverseConnection]$Connection,
        [string]$ConnectionName
    )
    process {
        $escapedName = $LogicalName.Replace("'", "''")
        $uri = "EntityDefinitions(LogicalName='$escapedName')"
        if ($IncludeColumns) { $uri += '/Attributes' }
        $connectionParameters = Get-DataverseRequestConnectionParameters $Connection $ConnectionName
        Invoke-DataverseRequest -Uri $uri @connectionParameters | ConvertFrom-DataverseResponseContent
    }
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
        [ValidateRange(1, 1000)][int]$BatchSize = 10,
        [ValidateRange(0, 1024)][int]$MaxDop = 0,
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
    $contentId = 0
    $rows | ForEach-Object {
        $contentId++
        @{ ContentId = $contentId.ToString(); Method = 'POST'; Uri = $TableSetName; Value = $_ }
    } | Invoke-DataverseRequest -BatchSize $BatchSize -MaxDop $MaxDop @connectionParameters
}
