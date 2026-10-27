import subprocess

cmd_compile = (
    r'"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe" '
    r'/out:scratch\test_perms.exe '
    r'scratch\test_perms.cs'
)
subprocess.run(cmd_compile, shell=True, check=True)
run_res = subprocess.run(r"scratch\test_perms.exe", shell=True, capture_output=True, text=True)
print(run_res.stdout)
