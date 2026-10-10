"""Check public repository paths, secrets, Markdown links and release assets."""
from pathlib import Path
import re
import zipfile

ROOT = Path(__file__).resolve().parents[1]
SKIP = {'.git', 'build', '__pycache__', '.vs', '.idea', '.vscode'}
errors = []
files = []
for path in ROOT.rglob('*'):
    relative = path.relative_to(ROOT)
    if any(part in SKIP for part in relative.parts):
        continue
    if not relative.as_posix().isascii():
        errors.append('Non-English path: ' + str(relative))
    if path.is_file():
        files.append(path)
        if path.suffix.lower() in {'.jks', '.keystore', '.pyc', '.log', '.tmp', '.idsig'} or path.name in {'password.txt', '.env', 'local.properties'}:
            errors.append('Private/generated file: ' + str(relative))
        if path.suffix.lower() == '.txt':
            errors.append('Loose TXT file: ' + str(relative))
for path in files:
    if path.suffix == '.md':
        text = path.read_text(encoding='utf-8-sig')
        if re.search(r'\?{3,}', text):
            errors.append('Damaged text encoding: ' + str(path.relative_to(ROOT)))
        for link in re.findall(r'\[[^\]]*\]\(([^)]+)\)', text):
            if '://' in link or link.startswith('#'):
                continue
            target = link.split('#')[0]
            if target and not (path.parent / target).exists():
                errors.append('Broken local link: ' + str(path.relative_to(ROOT)) + ': ' + target)
expected = {'releases/Windows/AI-Image-Editing-Tools.exe', 'releases/Android/AI-Image-Editing-Tools.apk'}
actual = {p.relative_to(ROOT).as_posix() for p in files if p.suffix.lower() in {'.exe', '.apk'}}
if actual != expected:
    errors.append('Expected exactly the two named release packages')
apk = ROOT / 'releases/Android/AI-Image-Editing-Tools.apk'
if apk.exists():
    with zipfile.ZipFile(apk) as archive:
        if archive.testzip() is not None:
            errors.append('Corrupt APK ZIP entry')
        assets = {n for n in archive.namelist() if n.startswith('assets/') and not n.endswith('/')}
        if assets != {'assets/NOTICE.md'}:
            errors.append('Unexpected release assets: ' + repr(assets))
        notice = archive.read('assets/NOTICE.md').decode('utf-8')
        if 'MIT License' not in notice or 'BSD 2-Clause License' not in notice or 'Apache License' not in notice:
            errors.append('Missing embedded licenses')
for required in ['LICENSE', 'README.md', 'README.en.md', 'THIRD_PARTY_NOTICES.md', '.gitignore', '.gitattributes']:
    if not (ROOT / required).is_file():
        errors.append('Missing repository file: ' + required)
if errors:
    raise SystemExit('\n'.join(errors))
print('PASS: ASCII paths, release packages, embedded licenses, Markdown links and private-file checks')
print('Public files:', len(files), 'Total bytes:', sum(p.stat().st_size for p in files))
