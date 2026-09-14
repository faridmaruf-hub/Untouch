Get-PnpDevice | Where-Object {
    $_.FriendlyName -match 'touch|precision|synaptics|elan|glidepoint'
} | Select-Object FriendlyName, InstanceId, Class, Status | Format-List
