Add-Type @"
using System;
using System.Runtime.InteropServices;
public class CfgMgr {
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Auto)]
    public static extern int CM_Locate_DevNodeW(out uint pdnDevInst, string pDeviceID, uint ulFlags);
    [DllImport("cfgmgr32.dll")]
    public static extern int CM_Disable_DevNode(uint dnDevInst, uint ulFlags);
    [DllImport("cfgmgr32.dll")]
    public static extern int CM_Enable_DevNode(uint dnDevInst, uint ulFlags);
}
"@

$instanceId = "HID\ASUF1209&COL02\4&206411F&0&0001"
$devInst = 0
$rc = [CfgMgr]::CM_Locate_DevNodeW([ref]$devInst, $instanceId, 0)
if ($rc -ne 0) { Write-Host "CM_Locate_DevNodeW failed, CR_ code: $rc"; exit 1 }
Write-Host "Located devnode: $devInst"

Write-Host "Disabling now..."
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$rc = [CfgMgr]::CM_Disable_DevNode($devInst, 0)
$sw.Stop()
Write-Host "CM_Disable_DevNode returned $rc in $($sw.ElapsedMilliseconds) ms (this is just the API call time, not settle time -- touch the touchpad NOW)"

Start-Sleep -Seconds 4

Write-Host "Re-enabling now..."
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$rc = [CfgMgr]::CM_Enable_DevNode($devInst, 0)
$sw.Stop()
Write-Host "CM_Enable_DevNode returned $rc in $($sw.ElapsedMilliseconds) ms (touch the touchpad NOW)"
