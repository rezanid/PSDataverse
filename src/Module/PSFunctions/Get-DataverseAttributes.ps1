<#
.SYNOPSIS
    Returns all the attribute metadata of a given entity in Dataverse.

.DESCRIPTION
    Get-DataverseAttributes is a function that returns all attribute metadata of a given 
    entity. The attributes can be filtered by type. If attributes are not filtered by type
    only the common properties will be retrieved from Dataverse. Filtering attributes by 
    type will cause the function to include more metadata that is specific to a given type.

.PARAMETER EntityLogicalName
    The logical name of the entity in Dataverse.

.PARAMETER AttributeType
    The type of attributes to retrieve from Dataverse. By default, no filtering is applied.
    Filtering attributes by type will cause the function to include more metadata that are
    specific to the given type.

.EXAMPLE
     Get-DataverseAttributes -EntityLogicalName 'account', 'contact'

.EXAMPLE
     'account', 'contact' | Get-DataverseAttributes

.EXAMPLE
     Get-DataverseAttributes -EntityLogicalName 'account' -AttributeType Decimal

.EXAMPLE
     Get-DataverseAttributes -EntityLogicalName 'account' -AttributeType Picklist -Expand OptionSet -Filter "IsValidForRead eq true"

.INPUTS
    String

.OUTPUTS
    PSCustomObject

.NOTES
    Author:  Reza Niroomand
    Website: www.donebycode.com
    X: @rezaniroomand
#>
function Get-DataverseAttributes {

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
