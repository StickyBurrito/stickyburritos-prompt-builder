$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$artifacts = Join-Path $root 'artifacts'
$stage = Join-Path $artifacts 'payload'
$publish = Join-Path $artifacts 'app-publish'
$appData = Join-Path $stage 'App'
$web = Join-Path $stage 'Web'
$setupPayload = Join-Path $root 'TagRoll.Setup\Payload'

if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
if (Test-Path -LiteralPath $setupPayload) { Remove-Item -LiteralPath $setupPayload -Recurse -Force }
New-Item -ItemType Directory -Path $artifacts, $stage, $publish, $appData, $web, $setupPayload -Force | Out-Null

dotnet publish (Join-Path $root 'TagRoll.Web\TagRoll.Web.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publish
Copy-Item (Join-Path $publish 'Stickyburritos-Prompt-Generator.exe') -Destination $stage
Copy-Item (Join-Path $publish 'appsettings.json') -Destination $appData
Copy-Item (Join-Path $root 'index.html'), (Join-Path $root 'app.js'), (Join-Path $root 'theme.css'), (Join-Path $root 'styles.css') -Destination $web
Copy-Item (Join-Path $root 'assets') -Destination $web -Recurse
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath (Join-Path $setupPayload 'TagRollPayload.zip') -CompressionLevel Optimal

$setupOut = Join-Path $artifacts 'installer'
New-Item -ItemType Directory -Path $setupOut -Force | Out-Null
dotnet publish (Join-Path $root 'TagRoll.Setup\TagRoll.Setup.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $setupOut
Write-Host "Installer created: $(Join-Path $setupOut 'Stickyburritos-Prompt-Generator-Setup.exe')"
