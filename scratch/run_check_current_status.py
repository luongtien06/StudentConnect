import subprocess

cmd_compile = (
    r'"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe" '
    r'/out:scratch\check_current_status.exe '
    r'scratch\check_current_status.cs'
)
subprocess.run(cmd_compile, shell=True, check=True)
run_res = subprocess.run(r"scratch\check_current_status.exe", shell=True, capture_output=True, text=True)
print(run_res.stdout)
print(run_res.stderr)
