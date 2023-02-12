function Get-DvTableRowCount {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true)]
        [String]$TableName,
        [Parameter(Mandatory=$false)]
        [string]$Filter = ""
    )
    Write-Progress -Activity "Counting rows"
    $collectionName = Send-DataverseOperation '{"Uri":"EntityDefinitions(LogicalName=''be_filing'')?$select=LogicalCollectionName"}' | Select-Object -ExpandProperty Content | ConvertFrom-Json | Select-Object -ExpandProperty LogicalCollectionName
    $resp = Send-DataverseOperation "{""Uri"":""$($collectionName)?`$select=be_filingname&`$count=true""}" | Select-Object -ExpandProperty Content | ConvertFrom-Json
    $next = $resp | Select-Object -ExpandProperty "@odata.nextLink"
    $count = $resp | Select-Object -ExpandProperty "@odata.count"
    while ($next -ne "") {
        Write-Progress -Activity "Counting rows" -Status "Rows counted: $count"
        $resp = Send-DataverseOperation "{""Uri"":""$next""}" | Select-Object -ExpandProperty Content | ConvertFrom-Json
        if (Get-Member "@odata.nextLink" -InputObject $resp) {
            $next = $resp | Select-Object -ExpandProperty "@odata.nextLink"
            $count += $resp | Select-Object -ExpandProperty "@odata.count"
        } else {
            $next = ""
            $count += ($resp | Select-Object -ExpandProperty value).Count
        }
    }
    Write-Progress -Activity "Counting rows" -Completed
    return $count
}
