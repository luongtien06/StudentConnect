import subprocess

cmd_compile = (
    r'"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe" '
    r'/out:scratch\test_compile_error.exe '
    r'scratch\test_compile_error.cs'
)
subprocess.run(cmd_compile, shell=True, check=True)
run_res = subprocess.run(r"scratch\test_compile_error.exe", shell=True, capture_output=True, text=True)
print(run_res.stdout)
