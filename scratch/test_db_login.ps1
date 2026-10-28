Add-Type -Path 'E:\LTWEB\StudentConnect\bin\StudentConnect.dll'
Add-Type -Path 'E:\LTWEB\StudentConnect\bin\EntityFramework.dll'
$db = New-Object StudentConnect.Models.TDMUEcoSystemEntities
$email = 'testuser01@student.tdmu.edu.vn'
$norm = [StudentConnect.Helpers.StudentEmailValidator]::NormalizeEmail($email)
$u = $db.Users | Where-Object { $_.Email.Trim().ToLower() -eq $norm } | Select-Object -First 1
Write-Output ("User found: " + ($u -ne $null))
if ($u) {
    Write-Output ("Email: " + $u.Email)
    Write-Output ("Hash: " + $u.PasswordHash)
    Write-Output ("Verify: " + [StudentConnect.Helpers.PasswordHelper]::VerifyPassword('123456', $u.PasswordHash))
}
