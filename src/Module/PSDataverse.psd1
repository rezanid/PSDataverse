#
# Module manifest for module 'PSDataverse;'
#
# Generated by: Reza Niroomand
#
# Generated on: 26/04/2022
#

@{

# Script module or binary module file associated with this manifest.
RootModule = 'PSDataverse.psm1'

# Version number of this module.
ModuleVersion = '0.0.3'

# Supported PSEditions
CompatiblePSEditions = @("Core")

# ID used to uniquely identify this module
GUID = '081185a0-92be-4624-85e8-4903acb07e03'

# Author of this module
Author = 'Reza Niroomand'

# Company or vendor of this module
CompanyName = 'Novovio'

# Copyright statement for this module
Copyright = 'Copyright (c) Novovio.'

# Description of the functionality provided by this module
Description = 'Bring Dataverse''s Web API to PowerShell.'

# Minimum version of the PowerShell engine required by this module
PowerShellVersion = '5.1'

# Minimum version of Microsoft .NET Framework required by this module. This prerequisite is valid for the PowerShell Desktop edition only.
DotNetFrameworkVersion = '6.0'

# Minimum version of the common language runtime (CLR) required by this module. This prerequisite is valid for the PowerShell Desktop edition only.
ClrVersion = '6.0'

# Modules that must be imported into the global environment prior to importing this module
# RequiredModules = @()

# Assemblies that must be loaded prior to importing this module
# RequiredAssemblies = @()

# Script files (.ps1) that are run in the caller's environment prior to importing this module.
# ScriptsToProcess = @()

# Type files (.ps1xml) to be loaded when importing this module
# TypesToProcess = @()

# Format files (.ps1xml) to be loaded when importing this module
# FormatsToProcess = @()

# Modules to import as nested modules of the module specified in RootModule/ModuleToProcess
NestedModules = @('./bin/PSDataverse.dll')

# Functions to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no functions to export.
FunctionsToExport = @('Clear-DataverseTable')

# Cmdlets to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no cmdlets to export.
CmdletsToExport = @('Connect-Dataverse', 'Send-DataverseOperation')

# Variables to export from this module
VariablesToExport = @()

# Aliases to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no aliases to export.
AliasesToExport = @()

# Private data to pass to the module specified in RootModule/ModuleToProcess. This may also contain a PSData hashtable with additional module metadata used by PowerShell.
PrivateData = @{

    PSData = @{

        # Tags applied to this module. These help with module discovery in online galleries.
        Tags = @('PSEdition_Core', 'PSEdition_Desktop', 'Windows', 'Linux', 'macOS', 'Dataverse')

        # A URL to the license for this module.
        LicenseUri = 'https://github.com/rezanid/PSDataverse/blob/main/LICENSE'

        # A URL to the main website for this project.
        ProjectUri = 'https://github.com/rezanid/PSDataverse'

        # A URL to an icon representing this module.
        IconUri = 'https://github.com/rezanid/PSDataverse/raw/268e36c93ddfbcb6bcc2255beed9b03499210dfb/media/PSDataverse-Logo.png'

        # ReleaseNotes of this module
        # ReleaseNotes = ''

        # Prerelease string of this module
        # Prerelease = ''

        # Flag to indicate whether the module requires explicit user acceptance for install/update/save
        RequireLicenseAcceptance = $false

        # External dependent modules of this module
        # ExternalModuleDependencies = @()

        IsPrerelease = 'True'
    } # End of PSData hashtable

} # End of PrivateData hashtable

# HelpInfo URI of this module
# HelpInfoURI = ''

# Default prefix for commands exported from this module. Override the default prefix using Import-Module -Prefix.
# DefaultCommandPrefix = ''

}
