param([string]$Tag='host1',[int]$TimeoutSeconds=100,[switch]$Motion,[int]$InstallerTimeoutSeconds=600,[switch]$Gameplay)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=Split-Path -Parent $PSScriptRoot
if($Tag -notmatch '^[A-Za-z0-9_-]+$' -or $TimeoutSeconds -lt 30 -or $TimeoutSeconds -gt $(if($Gameplay){300}else{180}) -or
    $InstallerTimeoutSeconds -lt 300 -or $InstallerTimeoutSeconds -gt 900){throw 'invalid tag/timeout'}
if($Gameplay -and $Motion){throw 'native upright motion blocked; do not repeat exhausted input with gameplay'}
$dir=Join-Path $root ('artifacts\openxr\host\'+$Tag)
if(Test-Path $dir){throw 'fresh tag required'}
[IO.Directory]::CreateDirectory($dir)|Out-Null
. (Join-Path $PSScriptRoot 'SimulatorWindow.ps1')
. (Join-Path $PSScriptRoot 'NativeMotion.ps1')
$game=if($env:VHVR_GAME_ROOT){$env:VHVR_GAME_ROOT}else{'C:\Program Files (x86)\Steam\steamapps\common\Vampire Hunters'}
$simulatorJson=if($env:VHVR_XR_SIM_JSON){$env:VHVR_XR_SIM_JSON}else{'C:\MetaXR\meta_openxr_simulator.json'}
$log=Join-Path $game 'BepInEx\LogOutput.log'
$workspace=Join-Path $dir 'transaction'
$summary=[ordered]@{verdict='INCONCLUSIVE';startedUtc=[DateTime]::UtcNow.ToString('o');error=$null;cleanupError=$null;stockVerified=$false;captures=@()}
$before=(Get-ItemProperty 'HKLM:\SOFTWARE\Khronos\OpenXR\1').ActiveRuntime
$installed=$false;$launched=$false;$code=2
function GameProcesses {@(Get-Process -Name 'Vampire Hunters' -ErrorAction SilentlyContinue)}
function Installer([string]$Action,[string]$Config='') {
    $args=@('-NoProfile','-NonInteractive','-File',(Join-Path $root 'src\core\Install-Stage0.ps1'),'-Action',$Action,'-Workspace',$workspace)
    if($Config){$args+=@('-OpenXrHostConfig',$Config)}
    $line=($args|ForEach-Object{'"'+$_+'"'})-join ' '
    $child=Start-Process powershell -ArgumentList $line -PassThru -NoNewWindow -RedirectStandardOutput (Join-Path $dir ($Action+'.log')) -RedirectStandardError (Join-Path $dir ($Action+'.stderr.log'))
    $handle=$child.Handle
    # TOOL-034: full-tree hashes at every recovery boundary exceeded 300 s on host6.
    # Increase only the bounded transaction budget; never skip hashes or overlap recovery/launch.
    if(-not $child.WaitForExit($InstallerTimeoutSeconds*1000)){Stop-Process $child.Id -Force;throw 'installer backstop; inspect journal'}
    $child.WaitForExit();$summary[$Action+'Exit']=$child.ExitCode
    if($child.ExitCode -ne 0){throw ($Action+' failed')}
}
try {
    if(@(GameProcesses).Count -or (Get-Process OpenXrProbe -ErrorAction SilentlyContinue)){throw 'target/probe already running; no takeover'}
    $sim=Get-Process MetaXRSimulator -ErrorAction SilentlyContinue|Select-Object -First 1
    if(!$sim){throw 'simulator not running'}
    $config=Join-Path $dir 'config.json'
    $target=if($Motion){600}else{120}
    @{Enabled=$true;Output=$dir;Runtime=$simulatorJson;Frames=$target;
        GameplayOnly=[bool]$Gameplay;GameplayWaitSeconds=180}|ConvertTo-Json|Set-Content $config -Encoding UTF8
    $motionRung=0;$motionKey=0
    $summary.motionInputMethod=if($Motion){'official simulator WASD D / right-arrow, 150ms hold, focused simulator; must observe runtime poses'}else{'NOT_REQUESTED_NO_NATIVE_MOTION_INPUT'}
    Write-Output 'transaction install started'
    Installer 'Install' $config;$installed=$true
    $summary.pluginSha256=(Get-FileHash (Join-Path $game 'BepInEx\plugins\VampireHuntersVR.Stage0.dll')).Hash
    $summary.nativeSha256=(Get-FileHash (Join-Path $game 'BepInEx\plugins\VhvrNativeHost.dll')).Hash
    Start-Process 'steam://rungameid/2206270'|Out-Null;$launched=$true
    $deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds);$done=$false;$captured=$false
    $receipt=Join-Path $dir 'host.json'
    while([DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 500
        if(Test-Path $log){$text=[string](Get-Content $log -Raw -ErrorAction SilentlyContinue);if($text -match '\[OPENXR_HOST\] (FAILED|INIT_FAILED)'){throw 'host failed; see game/native logs'}}
        if(Test-Path $receipt){
            try {$r=Get-Content $receipt -Raw|ConvertFrom-Json}catch{continue}
            if(!$captured -and $r.status.Frames -ge 40 -and $r.status.State -ne 3){
                $gp=@(GameProcesses);if(!$gp.Count -or $gp[0].Id -ne $r.processId){throw 'receipt process mismatch'}
                Set-SimulatorViewport $sim 'Both'
                $cap=Save-SimulatorClient $sim (Join-Path $dir 'composed.png')
                if([DateTime]$cap.capturedUtc -lt [DateTime]$r.startedUtc){throw 'capture predates host'}
                $summary.captures+=,$cap;$captured=$true
            }
            if($Motion -and $r.status.Frames -ge 45 -and $motionRung -lt 3 -and $r.status.State -ne 3) {
                Focus-Simulator $sim
                if($motionRung -gt 0) {
                    $motionKey=if($motionRung -eq 1){0x44}else{0x27}
                    [VhvrSimWindow]::keybd_event([byte]$motionKey,0,0,[UIntPtr]::Zero)
                    try {Start-Sleep -Milliseconds 150} finally {[VhvrSimWindow]::keybd_event([byte]$motionKey,0,2,[UIntPtr]::Zero);$motionKey=0}
                }
                [IO.File]::WriteAllText((Join-Path $dir 'capture.request'),[string]$motionRung)
                $captureFile=Join-Path $dir ('host.json.capture'+$motionRung+'.json')
                $motionDeadline=[DateTime]::UtcNow.AddSeconds(8)
                while(!(Test-Path $captureFile) -and [DateTime]::UtcNow -lt $motionDeadline){Start-Sleep -Milliseconds 100}
                if(!(Test-Path $captureFile)){throw 'motion capture response timeout'}
                Start-Sleep -Milliseconds 200
                $cap=Save-SimulatorClient $sim (Join-Path $dir ('motion'+$motionRung+'.png'))
                $summary.captures+=,$cap;$motionRung++
            }
            if($r.status.State -eq 3){$done=$true;break}
        }
    }
    if(!$done){throw 'host timed out; no final receipt'}
    if(!$r.inGameOpenXrHost -or $r.eyeSource -ne 'gameCamera' -or $r.status.AdapterMatched -ne 1 -or
        $r.status.MainThread -eq $r.status.RenderThread){throw 'in-game/device/render-thread receipt identity rejected'}
    if($Gameplay -and ($r.gameplayOnly -ne $true -or $r.sourceCamera -ne 'Main Camera' -or
        $r.scene -match '^(Intro|MainMenu)' -or $r.gameplayIdentity.policy -ne 'active-player-fp-camera-direct-child-v1' -or
        $r.gameplayIdentity.scene -ne $r.scene)){throw 'actual gameplay camera identity missing/rejected'}
    foreach($capture in $summary.captures){
        $time=[DateTime]$capture.capturedUtc
        if($time -lt [DateTime]$r.startedUtc -or $time -gt [DateTime]$r.updatedUtc -or
            (Get-FileHash $capture.path).Hash -ne $capture.sha256){throw 'capture time/hash provenance rejected'}
    }
    if($r.status.Frames -lt $target -or $r.status.GpuCompleted -ne 2*$r.status.Frames -or $r.status.HoldSeconds -lt 3 -or $r.status.Ended -ne 1 -or $r.status.Stopping -ne 1){throw 'frame/sync/hold/shutdown receipt rejected'}
    if(!$captured){throw 'no current-run composed capture'}
    if($Motion -and $motionRung -ne 3){throw 'missing motion capture rungs'}
    if($Motion){
        $summary.motion=@(0,1,2|ForEach-Object{Get-Content (Join-Path $dir ('host.json.capture'+$_+'.json')) -Raw|ConvertFrom-Json})
        $p0=$summary.motion[0].capturedStatus.eyes;$p1=$summary.motion[1].capturedStatus.eyes;$p2=$summary.motion[2].capturedStatus.eyes
        $summary.motionClassification=@(
            (Get-NativeMotionClassification $p0 $p1 translation),
            (Get-NativeMotionClassification $p1 $p2 yaw))
        $summary.negativeMotionControlsVerified=$false # Current three captures do not prove no-input/unselected controls.
        $summary.m4bPass=$false
        if(@($summary.motionClassification|Where-Object {-not $_.passed}).Count){throw 'upright isolated native center translation/pure yaw rejected; see motionClassification'}
        $summary.motionTranslationMeters=$summary.motionClassification[0].centerMeters
        $summary.motionYawDegrees=$summary.motionClassification[1].yawDegrees
    }
    foreach($eye in 0,1){
        $a=Join-Path $dir ('host.json.eye'+$eye+'.raw');$b=Join-Path $dir ('host.json.source'+$eye+'.raw')
        $runtime=[IO.File]::ReadAllBytes($a);$source=[IO.File]::ReadAllBytes($b);$stride=$r.status.Width*4;$height=$r.status.Height
        if($runtime.Length -ne $stride*$height -or $source.Length -ne $runtime.Length){throw 'raw image size'}
        $expected=New-Object byte[] $source.Length
        for($y=0;$y -lt $height;$y++){[Buffer]::BlockCopy($source,($height-1-$y)*$stride,$expected,$y*$stride,$stride)}
        $sha=[Security.Cryptography.SHA256]::Create()
        try {if([Convert]::ToBase64String($sha.ComputeHash($runtime)) -ne [Convert]::ToBase64String($sha.ComputeHash($expected))){throw 'runtime differs from exact Y-flipped source'}}finally{$sha.Dispose()}
    }
    $summary.verdict='TRANSFER_CAPTURED_NOT_M4B_ACCEPTED';$code=0
    if($Gameplay){$summary.verdict='GAMEPLAY_TRANSFER_CAPTURED_NOT_M4B_ACCEPTED';$summary.gameplayIdentity=$r.gameplayIdentity;$summary.m4bPass=$false}
} catch {$summary.verdict='FAIL';$summary.error=$_.Exception.ToString();Write-Output ('HOST STOP: '+$_.Exception.Message)}
finally {
    if($launched){foreach($p in @(GameProcesses)){[void]$p.CloseMainWindow()};Start-Sleep -Seconds 5;foreach($p in @(GameProcesses)){Stop-Process $p.Id -Force}}
    if(Test-Path $log){try {Copy-Item $log (Join-Path $dir 'game.log')} catch {$summary.cleanupError='log capture: '+$_.Exception.Message}}
    if($installed -or (Test-Path (Join-Path $workspace 'journal.jsonl'))){
        try {Installer 'Uninstall'; & powershell -NoProfile -NonInteractive -File (Join-Path $root 'artifacts\devtools\Check-Stock.ps1') > (Join-Path $dir 'stock.log');if($LASTEXITCODE -ne 0){throw 'stock differs'};$summary.stockVerified=$true}
        catch {$summary.cleanupError=$_.Exception.ToString();$summary.verdict='FAIL';$code=2}
    }
    $summary.runtimeBefore=$before;$summary.runtimeAfter=(Get-ItemProperty 'HKLM:\SOFTWARE\Khronos\OpenXR\1').ActiveRuntime
    if($summary.runtimeAfter -ne $before){$summary.error='runtime changed';$code=2;$summary.verdict='FAIL'}
    $summary.finishedUtc=[DateTime]::UtcNow.ToString('o');$summary|ConvertTo-Json -Depth 10|Set-Content (Join-Path $dir 'run.json') -Encoding UTF8
}
Write-Output ($summary.verdict+' stock='+$summary.stockVerified)
exit $code