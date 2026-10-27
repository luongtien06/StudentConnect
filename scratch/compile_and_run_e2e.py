import subprocess
import os
import glob
import shutil

os.chdir(r"e:\LTWEB\StudentConnect")

# Copy all DLLs from bin to scratch
os.makedirs("scratch", exist_ok=True)
for dll in glob.glob(r"bin\*.dll"):
    shutil.copy(dll, "scratch")

shutil.copyfile("Web.config", r"scratch\RunE2EAcceptanceSuite.exe.config")
os.makedirs(r"scratch\Config", exist_ok=True)
shutil.copyfile(r"Config\ConnectionStrings.config", r"scratch\Config\ConnectionStrings.config")

cmd_compile = (
    r'"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe" '
    r'/out:scratch\RunE2EAcceptanceSuite.exe '
    r'/r:bin\StudentConnect.dll '
    r'/r:bin\EntityFramework.dll '
    r'/r:bin\EntityFramework.SqlServer.dll '
    r'/r:bin\System.Web.Mvc.dll '
    r'scratch\RunE2EAcceptanceSuite.cs'
)

res = subprocess.run(cmd_compile, shell=True, capture_output=True, text=True)
print("Compile output:", res.stdout)
print("Compile errors:", res.stderr)

if res.returncode == 0:
    run_res = subprocess.run(r"scratch\RunE2EAcceptanceSuite.exe", shell=True, capture_output=True, text=True)
    print("Execution output:\n", run_res.stdout)
    print("Execution errors:\n", run_res.stderr)
    print("Exit code:", run_res.returncode)
