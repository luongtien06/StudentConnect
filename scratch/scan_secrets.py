import os, re

suspicious_patterns = [
    (re.compile(r'(?i)(api[_-]?key|secret[_-]?key|private[_-]?key)\s*[:=]\s*[\'"][A-Za-z0-9_\-]{16,}[\'"]'), "API_KEY"),
    (re.compile(r'AIzaSy[A-Za-z0-9_\-]{33}'), "GOOGLE_API_KEY"),
]

ignore_exts = {'.dll', '.exe', '.pdb', '.png', '.jpg', '.jpeg', '.gif', '.svg', '.woff', '.woff2', '.ttf', '.eot', '.ico', '.zip'}
ignore_dirs = {'.git', '.vs', 'bin', 'obj', 'packages', 'brain'}

matches = []
for root, dirs, files in os.walk('.'):
    dirs[:] = [d for d in dirs if d not in ignore_dirs and not d.startswith('.')]
    for file in files:
        ext = os.path.splitext(file)[1].lower()
        if ext in ignore_exts:
            continue
        filepath = os.path.join(root, file)
        try:
            with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
                for idx, line in enumerate(f, 1):
                    for pat, name in suspicious_patterns:
                        m = pat.search(line)
                        if m:
                            matched_text = m.group(0)
                            # Mask middle of secret
                            if len(matched_text) > 10:
                                masked = matched_text[:4] + '***' + matched_text[-4:]
                            else:
                                masked = '***'
                            matches.append((name, filepath, idx, masked))
        except Exception:
            pass

print(f"Total potential secret hits: {len(matches)}")
for name, path, line_no, masked in matches:
    print(f"[{name}] {path}:{line_no} -> {masked}")
