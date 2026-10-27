# Script thiết lập Windows Task Scheduler để Worker tự chạy ngầm mỗi khi khởi động máy
# Chạy với quyền Administrator

$taskName = "TDMU_StudentConnect_AgentWorker"
$exePath = "E:\LTWEB\StudentConnect\StudentConnect.Worker\bin\Debug\StudentConnect.Worker.exe"

if (!(Test-Path $exePath)) {
    Write-Error "Khong tim thay file StudentConnect.Worker.exe tai $exePath. Vui long build project truoc."
    exit 1
}

$action = New-ScheduledTaskAction -Execute $exePath
$trigger = New-ScheduledTaskTrigger -AtStartup
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)

try {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
    Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Description "TDMU StudentConnect AI Agent Background Worker"
    Write-Host "Da cai dat Windows Task '$taskName' thanh cong!" -ForegroundColor Green
    Write-Host "Worker se tu dong chay ngam khi khoi dong Windows." -ForegroundColor Cyan
} catch {
    Write-Error "Loi khi cai dat Scheduled Task: $_"
}
