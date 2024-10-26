dotnet restore

$packagePath = Join-Path $PSScriptRoot "package"
$downloadPath = Join-Path $packagePath "PublicSuffix\data\public_suffix_list.dat"
$solutionPath = Join-Path $packagePath "PublicSuffix.sln"

Invoke-WebRequest "https://publicsuffix.org/list/public_suffix_list.dat" -OutFile $downloadPath

dotnet build $solutionPath --configuration Release --no-restore --no-incremental
dotnet test  $solutionPath --configuration Release --no-build
dotnet pack  $solutionPath --configuration Release --no-build
