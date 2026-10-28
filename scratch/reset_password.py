import hashlib, os, base64, subprocess

salt = os.urandom(16)
key = hashlib.pbkdf2_hmac('sha256', b'123456', salt, 10000, 32)
salt_b64 = base64.b64encode(salt).decode()
key_b64 = base64.b64encode(key).decode()
hash_val = f'PBKDF2$10000${salt_b64}${key_b64}'
print('New hash:', hash_val)

cmd = f'sqlcmd -S "localhost\\MSSQLSERVER01" -d "TDMUEcoSystem" -Q "UPDATE dbo.Users SET PasswordHash = \'{hash_val}\' WHERE Email = \'testuser01@student.tdmu.edu.vn\'"'
subprocess.check_call(cmd, shell=True)
print('Updated password for testuser01 to 123456')
