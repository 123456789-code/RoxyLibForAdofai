param([switch]$Restore, [switch]$Test, [switch]$Format, [switch]$VerifyStyle, [string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$buildState = Join-Path $root 'RoxyLib/obj/.build'
$settings = @{
	DOTNET_CLI_HOME = "$buildState/dotnet"
	TEMP = "$buildState/tmp"
	TMP = "$buildState/tmp"
	NUGET_PACKAGES = "$root/.packages"
	NUGET_HTTP_CACHE_PATH = "$buildState/nuget-http"
	NUGET_PLUGINS_CACHE_PATH = "$buildState/nuget-plugins"
	DOTNET_CLI_TELEMETRY_OPTOUT = '1'
	DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
	DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
	DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
	DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
	MSBUILDDISABLENODEREUSE = '1'
	MSBuildEnableWorkloadResolver = 'false'
}
$original = @{}
foreach ($key in $settings.Keys) {
	$original[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
	[Environment]::SetEnvironmentVariable($key, $settings[$key], 'Process')
}
try {
	New-Item -ItemType Directory -Force -Path $settings.TEMP, $settings.DOTNET_CLI_HOME | Out-Null
	Push-Location $root
	try {
		$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
		if ($Restore) {
			& $dotnet restore RoxyLibForAdofai.slnx --locked-mode --configfile NuGet.config --packages .packages -p:NuGetAudit=false -p:NuGetInteractive=false -p:AutomaticallyUseReferenceAssemblyPackages=false
			if ($LASTEXITCODE -ne 0) { throw "Restore failed: $LASTEXITCODE" }
		}
		if ($Format) {
			& $dotnet format style RoxyLibForAdofai.slnx --no-restore --diagnostics IDE0022 IDE0065 IDE0161 IDE0005 IDE0040 IDE0007 IDE0008 --severity info
			if ($LASTEXITCODE -ne 0) { throw "Style formatting failed: $LASTEXITCODE" }
			& $dotnet format whitespace RoxyLibForAdofai.slnx --no-restore
			if ($LASTEXITCODE -ne 0) { throw "Whitespace formatting failed: $LASTEXITCODE" }
		}
		if ($VerifyStyle) {
			& $dotnet format style RoxyLibForAdofai.slnx --no-restore --verify-no-changes --diagnostics IDE1006 IDE0022 IDE0065 IDE0161 IDE0005 IDE0040 IDE0007 IDE0008 --severity info --report "$root/RoxyLib/obj/style-report.json" --verbosity diagnostic *> "$buildState/style-workspace.log"
			if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath "$buildState/style-workspace.log"; throw "Style verification failed: $LASTEXITCODE" }
			& $dotnet format whitespace RoxyLibForAdofai.slnx --no-restore --verify-no-changes
			if ($LASTEXITCODE -ne 0) { throw "Whitespace verification failed: $LASTEXITCODE" }
		}
		& $dotnet build RoxyLibForAdofai.slnx --no-restore -c $Configuration -p:UseSharedCompilation=false -nodeReuse:false
		if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
		if ($Test) {
			[xml]$props = Get-Content -LiteralPath "$root/Directory.Build.props"
			$gameManaged = Join-Path $props.Project.PropertyGroup.GameDir 'A Dance of Fire and Ice_Data/Managed'
			& "$root/RoxyLib.Tests/bin/$Configuration/net481/RoxyLib.Tests.exe" "$root/RoxyLib.Tests/obj/test-data" "$root/RoxyLib/bin/$Configuration/net481" $gameManaged
			if ($LASTEXITCODE -ne 0) { throw "Tests failed: $LASTEXITCODE" }
		}
	} finally { Pop-Location }
} finally {
	foreach ($key in $settings.Keys) {
		[Environment]::SetEnvironmentVariable($key, $original[$key], 'Process')
	}
}
