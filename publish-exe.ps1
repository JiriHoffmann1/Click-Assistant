param(
    [string]$Runtime = "win-x64"
)

dotnet publish src/ClickAssistant.App/ClickAssistant.App.csproj `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o publish

Write-Host "Hotovo: publish\ClickAssistant.App.exe"
