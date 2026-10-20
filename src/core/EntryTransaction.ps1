$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '..\..\tests\EntrySupport.ps1')

# Current-stage filesystem engine only. No game launch, Steam changes or stock restore.
# Callers must independently establish deployment consent and fresh pinned preflight.
function Assert-TransactionStopped([scriptblock]$Guard) {
    if ($null -eq $Guard) { throw 'An explicit target guard is required.' }
    if (@(Get-Process | Where-Object { $_.ProcessName -eq 'Vampire Hunters' }).Count) {
        throw 'Game running; transaction stopped.'
    }
    & $Guard | Out-Null
}

function Assert-TransactionPaths([string]$Target, [string]$Workspace) {
    Assert-BackupPathsSeparate $Target $Workspace
    $a = Get-BackupFullPath $Target
    $b = Get-BackupFullPath $Workspace
    if ($a.StartsWith('\\') -or $b.StartsWith('\\') -or
        [IO.Path]::GetPathRoot($a) -cne [IO.Path]::GetPathRoot($b)) {
        throw 'Transaction requires separate local paths on the same volume.'
    }
    if (-not (Test-Path -LiteralPath $Target -PathType Container)) { throw 'Target missing.' }
}

function Get-TransactionMutexName([string]$Target) {
    $bytes = [Text.Encoding]::UTF8.GetBytes((Get-BackupFullPath $Target).ToUpperInvariant())
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { $hash = [BitConverter]::ToString($algorithm.ComputeHash($bytes)).Replace('-', '') }
    finally { $algorithm.Dispose() }
    return 'Local\VampireHuntersVR.Entry.' + $hash
}

function Invoke-TransactionExclusive([string]$Target, [scriptblock]$Operation) {
    # Serializes this tool in the current Windows session; not Steam or another user session.
    $mutex = New-Object Threading.Mutex($false, (Get-TransactionMutexName $Target))
    $held = $false
    try {
        try { $held = $mutex.WaitOne(0) }
        catch [Threading.AbandonedMutexException] { $held = $true }
        if (-not $held) { throw 'Another transaction owns this target; no parallel mutation.' }
        & $Operation
    } finally {
        if ($held) { $mutex.ReleaseMutex() }
        $mutex.Dispose()
    }
}

function Get-TransactionPlan([string]$Target, [object[]]$Files, [string[]]$Scopes = @(), [object[]]$Mutations = @()) {
    if ($Files.Count -eq 0) { throw 'Empty transaction payload.' }
    $seen = @{}
    foreach ($file in $Files) {
        $path = ConvertTo-EntryRelativePath $file.Path
        if ($file.Path -cne $path) { throw 'Canonical payload path required.' }
        if ($seen.ContainsKey($path) -or $file.Bytes -lt 0 -or $file.SHA256 -cnotmatch '^[A-F0-9]{64}$') {
            throw 'Invalid/duplicate transaction record.'
        }
        $seen[$path] = $true
    }
    foreach ($path in $seen.Keys) {
        $parent = [IO.Path]::GetDirectoryName($path)
        while ($parent) {
            if ($seen.ContainsKey($parent)) { throw 'Payload file is also an ancestor directory.' }
            $parent = [IO.Path]::GetDirectoryName($parent)
        }
    }
    if ($Target -cne (Get-BackupFullPath $Target)) { throw 'Canonical target path required.' }
    if (@($Scopes).Count -eq 0) {
        # Legacy shape: one owned root per top-level segment, restricted to loader-owned names.
        $roots = @{}
        foreach ($path in $seen.Keys) {
            $top = $path.Split('\')[0]
            if (@('BepInEx','dotnet','doorstop_config.ini','.doorstop_version','changelog.txt','winhttp.dll') -cnotcontains $top) {
                throw 'Unexpected transaction root.'
            }
            if (($top -in @('BepInEx','dotnet')) -eq ($path -eq $top)) { throw 'Invalid root file/directory shape.' }
            $roots[$top] = $true
        }
        $scopeList = @($roots.Keys | Sort-Object)
    } else {
        # Declared scopes: any narrow path the payload owns, inside stock or beside it. Every payload file
        # must live inside exactly one scope, and no scope may contain stock that is not a declared mutation.
        $scopeSet = @{}
        foreach ($scope in $Scopes) {
            $relative = ConvertTo-EntryRelativePath $scope
            if ($relative -cne $scope) { throw 'Canonical scope path required.' }
            if ($scopeSet.ContainsKey($relative)) { throw 'Duplicate scope.' }
            $scopeSet[$relative] = $true
        }
        foreach ($scope in $scopeSet.Keys) {
            foreach ($other in $scopeSet.Keys) {
                if ($other -cne $scope -and (Test-EntryPathInScope $other $scope)) { throw 'Nested scopes are not supported.' }
            }
        }
        foreach ($path in $seen.Keys) {
            if (@($Scopes | Where-Object { Test-EntryPathInScope $path $_ }).Count -ne 1) {
                throw ('Payload file is not inside exactly one declared scope: ' + $path)
            }
            # A nested payload file is always covered by a directory scope, so declaring the file itself is a mistake.
            # A top-level file has no parent directory to declare: it is its own scope (same shape the legacy branch
            # gives winhttp.dll / doorstop_config.ini / .doorstop_version / changelog.txt).
            if ($scopeSet.ContainsKey($path) -and [IO.Path]::GetDirectoryName($path)) {
                throw ('Declare the parent scope instead of the payload file itself: ' + $path)
            }
        }
        $scopeList = @($scopeSet.Keys | Sort-Object)
    }
    $mutationList = @()
    if (@($Mutations).Count -gt 0) {
        $mutationSeen = @{}
        foreach ($mutation in $Mutations) {
            $relative = ConvertTo-EntryRelativePath $mutation.Path
            if ($relative -cne $mutation.Path) { throw 'Canonical mutation path required.' }
            if ($mutationSeen.ContainsKey($relative)) { throw 'Duplicate mutation.' }
            if (@($scopeList | Where-Object { Test-EntryPathInScope $relative $_ }).Count -ne 0) {
                throw ('A declared mutation must not sit inside an owned scope: ' + $relative)
            }
            if (@($seen.Keys) -ccontains $relative) { throw ('A declared mutation cannot also be a payload file: ' + $relative) }
            if ($mutation.StockBytes -lt 0 -or $mutation.StockSHA256 -cnotmatch '^[A-F0-9]{64}$') { throw 'Invalid mutation pre-image record.' }
            $backupRoot = Get-BackupFullPath $mutation.BackupRoot
            $preImage = Join-Path $backupRoot $relative
            if (-not (Test-Path -LiteralPath $preImage -PathType Leaf)) { throw ('Mutation pre-image is missing from the pinned backup: ' + $relative) }
            $record = Get-EntryFileRecord $preImage $relative
            if ($record.Bytes -ne $mutation.StockBytes -or $record.SHA256 -cne $mutation.StockSHA256) {
                throw ('Mutation pre-image differs from the pinned backup: ' + $relative)
            }
            $mutationSeen[$relative] = [pscustomobject]@{
                Path=$relative; StockBytes=[long]$mutation.StockBytes; StockSHA256=$mutation.StockSHA256; BackupRoot=$backupRoot
            }
        }
        $mutationList = @($mutationSeen.Values | Sort-Object Path)
    }
    return [pscustomobject]@{ Files=@($Files | Sort-Object Path); Roots=$scopeList; Mutations=$mutationList }
}

function Assert-TransactionStock([string]$Target, $Stock, [string[]]$Roots, $Mutations = @(), $ObservedMutations = @{}) {
    if ((Get-BackupFullPath $Stock.Root) -cne (Get-BackupFullPath $Target) -or @($Stock.Files).Count -eq 0) {
        throw 'Stock inventory target missing/different.'
    }
    $seen = @{}
    foreach ($path in @($Stock.Directories) + @($Stock.Files | ForEach-Object { $_.RelativePath })) {
        $relative = ConvertTo-EntryRelativePath $path
        if ($seen.ContainsKey($relative)) { throw 'Duplicate/aliased stock inventory path.' }
        if (@($Roots | Where-Object { Test-EntryPathInScope $relative $_ }).Count -ne 0) {
            throw ('Owned scope collides with pinned stock inventory: ' + $relative)
        }
        $seen[$relative] = $true
    }
    foreach ($mutation in $Mutations) {
        if (-not $seen.ContainsKey($mutation.Path)) { throw ('Declared mutation is not a pinned stock path: ' + $mutation.Path) }
    }
    $current = Get-BackupInventory $Target
    $scopeFilter = {
        param($relative)
        return (@($Roots | Where-Object { Test-EntryPathInScope $relative $_ }).Count -eq 0)
    }
    $mutated = @($Mutations | ForEach-Object { $_.Path })
    $filtered = [pscustomobject]@{
        Directories=@($current.Directories | Where-Object { & $scopeFilter $_ })
        Files=@($current.Files | Where-Object { (& $scopeFilter $_.RelativePath) -and $mutated -cnotcontains $_.RelativePath })
    }
    $expected = [pscustomobject]@{
        Directories=@($Stock.Directories)
        Files=@($Stock.Files | Where-Object { $mutated -cnotcontains $_.RelativePath })
    }
    Assert-BackupInventoryEqual $expected $filtered -CheckSourceTimes
    foreach ($mutation in $Mutations) {
        $live = Join-Path $Target $mutation.Path
        Assert-BackupPathSafe $live
        if (-not (Test-Path -LiteralPath $live -PathType Leaf)) { throw ('Declared mutation file is missing: ' + $mutation.Path) }
        $record = Get-EntryFileRecord $live $mutation.Path
        if ($record.Bytes -eq $mutation.StockBytes -and $record.SHA256 -ceq $mutation.StockSHA256) { continue }
        if ($ObservedMutations.ContainsKey($mutation.Path) -and $ObservedMutations[$mutation.Path] -ccontains $record.SHA256) {
            # A recorded rewrite of a declared stock file: accepted only because recovery will restore the pre-image.
            continue
        }
        throw ('Declared mutation differs from its pinned pre-image and has no recorded observation: ' + $mutation.Path)
    }
}

function Write-TransactionEvent([string]$Workspace, $Event) {
    $journal = Join-Path $Workspace 'journal.jsonl'
    Assert-BackupPathSafe $journal
    $sequence = 0
    if (Test-Path -LiteralPath $journal) { $sequence = [IO.File]::ReadAllLines($journal).Length }
    $Event['Sequence'] = $sequence
    $Event['Utc'] = [DateTime]::UtcNow.ToString('o')
    $bytes = [Text.Encoding]::UTF8.GetBytes(($Event | ConvertTo-Json -Depth 12 -Compress) + "`n")
    $stream = [IO.File]::Open($journal, [IO.FileMode]::Append, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
}

function Get-TransactionJournal([string]$Target, [string]$Workspace, $Stock, [switch]$RecordObservedMutations) {
    Assert-TransactionPaths $Target $Workspace
    $journal = Join-Path $Workspace 'journal.jsonl'
    Assert-BackupPathSafe $journal
    $raw = [IO.File]::ReadAllText($journal)
    if (-not $raw.EndsWith("`n")) { throw 'Incomplete journal tail; manual review required.' }
    $events = @($raw.TrimEnd("`r", "`n").Split("`n") | ForEach-Object { $_ | ConvertFrom-Json })
    $header = $events[0]
    if ($header.Type -cne 'Begin' -or $header.Version -ne 1 -or
        $header.Target -cne (Get-BackupFullPath $Target) -or $header.Workspace -cne (Get-BackupFullPath $Workspace)) {
        throw 'Journal identity differs.'
    }
    Assert-BackupInventoryEqual $Stock $header.Stock -CheckSourceTimes
    $scopes = @()
    if ($null -ne $header.Scopes) { $scopes = @($header.Scopes) }
    $mutations = @()
    if ($null -ne $header.Mutations) { $mutations = @($header.Mutations) }
    $generated = @()
    if ($null -ne $header.Generated) { $generated = @($header.Generated) }
    $plan = Get-TransactionPlan $Target @($header.Files) $scopes $mutations
    if ($null -ne $header.Roots -and @(Compare-Object @($header.Roots) @($plan.Roots)).Count -gt 0) {
        throw 'Journal scope list differs from its payload.'
    }
    $published = @{}; $intents = @{}; $quarantine = @{}; $moved = @{}
    $observed = @{}
    $ready = $false; $installed = $false; $recovered = $false
    for ($i = 0; $i -lt $events.Count; $i++) {
        $event = $events[$i]
        if ($event.Sequence -ne $i -or $recovered) { throw 'Invalid journal sequence/state.' }
        switch -CaseSensitive ($event.Type) {
            'Begin' { if ($i -ne 0) { throw 'Repeated journal header.' } }
            'Ready' { if ($ready -or $intents.Count -or $quarantine.Count) { throw 'Invalid Ready state.' }; $ready = $true }
            'PublishIntent' {
                if (-not $ready -or $installed -or $quarantine.Count -or $plan.Roots -cnotcontains $event.Root -or $intents.ContainsKey($event.Root)) {
                    throw 'Invalid publish intent.'
                }
                $intents[$event.Root] = $true
            }
            'Published' {
                if (-not $intents.ContainsKey($event.Root) -or $published.ContainsKey($event.Root) -or $quarantine.Count) { throw 'Invalid publish result.' }
                $published[$event.Root] = $true
            }
            'Installed' {
                if ($installed -or $published.Count -ne $plan.Roots.Count -or $quarantine.Count) { throw 'Invalid Installed state.' }
                $installed = $true
            }
            'QuarantineIntent' {
                if (-not $intents.ContainsKey($event.Root) -or $quarantine.ContainsKey($event.Root)) { throw 'Invalid quarantine intent.' }
                $quarantine[$event.Root] = $event.Inventory
            }
            'Quarantined' {
                if (-not $quarantine.ContainsKey($event.Root) -or $moved.ContainsKey($event.Root)) { throw 'Invalid quarantine result.' }
                $moved[$event.Root] = $true
            }
            'MutationObserved' {
                if ($plan.Mutations.Path -cnotcontains $event.Path -or $event.SHA256 -cnotmatch '^[A-F0-9]{64}$') { throw 'Invalid mutation observation.' }
                if (-not $observed.ContainsKey($event.Path)) { $observed[$event.Path] = @() }
                $observed[$event.Path] = @($observed[$event.Path] + $event.SHA256)
            }
            'MutationRestored' {
                if ($plan.Mutations.Path -cnotcontains $event.Path) { throw 'Invalid mutation restore event.' }
            }
            'Recovered' { $recovered = $true }
            default { throw 'Unknown journal event.' }
        }
    }
    $liveObserved = @{}
    foreach ($mutation in $plan.Mutations) {
        if ($observed.ContainsKey($mutation.Path)) { $liveObserved[$mutation.Path] = @($observed[$mutation.Path]) }
    }
    if ($RecordObservedMutations) {
        foreach ($mutation in $plan.Mutations) {
            $live = Join-Path $Target $mutation.Path
            if (-not (Test-Path -LiteralPath $live -PathType Leaf)) { throw ('Declared mutation file is missing: ' + $mutation.Path) }
            $record = Get-EntryFileRecord $live $mutation.Path
            if ($record.Bytes -eq $mutation.StockBytes -and $record.SHA256 -ceq $mutation.StockSHA256) { continue }
            Write-TransactionEvent $Workspace ([ordered]@{Type='MutationObserved';Path=$mutation.Path;Bytes=$record.Bytes;SHA256=$record.SHA256})
            if (-not $liveObserved.ContainsKey($mutation.Path)) { $liveObserved[$mutation.Path] = @() }
            $liveObserved[$mutation.Path] = @($liveObserved[$mutation.Path] + $record.SHA256)
        }
    }
    Assert-TransactionStock $Target $Stock $plan.Roots $plan.Mutations $liveObserved
    return [pscustomobject]@{
        Plan=$plan; Intents=$intents; Published=$published; Quarantine=$quarantine; Moved=$moved
        Installed=$installed; Recovered=$recovered; ObservedMutations=$liveObserved; Generated=$generated
    }
}

function Assert-TransactionPayload([string]$Base, [string]$Top, [object[]]$Files, [object[]]$Generated = @(), [switch]$AllowGenerated) {
    $path = Join-Path $Base $Top
    Assert-BackupPathSafe $path
    $expected = @($Files | Where-Object { Test-EntryPathInScope $_.Path $Top })
    foreach ($file in $expected) {
        $actual = Get-EntryFileRecord (Join-Path $Base $file.Path) $file.Path
        if ($actual.Bytes -ne $file.Bytes -or $actual.SHA256 -cne $file.SHA256) {
            throw 'Owned payload changed/missing; preserve for review.'
        }
    }
    # Declared generated files are written by the installed tool itself (e.g. UUVR's patcher) after a launch.
    # Missing is acceptable only before that tool has run; present must match the declared bytes exactly.
    $scopedGenerated = @($Generated | Where-Object { Test-EntryPathInScope $_.Path $Top })
    foreach ($file in $scopedGenerated) {
        $live = Join-Path $Base $file.Path
        if (-not (Test-Path -LiteralPath $live -PathType Leaf)) {
            if ($AllowGenerated) { continue }
            throw ('Declared generated file is missing: ' + $file.Path)
        }
        $actual = Get-EntryFileRecord $live $file.Path
        if ($actual.Bytes -ne $file.Bytes -or $actual.SHA256 -cne $file.SHA256) {
            throw ('Declared generated file changed; preserve for review: ' + $file.Path)
        }
    }
    if ($Top -in @('BepInEx','dotnet')) {
        $tree = @(Get-BackupTree $path)
        if (-not $AllowGenerated -and @($tree | Where-Object { -not $_.PSIsContainer }).Count -ne $expected.Count) {
            throw 'Uncommitted root contains unexpected files.'
        }
        return
    }
    if ($scopedGenerated.Count -gt 0 -and (Test-Path -LiteralPath $path -PathType Container)) {
        # The tree walk reports paths relative to the scope, so compare against scope-relative names.
        $prefix = $Top + '\'
        $allowed = @(@($expected) + @($scopedGenerated) | ForEach-Object {
            if ($_.Path.StartsWith($prefix, [StringComparison]::Ordinal)) { $_.Path.Substring($prefix.Length) }
            else { throw ('Scope member path is not inside its scope: ' + $_.Path) }
        })
        foreach ($item in @(Get-BackupTree $path | Where-Object { -not $_.PSIsContainer })) {
            $relative = $item.FullName.Substring($path.Length + 1)
            if ($allowed -cnotcontains $relative) { throw ('Scope contains undeclared files; preserve for review: ' + $relative) }
        }
    }
}

function Get-TransactionRootInventory([string]$Base, [string]$Top) {
    $path = Join-Path $Base $Top
    if (Test-Path -LiteralPath $path -PathType Container) { return Get-BackupInventory $path }
    $record = Get-EntryFileRecord $path $Top
    return [pscustomobject]@{
        Directories=@(); Files=@([pscustomobject]@{
            RelativePath=$Top; Bytes=$record.Bytes; SHA256=$record.SHA256
            LastWriteUtcTicks=(Get-Item -LiteralPath $path).LastWriteTimeUtc.Ticks
        })
    }
}

function Move-TransactionRoot([string]$From, [string]$To, [string]$Top) {
    Assert-BackupPathSafe $From
    Assert-BackupPathSafe $To
    # A declared scope may carry no installed files at all (only tool-generated ones, written later or never):
    # there is nothing to publish, and the scope stays reserved by the no-merge check.
    if (-not (Test-Path -LiteralPath $From)) { return }
    if (Test-Path -LiteralPath $To) { throw 'Move destination exists; no overwrite.' }
    $parent = [IO.Path]::GetDirectoryName($To)
    if ($parent) { [IO.Directory]::CreateDirectory($parent) | Out-Null }
    if (Test-Path -LiteralPath $From -PathType Container) { [IO.Directory]::Move($From, $To) }
    else { [IO.File]::Move($From, $To) }
}

function Invoke-EntryTransactionCore {
    param([string]$Target, [object[]]$Files, $Stock, [string]$Workspace,
        [scriptblock]$Guard, [scriptblock]$Checkpoint = {},
        [string[]]$Scopes = @(), [object[]]$Mutations = @(), [object[]]$Generated = @())
    Assert-TransactionPaths $Target $Workspace
    Assert-TransactionStopped $Guard
    if (Test-Path -LiteralPath $Workspace) { throw 'Workspace exists; use explicit recovery, never resume installation.' }
    $plan = Get-TransactionPlan $Target $Files $Scopes $Mutations
    Assert-TransactionStock $Target $Stock $plan.Roots $plan.Mutations
    foreach ($top in $plan.Roots) {
        if (Test-Path -LiteralPath (Join-Path $Target $top)) { throw 'Existing target root; no merge.' }
    }
    foreach ($file in $plan.Files) {
        Assert-BackupPathsSeparate $Target $file.Source
        Assert-BackupPathsSeparate $Workspace $file.Source
        $actual = Get-EntryFileRecord $file.Source $file.Path
        if ($actual.Bytes -ne $file.Bytes -or $actual.SHA256 -cne $file.SHA256) { throw 'Source differs from plan.' }
    }
    New-Item -ItemType Directory -Path $Workspace | Out-Null
    $stage = Join-Path $Workspace 'stage'
    New-Item -ItemType Directory -Path $stage | Out-Null
    $payload = @($plan.Files | ForEach-Object { [pscustomobject]@{Path=$_.Path;Bytes=$_.Bytes;SHA256=$_.SHA256} })
    Write-TransactionEvent $Workspace ([ordered]@{
        Type='Begin'; Version=1; Target=(Get-BackupFullPath $Target); Workspace=(Get-BackupFullPath $Workspace)
        Files=$payload; Stock=$Stock; Scopes=@($Scopes); Roots=@($plan.Roots); Mutations=@($plan.Mutations); Generated=@($Generated)
    })
    foreach ($file in $plan.Files) {
        Assert-TransactionStopped $Guard
        $to = Join-Path $stage $file.Path
        Assert-BackupPathSafe $file.Source
        Assert-BackupPathSafe $to
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($to)) | Out-Null
        $input = [IO.File]::Open($file.Source, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        try {
            $output = [IO.File]::Open($to, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
            try { $input.CopyTo($output); $output.Flush($true) } finally { $output.Dispose() }
        } finally { $input.Dispose() }
        & $Checkpoint 'StagedFile' $file.Path | Out-Null
    }
    foreach ($top in $plan.Roots) { Assert-TransactionPayload $stage $top $payload }
    Write-TransactionEvent $Workspace ([ordered]@{Type='Ready'})
    foreach ($top in $plan.Roots) {
        Assert-TransactionStopped $Guard
        Assert-TransactionStock $Target $Stock $plan.Roots $plan.Mutations
        Write-TransactionEvent $Workspace ([ordered]@{Type='PublishIntent';Root=$top})
        & $Checkpoint 'BeforePublish' $top | Out-Null
        Assert-TransactionStopped $Guard
        Assert-TransactionPayload $stage $top $payload
        Move-TransactionRoot (Join-Path $stage $top) (Join-Path $Target $top) $top
        & $Checkpoint 'AfterPublish' $top | Out-Null
        Assert-TransactionPayload $Target $top $payload $Generated -AllowGenerated
        Write-TransactionEvent $Workspace ([ordered]@{Type='Published';Root=$top})
    }
    Assert-TransactionStopped $Guard
    Assert-TransactionStock $Target $Stock $plan.Roots $plan.Mutations
    foreach ($top in $plan.Roots) { Assert-TransactionPayload $Target $top $payload $Generated -AllowGenerated }
    Write-TransactionEvent $Workspace ([ordered]@{Type='Installed'})
    return [pscustomobject]@{ Status='ADDITIONS_INSTALLED'; Workspace=$Workspace; Files=$payload.Count; GameLaunched=$false }
}

function Undo-EntryTransactionCore {
    param([string]$Target, $Stock, [string]$Workspace, [scriptblock]$Guard, [scriptblock]$Checkpoint = {})
    Assert-TransactionStopped $Guard
    $state = Get-TransactionJournal $Target $Workspace $Stock -RecordObservedMutations
    $stage = Join-Path $Workspace 'stage'
    $quarantine = Join-Path $Workspace 'quarantine'
    # Validate EVERY root before moving anything. Ambiguous/foreign/modified roots remain untouched.
    foreach ($top in $state.Plan.Roots) {
        $live = Join-Path $Target $top; $staged = Join-Path $stage $top; $saved = Join-Path $quarantine $top
        Assert-BackupPathSafe $live; Assert-BackupPathSafe $staged; Assert-BackupPathSafe $saved
        if (Test-Path -LiteralPath $saved) {
            if (-not $state.Quarantine.ContainsKey($top) -or (Test-Path -LiteralPath $live) -or (Test-Path -LiteralPath $staged)) {
                throw 'Ambiguous quarantine root.'
            }
            Assert-BackupInventoryEqual $state.Quarantine[$top] (Get-TransactionRootInventory $quarantine $top) -CheckSourceTimes
        } elseif (Test-Path -LiteralPath $live) {
            if ($state.Recovered -or -not $state.Intents.ContainsKey($top) -or (Test-Path -LiteralPath $staged) -or $state.Moved.ContainsKey($top)) {
                throw 'Foreign/ambiguous target root; no cleanup.'
            }
            Assert-TransactionPayload $Target $top $state.Plan.Files $state.Generated -AllowGenerated:$state.Installed
            if ($state.Quarantine.ContainsKey($top)) {
                Assert-BackupInventoryEqual $state.Quarantine[$top] (Get-TransactionRootInventory $Target $top) -CheckSourceTimes
            }
        } elseif ($state.Published.ContainsKey($top) -or $state.Quarantine.ContainsKey($top)) {
            # A scope that never carried installed files may legitimately be absent (its declared generated files
            # are written by the installed tool, or never at all); a scope with installed files may not.
            if (@($state.Plan.Files | Where-Object { Test-EntryPathInScope $_.Path $top }).Count -gt 0) {
                throw 'Published root missing; manual review required.'
            }
        } elseif ($state.Intents.ContainsKey($top) -and -not (Test-Path -LiteralPath $staged)) {
            throw 'Incomplete publish has neither source nor destination.'
        }
    }
    if ($state.Recovered) { return [pscustomobject]@{Status='ALREADY_RECOVERED';Workspace=$Workspace} }
    [IO.Directory]::CreateDirectory($quarantine) | Out-Null
    foreach ($top in $state.Plan.Roots) {
        Assert-TransactionStopped $Guard
        Assert-TransactionStock $Target $Stock $state.Plan.Roots $state.Plan.Mutations $state.ObservedMutations
        $live = Join-Path $Target $top; $saved = Join-Path $quarantine $top
        if (Test-Path -LiteralPath $live) {
            Assert-TransactionPayload $Target $top $state.Plan.Files $state.Generated -AllowGenerated:$state.Installed
            if (-not $state.Quarantine.ContainsKey($top)) {
                Write-TransactionEvent $Workspace ([ordered]@{Type='QuarantineIntent';Root=$top;Inventory=(Get-TransactionRootInventory $Target $top)})
            }
            & $Checkpoint 'BeforeQuarantine' $top | Out-Null
            Assert-TransactionStopped $Guard
            $fresh = Get-TransactionJournal $Target $Workspace $Stock
            Assert-BackupInventoryEqual $fresh.Quarantine[$top] (Get-TransactionRootInventory $Target $top) -CheckSourceTimes
            Move-TransactionRoot $live $saved $top
            & $Checkpoint 'AfterQuarantine' $top | Out-Null
        }
        if ((Test-Path -LiteralPath $saved) -and -not $state.Moved.ContainsKey($top)) {
            $fresh = Get-TransactionJournal $Target $Workspace $Stock
            Assert-BackupInventoryEqual $fresh.Quarantine[$top] (Get-TransactionRootInventory $quarantine $top) -CheckSourceTimes
            Write-TransactionEvent $Workspace ([ordered]@{Type='Quarantined';Root=$top})
        }
    }
    # Declared stock mutations: record what the installed tool actually wrote, then put the pinned pre-image
    # back byte-for-byte and restore its recorded write time so the final stock comparison stays exact.
    foreach ($mutation in $state.Plan.Mutations) {
        Assert-TransactionStopped $Guard
        $live = Join-Path $Target $mutation.Path
        $source = Join-Path $mutation.BackupRoot $mutation.Path
        Assert-BackupPathSafe $live
        Assert-BackupPathSafe $source
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw ('Pinned pre-image is missing: ' + $mutation.Path) }
        $record = Get-EntryFileRecord $live $mutation.Path
        if ($record.Bytes -eq $mutation.StockBytes -and $record.SHA256 -ceq $mutation.StockSHA256) { continue }
        Remove-Item -LiteralPath $live -Force
        [IO.File]::Copy($source, $live, $false)
        $stockRecord = @($Stock.Files | Where-Object { $_.RelativePath -ceq $mutation.Path })
        if ($stockRecord.Count -ne 1) { throw ('Pinned stock record is missing for the mutation: ' + $mutation.Path) }
        [IO.File]::SetLastWriteTimeUtc($live, [DateTime]::new([long]$stockRecord[0].LastWriteUtcTicks, [DateTimeKind]::Utc))
        $restored = Get-EntryFileRecord $live $mutation.Path
        if ($restored.Bytes -ne $mutation.StockBytes -or $restored.SHA256 -cne $mutation.StockSHA256) {
            throw ('Restored stock file does not match its pinned pre-image: ' + $mutation.Path)
        }
        Write-TransactionEvent $Workspace ([ordered]@{Type='MutationRestored';Path=$mutation.Path;Bytes=$restored.Bytes;SHA256=$restored.SHA256})
    }
    Assert-TransactionStopped $Guard
    Assert-BackupInventoryEqual $Stock (Get-BackupInventory $Target) -CheckSourceTimes
    Write-TransactionEvent $Workspace ([ordered]@{Type='Recovered'})
    return [pscustomobject]@{Status='ADDITIONS_QUARANTINED_STOCK_VERIFIED';Workspace=$Workspace;GameLaunched=$false}
}

function Invoke-EntryTransaction {
    param([string]$Target, [object[]]$Files, $Stock, [string]$Workspace,
        [scriptblock]$Guard, [scriptblock]$Checkpoint = {},
        [string[]]$Scopes = @(), [object[]]$Mutations = @(), [object[]]$Generated = @())
    $arguments = $PSBoundParameters
    Invoke-TransactionExclusive $Target { Invoke-EntryTransactionCore @arguments }
}

function Undo-EntryTransaction {
    param([string]$Target, $Stock, [string]$Workspace, [scriptblock]$Guard, [scriptblock]$Checkpoint = {})
    $arguments = $PSBoundParameters
    Invoke-TransactionExclusive $Target { Undo-EntryTransactionCore @arguments }
}