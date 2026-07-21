# Sets the Windows clipboard for AdvancedPaste sign-off inputs.
#   -Mode html : sets BOTH CF_HTML (HTML Format) and unicode text
#   -Mode text : sets plain unicode text (used for CSV / XML / JSON inputs)
# Text is read from -Value or, if -FromFile is given, from that file (UTF-8).
param(
    [ValidateSet('html','text')] [string]$Mode = 'text',
    [string]$Value = '',
    [string]$Html = '',
    [string]$FromFile = '',
    [string]$HtmlFromFile = ''
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

if ($FromFile -ne '')     { $Value = [System.IO.File]::ReadAllText($FromFile) }
if ($HtmlFromFile -ne '') { $Html  = [System.IO.File]::ReadAllText($HtmlFromFile) }

$do = New-Object System.Windows.Forms.DataObject
if ($Mode -eq 'html') {
    if ($Html -eq '') { $Html = $Value }
    # WinForms wraps the fragment into a valid CF_HTML descriptor automatically.
    $do.SetText($Html, [System.Windows.Forms.TextDataFormat]::Html)
    $do.SetText($Value, [System.Windows.Forms.TextDataFormat]::UnicodeText)
} else {
    $do.SetText($Value, [System.Windows.Forms.TextDataFormat]::UnicodeText)
}
[System.Windows.Forms.Clipboard]::SetDataObject($do, $true)
Write-Host "clipboard set mode=$Mode len=$($Value.Length)"
