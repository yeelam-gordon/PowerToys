<#
AdvancedPaste winappcli-driven sign-off harness.

Drives the REAL Advanced Paste window (summoned via the named-pipe ShowUI
controller) end to end: sets the clipboard, focuses a target Notepad, invokes a
paste-format action through winappcli, lets AdvancedPaste paste into Notepad via
its own SendInput(Ctrl+V), then reads the pasted output back with
`winapp ui get-value` and screenshots both the AP window and the result.

Prerequisites (started by the caller):
  * ap_controller.ps1 running (owns the ShowUI pipe; re-shows on show.trigger)
  * a Notepad window open (the paste target)

Emits <Basename>.json and <Basename>.md. Exit 0 = all P0 pass, 1 = a P0 failed.
#>
param(
    [string]$Basename = "run",
    [string]$OutDir   = "$PSScriptRoot",
    [string]$WorkDir  = "$PSScriptRoot",
    [string]$ShotDir  = "",
    [switch]$NoShots
)
$ErrorActionPreference = "Continue"
. (Join-Path $WorkDir "input_helpers.ps1")
if ($ShotDir -eq "") { $ShotDir = Join-Path $OutDir "screenshots\$Basename" }
New-Item -ItemType Directory -Force $ShotDir | Out-Null

$trigger = Join-Path $WorkDir "show.trigger"
$np = Get-NotepadHwnd
if (-not $np) { Start-Process notepad; Start-Sleep 3; $np = Get-NotepadHwnd }

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
function ShotWin($hwnd, $title, $name) {
    if ($NoShots) { return "" }
    $p = Join-Path $ShotDir "$name.png"
    winapp ui screenshot $title -w $hwnd -o $p --focus 2>&1 | Out-Null
    return $p
}
function Read-Notepad {
    $v = winapp ui get-value "Text editor" -w $np 2>&1 | Where-Object { $_ -notmatch '^Auto-selected' } | Select-Object -First 50
    ($v -join "`n").Trim()
}

# --- Clipboard setup helpers (pass content via files to avoid arg quoting issues) ---
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

# Drive a transform: clipboard already set. Clears notepad, shows AP, screenshots,
# invokes the format, polls notepad until output looks final, returns pasted text.
function Drive-Transform($fmtName, $shotName) {
    for ($attempt=1; $attempt -le 3; $attempt++) {
        Clear-Notepad $np | Out-Null
        $ap = Show-AP
        if (-not $ap) { continue }
        $shot = Shot $ap $shotName
        winapp ui invoke $fmtName -w $ap 2>&1 | Out-Null
        $out = ""
        for ($i=0; $i -lt 12; $i++) {
            Start-Sleep -Milliseconds 700
            $out = Read-Notepad
            if ($out -and $out -ne "Text editor") { break }
        }
        Start-Sleep -Milliseconds 500
        $out = Read-Notepad
        if ($out -and $out -ne "Text editor") {
            $rshot = ShotWin $np "Text editor" "$shotName-result"
            return @{ ap=$ap; shot=$shot; rshot=$rshot; out=$out }
        }
        Write-Host "  (retry ${attempt}: empty paste for $fmtName)"
    }
    $rshot = ShotWin $np "Text editor" "$shotName-result"
    return @{ ap=$ap; shot=$shot; rshot=$rshot; out=$out }
}

$results = @()
function Add-Result($id,$pri,$desc,$pass,$expected,$actual,$shot,$rshot){
    $script:results += [pscustomobject]@{
        id=$id; priority=$pri; description=$desc; pass=[bool]$pass;
        expected=$expected; actual=$actual; screenshot=$shot; result_screenshot=$rshot
    }
    $tag = if($pass){"PASS"}else{"FAIL"}
    Write-Host ("[{0}] {1} {2} :: {3}" -f $tag,$id,$pri,$desc)
}

# ============================ CHECKS ============================

# CHK-01 P0 - Paste as plain text strips HTML formatting
SetClipHtml "BoldHello" "<b>Bold</b>Hello"; Start-Sleep 1
$r = Drive-Transform "Paste as plain text" "chk01-plaintext"
$pass = ($r.out -eq "BoldHello")
Add-Result "CHK-01" "P0" "Paste as plain text returns clipboard text, strips HTML tags" $pass 'equals "BoldHello"' $r.out $r.shot $r.rshot

# CHK-02 P0 - Paste as markdown converts HTML heading
SetClipHtml "Title bold" "<h1>Title</h1><p><b>bold</b></p>"; Start-Sleep 1
$r = Drive-Transform "Paste as markdown" "chk02-markdown-heading"
$mdOut = $r.out
$pass = ($mdOut -match "#\s*Title")
Add-Result "CHK-02" "P0" "Paste as markdown converts HTML <h1> heading to '# Title'" $pass "contains '# Title'" $mdOut $r.shot $r.rshot

# CHK-03 P0 - Paste as JSON converts CSV to JSON array-of-arrays
SetClipText "name,age`r`nAlice,30"; Start-Sleep 1
$r = Drive-Transform "Paste as JSON" "chk03-json-csv"
$pass = ($r.out -match '"name"') -and ($r.out -match '"Alice"') -and ($r.out.TrimStart().StartsWith("["))
Add-Result "CHK-03" "P0" "Paste as JSON converts CSV to JSON array (values preserved)" $pass 'array containing "name" and "Alice"' $r.out $r.shot $r.rshot

# CHK-04 P1 - Paste as JSON converts XML to JSON object
SetClipText "<note><to>Tove</to><from>Jani</from></note>"; Start-Sleep 1
$r = Drive-Transform "Paste as JSON" "chk04-json-xml"
$pass = ($r.out -match '"note"\s*:') -and ($r.out -match "Tove")
Add-Result "CHK-04" "P1" "Paste as JSON converts XML to JSON object with element keys" $pass "contains '\"note\":' object and 'Tove'" $r.out $r.shot $r.rshot

# CHK-05 P1 - Paste as JSON passes through already-valid JSON unchanged
SetClipText '{"k":123}'; Start-Sleep 1
$r = Drive-Transform "Paste as JSON" "chk05-json-passthrough"
$pass = ($r.out -eq '{"k":123}')
Add-Result "CHK-05" "P1" "Paste as JSON returns already-valid JSON unchanged (passthrough)" $pass 'equals {"k":123}' $r.out $r.shot $r.rshot

# CHK-06 P1 - Paste as JSON never-throws guard: plain multiline text -> array of lines
SetClipText "hello world`r`nsecond line"; Start-Sleep 1
$r = Drive-Transform "Paste as JSON" "chk06-json-fallback"
$pass = ($r.out -match '"hello world"') -and ($r.out -match '"second line"') -and ($r.out.TrimStart().StartsWith("["))
Add-Result "CHK-06" "P1" "Paste as JSON falls back to JSON array-of-lines for non-tabular text (never-throws guard)" $pass 'array containing "hello world","second line"' $r.out $r.shot $r.rshot

# CHK-07 P1 - AI prompt box is gated (disabled) when no AI provider/GPO configured
$ap = Show-AP
$aiEnabled = (winapp ui get-property "InputTxtBox" -w $ap --property IsEnabled 2>&1 | Select-String "IsEnabled:\s*(\w+)").Matches.Groups[1].Value
$shot = Shot $ap "chk07-ai-gating"
$pass = ($aiEnabled -eq "False")
Add-Result "CHK-07" "P1" "AI prompt box (InputTxtBox) is disabled when no AI provider is configured (AI gating)" $pass "InputTxtBox IsEnabled=False" "IsEnabled=$aiEnabled" $shot ""

# CHK-08 P2 - Window clipboard preview reflects the current clipboard text
SetClipText "PREVIEW_CHECK_555"; Start-Sleep 1
$ap = Show-AP
$found = winapp ui search "PREVIEW_CHECK_555" -w $ap 2>&1 | Select-String "PREVIEW_CHECK_555"
$shot = Shot $ap "chk08-clipboard-preview"
$pass = [bool]$found
Add-Result "CHK-08" "P2" "Window clipboard preview shows the current clipboard content" $pass "preview shows 'PREVIEW_CHECK_555'" ("found=" + [bool]$found) $shot ""

# CHK-09 P2 - Core paste-format list exposes plain text, markdown and JSON actions
$ap = Show-AP
$hasPlain = winapp ui search "Paste as plain text" -w $ap 2>&1 | Select-String "ListItem"
$hasMd    = winapp ui search "Paste as markdown"   -w $ap 2>&1 | Select-String "ListItem"
$hasJson  = winapp ui search "Paste as JSON"        -w $ap 2>&1 | Select-String "ListItem"
$shot = Shot $ap "chk09-format-list"
$pass = ([bool]$hasPlain) -and ([bool]$hasMd) -and ([bool]$hasJson)
Add-Result "CHK-09" "P2" "Core paste-format list shows plain text, markdown and JSON actions" $pass "all three core ListItems present" ("plain=$([bool]$hasPlain) md=$([bool]$hasMd) json=$([bool]$hasJson)") $shot ""

# CHK-10 P2 - Paste as markdown emits bold emphasis (**)
SetClipHtml "Title bold" "<h1>Title</h1><p><b>bold</b></p>"; Start-Sleep 1
$r = Drive-Transform "Paste as markdown" "chk10-markdown-bold"
$clean = ($r.out -replace '\\','')
$pass = ($clean -match '\*\*bold\*\*')
Add-Result "CHK-10" "P2" "Paste as markdown converts <b>bold</b> to '**bold**' emphasis" $pass "contains '**bold**'" $r.out $r.shot $r.rshot

# ============================ REPORT ============================
$p0 = $results | Where-Object { $_.priority -eq "P0" }
$p0fail = ($p0 | Where-Object { -not $_.pass }).Count
$total = $results.Count
$passed = ($results | Where-Object { $_.pass }).Count
$gate = if ($p0fail -eq 0) { "PASS" } else { "FAIL" }

$summary = [pscustomobject]@{
    basename=$Basename; gate=$gate; passed=$passed; total=$total;
    p0_failed=$p0fail; timestamp=(Get-Date).ToString("s"); checks=$results
}
$jsonPath = Join-Path $OutDir "$Basename.json"
$summary | ConvertTo-Json -Depth 6 | Set-Content -Path $jsonPath -Encoding UTF8

$md = @()
$md += "# AdvancedPaste Sign-off - $Basename"
$md += ""
$md += "GATE: **$gate** | Passed $passed/$total | P0 failures: $p0fail | $(Get-Date -Format s)"
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
