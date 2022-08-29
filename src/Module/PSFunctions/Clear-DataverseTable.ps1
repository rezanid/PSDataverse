function Clear-DataverseTable {
    [CmdletBinding(SupportsShouldProcess = $True)]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true, HelpMessage = "Enter one or more table names separated by commas.")]
        [string[]]
        $TableName,
        [switch]$NoWait
    )
    
    process {   
        $jobStatusCodes = @{
            0  = "Waiting For Resources"
            10 = "Waiting"
            20 = "In Progress"
            21 = "Pausing"
            22 = "Canceling"
            30 = "Succeeded"
            31 = "Failed"
            33 = "Canceled"
        }
    
        foreach ($name in $TableName) {
            if(!$PSCmdlet.ShouldProcess($name)) { continue }

            $jobId = Send-DataverseOperation @{
                Uri    = "BulkDelete"
                Method = "POST"
                Value  = @{
                    QuerySet              = @(@{EntityName = $name })
                    JobName               = "Bulk delete all rows from $name table"
                    SendEmailNotification = $false
                    ToRecipients          = @(@{
                            activitypartyid = "00000000-0000-0000-0000-000000000000"
                            "@odata.type"   = "Microsoft.Dynamics.CRM.activityparty"
                        })
                    CCRecipients          = @(@{
                            activitypartyid = "00000000-0000-0000-0000-000000000000"
                            "@odata.type"   = "Microsoft.Dynamics.CRM.activityparty"
                        })
                    RecurrencePattern     = ""
                    StartDateTime         = Get-Date
                    RunNow                = $false
                }
            } | Select-Object -ExpandProperty Content | ConvertFrom-Json | Select-Object -ExpandProperty JobId
    
            if ($NoWait) { return "asyncoperations($jobId)" }
    
            do {
                Start-Sleep -Seconds 1
                $result = Send-DataverseOperation "asyncoperations($jobId)?`$select=statuscode,statecode" -InformationAction Ignore | Select-Object -ExpandProperty Content | ConvertFrom-Json
                Write-Progress -Activity "BulkDelete" -Status $jobStatusCodes[[int]$result.statuscode]
            } while ($result.statecode -ne 3)
            Write-Progress -Activity "BulkDelete" -Completed
            
            switch ($result.statuscode) {
                30 { Write-Information "Operation succeeded. For more information check asyncoperations($jobId)" }
                31 { Write-Error "Operation failed. For more information check asyncoperations($jobId)" }
                Default { Write-Warning "Operation canceled. For more information check asyncoperations($jobId)" }
            }
    
            Write-Output "asyncoperations($jobId)"
        }
    }
}