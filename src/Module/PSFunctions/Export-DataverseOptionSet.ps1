function Export-DataverseOptionSet {
    [CmdletBinding(SupportsShouldProcess = $True)]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true, HelpMessage = "Enter one or more OptionSet names separated by commas.")]
        [string[]]
        $OptionSet
    )
	#TODO: Support switch argument (-NameValueOnly)
	#TODO: Support switch argument (-LanguageCode) with default value 1033
	process {
		foreach ($name in $OptionSet) {
            if(!$PSCmdlet.ShouldProcess($name)) { continue }
			
			Send-DataverseOperation "GlobalOptionSetDefinitions(Name='$OptionSet')" `
				| Select-Object -ExpandProperty Content `
				| ConvertFrom-Json `
				| Select-Object -ExpandProperty Options
				| Select-Object -ExpandProperty Label -Property Value
				| Select-Object -ExpandProperty LocalizedLabels -Property Value
				| Where-Object { $_.LanguageCode -eq 1033 }
				| Select-Object Value, Label
		}
	}
}