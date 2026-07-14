param(
    [string]$TargetLanguage = "Simplified Chinese",
    [string]$Model = $env:OPENAI_MODEL,
    [string]$BaseUrl = $env:OPENAI_BASE_URL,
    [switch]$SelectedOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Model)) {
    $Model = "gpt-4o-mini"
}

if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
    $BaseUrl = "https://api.openai.com/v1"
}

if ([string]::IsNullOrWhiteSpace($env:OPENAI_API_KEY)) {
    throw "OPENAI_API_KEY is not set. Set it first, for example: `$env:OPENAI_API_KEY='sk-...'"
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$signature = @"
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class HotKeyWindow : NativeWindow, IDisposable {
    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public event EventHandler HotKeyPressed;
    private const int WM_HOTKEY = 0x0312;
    private int id;

    public HotKeyWindow(int hotkeyId, uint modifiers, uint key) {
        id = hotkeyId;
        CreateHandle(new CreateParams());
        RegisterHotKey(this.Handle, id, modifiers, key);
    }

    protected override void WndProc(ref Message m) {
        if (m.Msg == WM_HOTKEY && HotKeyPressed != null) {
            HotKeyPressed(this, EventArgs.Empty);
        }
        base.WndProc(ref m);
    }

    public void Dispose() {
        UnregisterHotKey(this.Handle, id);
        DestroyHandle();
    }
}
"@

Add-Type -TypeDefinition $signature -ReferencedAssemblies System.Windows.Forms

function Write-Log {
    param([string]$Message)
    $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$ts] $Message"
}

function Get-ClipboardTextSafely {
    for ($i = 0; $i -lt 20; $i++) {
        try {
            return [System.Windows.Forms.Clipboard]::GetText()
        } catch {
            Start-Sleep -Milliseconds 100
        }
    }
    throw "Could not read clipboard."
}

function Set-ClipboardTextSafely {
    param([string]$Text)
    for ($i = 0; $i -lt 20; $i++) {
        try {
            [System.Windows.Forms.Clipboard]::SetText($Text)
            return
        } catch {
            Start-Sleep -Milliseconds 100
        }
    }
    throw "Could not write clipboard."
}

function Invoke-ChatTranslation {
    param(
        [string]$Text,
        [string]$Language
    )

    $endpoint = $BaseUrl.TrimEnd("/") + "/chat/completions"
    $headers = @{
        "Authorization" = "Bearer $env:OPENAI_API_KEY"
        "Content-Type"  = "application/json; charset=utf-8"
    }

    $body = @{
        model = $Model
        temperature = 0
        messages = @(
            @{
                role = "system"
                content = "You are a precise software localization translator. Translate only user-visible text to $Language. Preserve resource script syntax, accelerator markers (&), placeholders, escape sequences, numbers, IDs, comments, braces, quotes, and line structure. Return only the translated text."
            },
            @{
                role = "user"
                content = $Text
            }
        )
    } | ConvertTo-Json -Depth 8

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
    $response = Invoke-RestMethod -Method Post -Uri $endpoint -Headers $headers -Body $bytes
    return [string]$response.choices[0].message.content
}

function Invoke-TranslateFocusedEditor {
    param([bool]$OnlySelection)

    Write-Log "Reading focused editor through clipboard..."
    $originalClipboard = $null
    try {
        $originalClipboard = Get-ClipboardTextSafely
    } catch {
        $originalClipboard = $null
    }

    if (-not $OnlySelection) {
        [System.Windows.Forms.SendKeys]::SendWait("^a")
        Start-Sleep -Milliseconds 120
    }

    [System.Windows.Forms.SendKeys]::SendWait("^c")
    Start-Sleep -Milliseconds 250
    $source = Get-ClipboardTextSafely

    if ([string]::IsNullOrWhiteSpace($source)) {
        Write-Log "No text was copied. Put the caret in Resource Hacker's editor view and try again."
        return
    }

    Write-Log "Translating $($source.Length) characters with model '$Model'..."
    $translated = Invoke-ChatTranslation -Text $source -Language $TargetLanguage

    if ([string]::IsNullOrWhiteSpace($translated)) {
        Write-Log "Translation returned empty text; editor was not modified."
        return
    }

    Set-ClipboardTextSafely -Text $translated
    Start-Sleep -Milliseconds 120
    [System.Windows.Forms.SendKeys]::SendWait("^v")
    Write-Log "Translated text pasted back into focused editor."

    if ($null -ne $originalClipboard) {
        Start-Sleep -Milliseconds 150
        Set-ClipboardTextSafely -Text $originalClipboard
    }
}

$MOD_ALT = 0x0001
$MOD_CONTROL = 0x0002
$MOD_SHIFT = 0x0004
$VK_T = 0x54

$allHotkey = [HotKeyWindow]::new(1001, ($MOD_CONTROL -bor $MOD_ALT), $VK_T)
$selectionHotkey = [HotKeyWindow]::new(1002, ($MOD_CONTROL -bor $MOD_ALT -bor $MOD_SHIFT), $VK_T)

$allHotkey.add_HotKeyPressed({
    try {
        Invoke-TranslateFocusedEditor -OnlySelection:$false
    } catch {
        Write-Log "ERROR: $($_.Exception.Message)"
    }
})

$selectionHotkey.add_HotKeyPressed({
    try {
        Invoke-TranslateFocusedEditor -OnlySelection:$true
    } catch {
        Write-Log "ERROR: $($_.Exception.Message)"
    }
})

Write-Host ""
Write-Host "Resource Hacker AI Translator Bridge"
Write-Host "Model: $Model"
Write-Host "Base URL: $BaseUrl"
Write-Host "Target language: $TargetLanguage"
Write-Host ""
Write-Host "Hotkeys:"
Write-Host "  Ctrl+Alt+T       Translate the full focused editor view (Ctrl+A, Ctrl+C, translate, Ctrl+V)"
Write-Host "  Ctrl+Alt+Shift+T Translate current selection only (Ctrl+C, translate, Ctrl+V)"
Write-Host ""
Write-Host "Keep this PowerShell window open. Press Ctrl+C here to stop."
Write-Host ""

try {
    [System.Windows.Forms.Application]::Run()
} finally {
    $allHotkey.Dispose()
    $selectionHotkey.Dispose()
}
