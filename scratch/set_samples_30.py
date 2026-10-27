import os

path = r"scratch\BenchmarkP2DeepDiagnostic.cs"
with open(path, "r", encoding="utf-8") as f:
    content = f.read()

content = content.replace("const int SAMPLES = 5;", "const int SAMPLES = 30;")

with open(path, "w", encoding="utf-8") as f:
    f.write(content)

print("Set SAMPLES = 30 in BenchmarkP2DeepDiagnostic.cs!")
