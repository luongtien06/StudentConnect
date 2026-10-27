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

# Edit BenchmarkP2DeepDiagnostic.cs to run 5 samples for fast verification
with open(r"scratch\BenchmarkP2DeepDiagnostic.cs", "r", encoding="utf-8") as f:
    diag_code = f.read()

diag_code = diag_code.replace("const int SAMPLES = 30;", "const int SAMPLES = 5;")
with open(r"scratch\BenchmarkP2DeepDiagnostic.cs", "w", encoding="utf-8") as f:
    f.write(diag_code)

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
    print("Running 5-sample verification...")
    run_res = subprocess.run(r"scratch\BenchmarkP2DeepDiagnostic.exe", shell=True, capture_output=True, text=True)
    print(run_res.stdout)
    if run_res.stderr:
        print("ERRORS:\n", run_res.stderr)
else:
    print("Compilation failed!")
