# D-170: builds the user-facing settings tool. It is framework-dependent on purpose (the .NET 10 Desktop runtime is
# already required by nothing else here, but a self-contained single file would add ~150 MB of duplicated runtime to a
# 400 KB tool); -SelfContained produces that single file when a machine without the runtime needs to run it.
param([switch]$SelfContained, [string]$Output = '')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$destination=if($Output){$Output}else{Join-Path $root 'dist\tools'}
[IO.Directory]::CreateDirectory($destination)|Out-Null
$arguments=@('publish',(Join-Path $PSScriptRoot 'VhvrSettings.csproj'),'-c','Release','-o',$destination,'-v','m','--nologo')
if($SelfContained){$arguments+=@('-r','win-x64','--self-contained','true','-p:PublishSingleFile=true')}
& dotnet @arguments
if($LASTEXITCODE -ne 0){throw 'VhvrSettings publish failed'}
if($SelfContained){
    Get-ChildItem $destination -File | Where-Object {$_.Name -notin @('VhvrSettings.exe','README-KO.md')} | Remove-Item -Force
}
$exe=Join-Path $destination 'VhvrSettings.exe'
if(-not (Test-Path -LiteralPath $exe)){throw 'VhvrSettings.exe missing after publish'}
# A published exe must prove itself before it is handed to anyone: the round trip runs on a scratch config, never on a
# real install, and the receipt states the hash so a later run can be attributed to this build.
$scratch=Join-Path $root ('artifacts\settings-selftest-'+[Guid]::NewGuid().ToString('N'))
& $exe --selftest $scratch | Out-Null
$exit=$LASTEXITCODE
$hash=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
$bytes=(Get-Item -LiteralPath $exe).Length
$receipt=@{verdict=if($exit -eq 0){'PASS'}else{'FAIL'};selftestExit=$exit;exe=$exe;sha256=$hash;bytes=$bytes;
    selfContained=$SelfContained.IsPresent;builtUtc=(Get-Date).ToUniversalTime().ToString('o')}
[IO.File]::WriteAllText((Join-Path $root 'artifacts\settings-build.json'),($receipt|ConvertTo-Json))
Remove-Item $scratch -Recurse -Force -ErrorAction SilentlyContinue
if($exit -ne 0){throw ('VhvrSettings self-test failed (exit '+$exit+')')}
Write-Output ('VhvrSettings.exe '+$hash.Substring(0,8)+' '+$bytes+' bytes -> '+$exe)