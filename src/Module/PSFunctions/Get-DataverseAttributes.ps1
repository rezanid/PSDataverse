function Get-DataverseAttributes {
    <#
.EXTERNALHELP PSDataverse.PowerShell-Help.xml
#>

  [CmdletBinding(SupportsShouldProcess)]
  param(
    [Parameter(Mandatory, ValueFromPipeline, ValueFromPipelineByPropertyName)]
    [ValidateNotNullOrEmpty()]
    [string[]]$EntityLogicalName,

    [ValidateNotNullOrEmpty()]
    [string]$AttributeType,

    [ValidateNotNullOrEmpty()]
    [string]$Select,

    [ValidateNotNullOrEmpty()]
    [string]$Filter,

    [ValidateNotNullOrEmpty()]
    [string]$Expand
  )

  process {
    foreach($entity in $EntityLogicalName) {
      if ($PSBoundParameters.ContainsKey('AttributeType')) {
        $query = "EntityDefinitions(LogicalName='$entity')/Attributes/Microsoft.Dynamics.CRM.$($AttributeType)AttributeMetadata?LabelLanguages=1033"
      } else {
        $query = "EntityDefinitions(LogicalName='$entity')/Attributes?LabelLanguages=1033"
      }
      if ($PSBoundParameters.ContainsKey('Select')) { $query += "&`$select=$Select" }
      if ($PSBoundParameters.ContainsKey('Filter')) { $query += "&`$filter=$Filter" }
      if ($PSBoundParameters.ContainsKey('Expand')) { $query += "&`$expand=$Expand" }
      if ($PSCmdlet.ShouldProcess($query, "Send-DataverseOperation")) {
        Send-DataverseOperation $query -AutoPaginate | Select-Object -ExpandProperty value
      }
    }
  }
}
