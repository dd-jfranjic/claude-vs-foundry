# Hard guard for LOCKED files: a PreToolUse hook that blocks Edit/Write/MultiEdit on any
# file carrying the "LOCKED FOR AI EDIT" marker — in EVERY permission mode, Bypass included.
# stdin = JSON {tool_name, tool_input:{file_path,...}}; exit 2 = block (stderr shown to Claude).
# Fail-open by design: a failure of the hook itself must never brick the session.
$ErrorActionPreference = "Stop"
try {
    $raw = [Console]::In.ReadToEnd()
    if (-not $raw) { exit 0 }
    $inp = $raw | ConvertFrom-Json
    $fp = $inp.tool_input.file_path
    if ($fp -and (Test-Path -LiteralPath $fp)) {
        if (Select-String -LiteralPath $fp -Pattern "LOCKED FOR AI EDIT" -SimpleMatch -Quiet) {
            [Console]::Error.WriteLine("BLOCKED (hook): '$fp' carries the 'LOCKED FOR AI EDIT' marker - this file is never edited, in any mode. Propose the change to the user and let them decide.")
            exit 2
        }
    }
    exit 0
} catch {
    exit 0
}
