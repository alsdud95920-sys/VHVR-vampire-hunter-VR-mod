param([switch]$SelfTest)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vs=(& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath)
if(-not $vs) {throw 'installed MSVC x64 toolchain required'}
$envbat=Join-Path $vs 'VC\Auxiliary\Build\vcvars64.bat'
$sdk=Join-Path $root 'artifacts\openxr\extracted'
$out=Join-Path $PSScriptRoot 'bin'
[IO.Directory]::CreateDirectory($out)|Out-Null
function Invoke-NativeBuild([string]$Name,[string[]]$Sources,[switch]$Dll,[switch]$OpenXr) {
    $flags=if($Dll){'/LD '}else{''}
    $inputs=($Sources|ForEach-Object {'"'+$_+'"'}) -join ' '
    $loader=if($OpenXr){'"'+(Join-Path $sdk 'x64\lib\openxr_loader.lib')+'" '}else{''}
    # vcvars64 invokes vswhere by name; scope its PATH prerequisite to the compiler shell.
    # D-145: advapi32.lib is required for RegGetValueA (the active-OpenXR-runtime lookup in Host.cpp).
    # D-147b: /Brepro makes the PE header timestamp deterministic so byte-for-byte identical sources produce identical DLLs.
    $cmd='set "PATH='+(Split-Path $vswhere)+';%PATH%" && call "'+$envbat+'" >nul && cd /d "'+$out+'" && cl /nologo /std:c++17 /EHsc /W4 /WX /wd4324 /wd4201 /wd4100 '+$flags+'/O2 /I "'+$PSScriptRoot+'" /I "'+(Join-Path $sdk 'include')+'" '+$inputs+' /link /Brepro /OUT:'+ $Name+' '+$loader+'d3d11.lib dxgi.lib advapi32.lib'
    & cmd.exe /d /s /c $cmd
    if($LASTEXITCODE -ne 0){throw ($Name+' build failed')}
}
Invoke-NativeBuild 'VhvrNativeHost.dll' @((Join-Path $PSScriptRoot 'Host.cpp'),(Join-Path $PSScriptRoot 'Gpu.cpp'),(Join-Path $PSScriptRoot 'Hands.cpp')) -Dll -OpenXr
if($SelfTest) {
    function Invoke-NativeFixture([string]$Name,[string]$Receipt) {
        $child=Start-Process -FilePath (Join-Path $out $Name) -ArgumentList ('"'+$Receipt+'"') -PassThru -NoNewWindow
        $handle=$child.Handle
        if(-not $child.WaitForExit(30000)) {
            Stop-Process -Id $child.Id -Force
            throw ($Name+' INCOMPLETE: exact-process fixture budget exceeded (30 seconds)')
        }
        $child.Refresh()
        if($child.ExitCode -ne 0){throw ($Name+' failed: exit '+$child.ExitCode)}
    }
    $test=Join-Path $root 'artifacts\openxr\host\native-selftest.json'
    [IO.Directory]::CreateDirectory((Split-Path $test))|Out-Null
    Invoke-NativeBuild 'NativeHost.Gpu.exe' @((Join-Path $PSScriptRoot 'Gpu.cpp'),(Join-Path $root 'tests\NativeHost.Gpu.cpp'))
    Invoke-NativeFixture 'NativeHost.Gpu.exe' $test
    Write-Output ([IO.File]::ReadAllText($test))
    # The executable substitutes XR/GPU symbols for actual Host.cpp; no fixture code enters the DLL.
    $faults=Join-Path $root 'artifacts\openxr\host\native-faults.json'
    Invoke-NativeBuild 'NativeHost.Faults.exe' @((Join-Path $PSScriptRoot 'Host.cpp'),(Join-Path $root 'tests\NativeHost.Faults.cpp'))
    Invoke-NativeFixture 'NativeHost.Faults.exe' $faults
}