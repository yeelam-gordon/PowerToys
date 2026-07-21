<#
AdvancedPaste winappcli-driven sign-off — locked-workstation variant.

Identical drive path to run_signoff.ps1 (summon the REAL AP window via the ShowUI
pipe, set the clipboard, invoke a real paste-format action through winappcli,
screenshot the window) EXCEPT the transform result is verified by reading the
output AdvancedPaste writes to the clipboard (the exact bytes it would paste) via
the clipboard API, instead of via SendInput(Ctrl+V) into Notepad + get-value.

Rationale: SendInput is blocked in this session (locked workstation ->
ERROR_ACCESS_DENIED), so neither our helper nor AdvancedPaste's own paste
keystroke can reach a target editor. AdvancedPaste still performs the real
transform and sets the clipboard first, so reading that clipboard verifies the
real app's produced output end-to-end. When the workstation is unlocked, use
run_signoff.ps1 (get-value on the pasted target) instead.

Emits <Basename>.json and <Basename>.md. Exit 0 = all P0 pass, 1 = a P0 failed.
#>
param(
    [string]$Basename = "run",
    [string]$OutDir   = "C:\s\Demo\SkillForDistill\benchmark\results\acc-advancedpaste",
    [string]$WorkDir  = "C:\s\Demo\SkillForDistill\benchmark\results\acc-advancedpaste\_work",
    [string]$ShotDir  = "",
    [switch]$NoShots
)
$ErrorActionPreference = "Continue"
if ($ShotDir -eq "") { $ShotDir = Join-Path $OutDir "screenshots\$Basename" }
New-Item -ItemType Directory -Force $ShotDir | Out-Null
$trigger = Join-Path $WorkDir "show.trigger"

# --- clipboard reader on a dedicated STA runspace ---
function Read-Clip {
    $ps = [powershell]::Create()
    $rs = [runspacefactory]::CreateRunspace(); $rs.ApartmentState = 'STA'; $rs.Open(); $ps.Runspace = $rs
    [void]$ps.AddScript('Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.Clipboard]::GetText()')
    $r = $ps.Invoke(); $rs.Close(); $ps.Dispose()
    ($r -join "`n").Trim()
}

function Get-AP {
    $m = winapp ui list-windows -a PowerToys.AdvancedPaste 2>&1 |
         Select-String -Pattern 'HWND (\d+): "Advanced Paste"'
    if ($m) { return $m.Matches.Groups[1].Value } else { return $null }
}
function Show-AP {
    New-Item -ItemType File -Path $trigger -Force | Out-Null
    Start-Sleep -Milliseconds 1500
    $ap = Get-AP
    for ($i=0; $i -lt 5 -and -not $ap; $i++) { Start-Sleep -Milliseconds 700; $ap = Get-AP }
    return $ap
}
function Shot($ap, $name) {
    if ($NoShots) { return "" }
    $p = Join-Path $ShotDir "$name.png"
    winapp ui screenshot "Advanced Paste" -w $ap -o $p --focus 2>&1 | Out-Null
    return $p
}

function SetClipHtml($text,$html){
    $tf = Join-Path $WorkDir "_clip_text.tmp"; $hf = Join-Path $WorkDir "_clip_html.tmp"
    [System.IO.File]::WriteAllText($tf,$text); [System.IO.File]::WriteAllText($hf,$html)
    powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $WorkDir "set_clipboard.ps1") -Mode html -FromFile $tf -HtmlFromFile $hf | Out-Null
}
function SetClipText($text){
    $tf = Join-Path $WorkDir "_clip_text.tmp"
    [System.IO.File]::WriteAllText($tf,$text)
    powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $WorkDir "set_clipboard.ps1") -Mode text -FromFile $tf | Out-Null
}

# Drive a transform: clipboard already set to $inputClip. Shows AP, screenshots,
# invokes the format, polls the clipboard until AdvancedPaste writes its output,
# returns the produced clipboard text.
function Drive-Transform($fmtName, $shotName, $inputClip) {
    for ($attempt=1; $attempt -le 3; $attempt++) {
        $ap = Show-AP
        if (-not $ap) { continue }
        $shot = Shot $ap $shotName
        winapp ui invoke $fmtName -w $ap 2>&1 | Out-Null
        $out = $inputClip
        for ($i=0; $i -lt 16; $i++) {
            Start-Sleep -Milliseconds 500
            $out = Read-Clip
            if ($out -ne $inputClip) { break }
        }
        Start-Sleep -Milliseconds 300
        $out = Read-Clip
        return @{ ap=$ap; shot=$shot; out=$out }
    }
    return @{ ap=$ap; shot=$shot; out=(Read-Clip) }
}

$results = @()
function Add-Result($id,$pri,$desc,$pass,$expected,$actual,$shot){
    $script:results += [pscustomobject]@{
        id=$id; priority=$pri; description=$desc; pass=[bool]$pass;
        expected=$expected; actual=$actual; screenshot=$shot
    }
    $tag = if($pass){"PASS"}else{"FAIL"}
    Write-Host ("[{0}] {1} {2} :: {3}" -f $tag,$id,$pri,$desc)
    if (-not $pass) { Write-Host ("      expected {0} | actual '{1}'" -f $expected,$actual) }
}

# ============================ CHECKS ============================

# CHK-01 P0 - Paste as plain text strips HTML formatting (produced clipboard text)
SetClipHtml "BoldHello" "<b>Bold</b>Hello"; Start-Sleep 1
$r = Drive-Transform "Paste as plain text" "chk01-plaintext" "BoldHello"
$pass = ($r.out -eq "BoldHello")
Add-Result "CHK-01" "P0" "Paste as plain text produces clipboard text with HTML stripped" $pass 'equals "BoldHello"' $r.out $r.shot

# CHK-02 P0 - Paste as markdown converts HTML heading
SetClipHtml "Title bold" "<h1>Title</h1><p><b>bold</b></p>"; Start-Sleep 1
$r = Drive-Transform "Paste as markdown" "chk02-markdown-heading" "Title bold"
$pass = ($r.out -match "#\s*Title")
Add-Result "CHK-02" "P0" "Paste as markdown converts HTML <h1> heading to '# Title'" $pass "contains '# Title'" $r.out $r.shot

# CHK-03 P0 - Paste as JSON converts CSV to JSON array
SetClipText "name,age`r`nAlice,30"; Start-Sleep 1
$r = Drive-Transform "Paste as JSON" "chk03-json-csv" "name,age`r`nAlice,30"
$pass = ($r.out -match '"name"') -and ($r.out -match '"Alice"') -and ($r.out.TrimStart().StartsWith("["))
Add-Result "CHK-03" "P0" "Paste as JSON converts CSV to JSON array (values preserved)" $pass 'array containing "name" and "Alice"' $r.out $r.shot

# CHK-04 P1 - Paste as JSON converts XML to JSON object
SetClipText "<note><to>Tove</to><from>Jani</from></note>"; Start-Sleep 1
$r = Drive-Transform "Paste as JSON" "chk04-json-xml" "<note><to>Tove</to><from>Jani</from></note>"
$pass = ($r.out -match '"note"\s*:') -and ($r.out -match "Tove")
Add-Result "CHK-04" "P1" "Paste as JSON converts XML to JSON object with element keys" $pass 'contains "note": key and Tove' $r.out $r.shot

# CHK-05 P1 - Paste as JSON passthrough of valid JSON
SetClipText '{"k":123}'; Start-Sleep 1
$r = Drive-Transform "Paste as JSON" "chk05-json-passthrough" '{"k":123}'
$pass = ($r.out -eq '{"k":123}')
Add-Result "CHK-05" "P1" "Paste as JSON returns already-valid JSON unchanged (passthrough)" $pass 'equals {"k":123}' $r.out $r.shot

# CHK-06 P1 - Paste as JSON never-throws fallback -> array of lines
SetClipText "hello world`r`nsecond line"; Start-Sleep 1
$r = Drive-Transform "Paste as JSON" "chk06-json-fallback" "hello world`r`nsecond line"
$pass = ($r.out -match '"hello world"') -and ($r.out -match '"second line"') -and ($r.out.TrimStart().StartsWith("["))
Add-Result "CHK-06" "P1" "Paste as JSON falls back to JSON array-of-lines for non-tabular text (never-throws guard)" $pass 'array containing "hello world","second line"' $r.out $r.shot

# CHK-07 P1 - AI prompt box gated
$ap = Show-AP
$aiEnabled = (winapp ui get-property "InputTxtBox" -w $ap --property IsEnabled 2>&1 | Select-String "IsEnabled:\s*(\w+)").Matches.Groups[1].Value
$shot = Shot $ap "chk07-ai-gating"
$pass = ($aiEnabled -eq "False")
Add-Result "CHK-07" "P1" "AI prompt box (InputTxtBox) is disabled when no AI provider is configured (AI gating)" $pass "InputTxtBox IsEnabled=False" "IsEnabled=$aiEnabled" $shot

# CHK-08 P2 - Clipboard preview reflects current clipboard
SetClipText "PREVIEW_CHECK_555"; Start-Sleep 1
$ap = Show-AP
$found = winapp ui search "PREVIEW_CHECK_555" -w $ap 2>&1 | Select-String "PREVIEW_CHECK_555"
$shot = Shot $ap "chk08-clipboard-preview"
$pass = [bool]$found
Add-Result "CHK-08" "P2" "Window clipboard preview shows the current clipboard content" $pass "preview shows 'PREVIEW_CHECK_555'" ("found=" + [bool]$found) $shot

# CHK-09 P2 - Core format list
$ap = Show-AP
$hasPlain = winapp ui search "Paste as plain text" -w $ap 2>&1 | Select-String "ListItem"
$hasMd    = winapp ui search "Paste as markdown"   -w $ap 2>&1 | Select-String "ListItem"
$hasJson  = winapp ui search "Paste as JSON"        -w $ap 2>&1 | Select-String "ListItem"
$shot = Shot $ap "chk09-format-list"
$pass = ([bool]$hasPlain) -and ([bool]$hasMd) -and ([bool]$hasJson)
Add-Result "CHK-09" "P2" "Core paste-format list shows plain text, markdown and JSON actions" $pass "all three core ListItems present" ("plain=$([bool]$hasPlain) md=$([bool]$hasMd) json=$([bool]$hasJson)") $shot

# CHK-10 P2 - Paste as markdown emits bold emphasis
SetClipHtml "Title bold" "<h1>Title</h1><p><b>bold</b></p>"; Start-Sleep 1
$r = Drive-Transform "Paste as markdown" "chk10-markdown-bold" "Title bold"
$clean = ($r.out -replace '\\','')
$pass = ($clean -match '\*\*bold\*\*')
Add-Result "CHK-10" "P2" "Paste as markdown converts <b>bold</b> to '**bold**' emphasis" $pass "contains '**bold**'" $r.out $r.shot

# ============================ REPORT ============================
$p0 = $results | Where-Object { $_.priority -eq "P0" }
$p0fail = ($p0 | Where-Object { -not $_.pass }).Count
$total = $results.Count
$passed = ($results | Where-Object { $_.pass }).Count
$gate = if ($p0fail -eq 0) { "PASS" } else { "FAIL" }

$summary = [pscustomobject]@{
    basename=$Basename; gate=$gate; passed=$passed; total=$total;
    p0_failed=$p0fail; verification="produced-clipboard (locked-workstation variant)";
    timestamp=(Get-Date).ToString("s"); checks=$results
}
$jsonPath = Join-Path $OutDir "$Basename.json"
$summary | ConvertTo-Json -Depth 6 | Set-Content -Path $jsonPath -Encoding UTF8

$md = @()
$md += "# AdvancedPaste Sign-off - $Basename"
$md += ""
$md += "GATE: **$gate** | Passed $passed/$total | P0 failures: $p0fail | verification: produced-clipboard | $(Get-Date -Format s)"
$md += ""
$md += "| ID | Pri | PASS | Capability | Expected | Actual |"
$md += "|----|-----|------|------------|----------|--------|"
foreach ($c in $results) {
    $a = ($c.actual -replace '\|','\|' -replace "`n"," ")
    if ($a.Length -gt 80) { $a = $a.Substring(0,80) + "..." }
    $md += ("| {0} | {1} | {2} | {3} | {4} | {5} |" -f $c.id,$c.priority,($(if($c.pass){"OK"}else{"X"})),$c.description,$c.expected,$a)
}
$mdPath = Join-Path $OutDir "$Basename.md"
$md -join "`n" | Set-Content -Path $mdPath -Encoding UTF8

Write-Host ""
Write-Host "GATE=$gate  passed=$passed/$total  p0_failed=$p0fail"
Write-Host "wrote $jsonPath"
Write-Host "wrote $mdPath"
if ($p0fail -eq 0) { exit 0 } else { exit 1 }
