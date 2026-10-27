import subprocess

cmd_compile = (
    r'"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe" '
    r'/out:scratch\print_batch9.exe '
    r'scratch\print_batch9.cs'
)
subprocess.run(cmd_compile, shell=True, check=True)
run_res = subprocess.run(r"scratch\print_batch9.exe", shell=True, capture_output=True, text=True)
print(run_res.stdout)
