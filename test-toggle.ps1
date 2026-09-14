Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Native {
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);
}
"@

$HWND_BROADCAST = [IntPtr]0xffff
$WM_SETTINGCHANGE = 0x1a
$SMTO_ABORTIFHUNG = 0x2

function Set-TouchpadEnabled([bool]$enabled) {
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\PrecisionTouchPad\Status" -Name "Enabled" -Value ([int]$enabled)
    $result = [UIntPtr]::Zero
    [Native]::SendMessageTimeout($HWND_BROADCAST, $WM_SETTINGCHANGE, [UIntPtr]::Zero, "PrecisionTouchPad", $SMTO_ABORTIFHUNG, 2000, [ref]$result) | Out-Null
}

Write-Host "Disabling touchpad for 5 seconds. Try touching/moving on it NOW..."
Set-TouchpadEnabled $false
Start-Sleep -Seconds 5
Write-Host "Re-enabling touchpad. Try it now..."
Set-TouchpadEnabled $true
Write-Host "Done. Registry value restored to Enabled=1."
