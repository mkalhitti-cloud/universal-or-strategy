import os
import zipfile

target_dir = r"C:\WSGTA\universal-or-strategy"
zip_files = []

for root, dirs, files in os.walk(target_dir):
    for f in files:
        path = os.path.join(root, f)
        if zipfile.is_zipfile(path):
            zip_files.append(path)

print("Found zip files:")
for z in zip_files:
    print(z)
