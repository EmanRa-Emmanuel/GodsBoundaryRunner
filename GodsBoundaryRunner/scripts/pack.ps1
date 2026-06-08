param(
    [string]$runtime = "win-x64"
)

$proj = "GodsBoundaryRunner.csproj"
$out = "publish/$runtime"

Write-Host "Publishing $proj for $runtime..."

dotnet publish $proj -c Release -r $runtime --self-contained true -o $out /p:PublishSingleFile=true
Write-Host "Published to $out"
