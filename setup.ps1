#Requires -Version 5.1

$scriptPath = Join-Path $PSScriptRoot 'shell\setup.ps1'
if (-not (Test-Path $scriptPath)) {
	throw "Cannot find setup script at '$scriptPath'."
}

& $scriptPath @Args
