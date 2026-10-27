import subprocess
import os
import glob
import shutil

os.chdir(r"e:\LTWEB\StudentConnect")

# Ensure scratch directory and config
os.makedirs("scratch", exist_ok=True)
for dll in glob.glob(r"bin\*.dll"):
    shutil.copy(dll, "scratch")

shutil.copyfile("Web.config", r"scratch\BenchmarkP2DeepDiagnostic.exe.config")
os.makedirs(r"scratch\Config", exist_ok=True)
shutil.copyfile(r"Config\ConnectionStrings.config", r"scratch\Config\ConnectionStrings.config")

cmd_compile = (
    r'"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe" '
    r'/out:scratch\BenchmarkP2DeepDiagnostic.exe '
    r'/r:bin\StudentConnect.dll '
    r'/r:bin\EntityFramework.dll '
    r'/r:bin\EntityFramework.SqlServer.dll '
    r'/r:bin\System.Web.Mvc.dll '
    r'scratch\BenchmarkP2DeepDiagnostic.cs'
)

res = subprocess.run(cmd_compile, shell=True, capture_output=True, text=True)
print("Compile output:", res.stdout)
print("Compile errors:", res.stderr)

if res.returncode == 0:
    print("Starting BenchmarkP2DeepDiagnostic (30 samples per scenario)...")
    run_res = subprocess.run(r"scratch\BenchmarkP2DeepDiagnostic.exe", shell=True, capture_output=True, text=True)
    print(run_res.stdout)
    if run_res.stderr:
        print("ERRORS:\n", run_res.stderr)
    print("Exit code:", run_res.returncode)
else:
    print("Compilation failed!")
