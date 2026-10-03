"""Independent permutation/container validation, standard library only."""
from pathlib import Path
import hashlib, importlib.util, json, math, random, struct, subprocess, uuid, zlib
from verify_apng import decode_rgba

ROOT=Path(__file__).resolve().parents[2]
from verify_apng import EXE
OUT=ROOT/'build/verification/scramble-core'/uuid.uuid4().hex
OUT.mkdir(parents=True)
spec=importlib.util.spec_from_file_location('gilbert',Path(__file__).with_name('gilbert2d.py'))
gilbert=importlib.util.module_from_spec(spec);spec.loader.exec_module(gilbert)

def chunk(t,p):return struct.pack('>I',len(p))+t+p+struct.pack('>I',zlib.crc32(t+p))
def png(w,h,pixels):
    rows=b''.join(b'\0'+pixels[y*w*4:(y+1)*w*4] for y in range(h))
    return b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))+chunk(b'tEXt',b'workflow\0{"nodes":["original metadata"]}')+chunk(b'IDAT',zlib.compress(rows))+chunk(b'IEND',b'')
def decode(path):
    data=Path(path).read_bytes();p=8;cs=[]
    assert data[:8]==b'\x89PNG\r\n\x1a\n'
    while p<len(data):
        n=struct.unpack_from('>I',data,p)[0];t=data[p+4:p+8];payload=data[p+8:p+8+n]
        assert zlib.crc32(t+payload)==struct.unpack_from('>I',data,p+8+n)[0]
        cs.append((t,payload));p+=n+12
        if t==b'IEND':break
    return decode_rgba(cs[0][1],b''.join(v for t,v in cs if t==b'IDAT'))
def call(*args,success=True):
    p=subprocess.run([str(EXE),*map(str,args)],capture_output=True,timeout=90)
    assert (p.returncode==0)==success,(args,p.returncode,p.stderr)
def transform(pixels,w,h,reverse=False):
    curve=[x+y*w for x,y in gilbert.gilbert2d(w,h)]
    assert sorted(curve)==list(range(w*h))
    shift=math.floor((math.sqrt(5)-1)/2*w*h+.5);out=bytearray(len(pixels))
    for i,a in enumerate(curve):
        b=curve[(i+shift)%(w*h)];src,dst=(b,a) if reverse else (a,b)
        out[dst*4:dst*4+4]=pixels[src*4:src*4+4]
    return bytes(out)
def read_record(path):
    b=path.read_bytes();assert b[-8:]==b'AIPTSCR1'
    n=struct.unpack_from('<I',b,len(b)-12)[0];r=json.loads(b[-12-n:-12]);return b,r
def patched(path,record):
    b,r=read_record(path);meta=json.dumps(record,ensure_ascii=False,separators=(',',':')).encode()
    return b[:r['ImageLength']+r['Length']]+meta+struct.pack('<I',len(meta))+b'AIPTSCR1'

checks=[]
for w,h in [(1,1),(1,7),(8,1),(3,5),(4,7),(19,37),(37,19),(31,32),(32,31)]:
    pixels=bytes(c for i in range(w*h) for c in ((i*13)%256,(i*29)%256,(i*67)%256,80+i%176))
    src=OUT/f'{w}x{h}.png';src.write_bytes(png(w,h,pixels));scr=OUT/f'{w}x{h}-scr.png';plain=OUT/f'{w}x{h}-plain.png'
    call('--scramble',src,scr,0,OUT/'report.json');assert decode(scr)==((w,h),transform(pixels,w,h))
    call('--descramble',scr,plain,0,OUT/'report.json');assert decode(plain)==((w,h),pixels)
checks.append('Reference Gilbert/golden-ratio permutation vectors, odd rectangles, one pixel, rows/columns and all RGBA values')

rng=random.Random(53);w,h=257,193;pixels=bytes(c for _ in range(w*h) for c in (*[rng.randrange(256) for _ in range(3)],255))
src=OUT/'中文含工作流.png';src.write_bytes(png(w,h,pixels))
files=[]
for q in [1,37,100]:
    p=OUT/f'quality{q}.jpg';call('--scramble',src,p,q,OUT/'report.json');assert p.read_bytes()[:2]==b'\xff\xd8';files.append(p)
assert files[0].stat().st_size<files[1].stat().st_size<files[2].stat().st_size
for q in [1,53,100,0]:call('--descramble',files[1],OUT/f'decode{q}.{"png" if q==0 else "jpg"}',q,OUT/'report.json')
checks.append('Arbitrary JPEG qualities 1%, 37%, 53%, 100%; actual encoder sizes and separate descramble quality')

for original in [src,files[1],ROOT/'build/verification/double.png',ROOT/'build/verification/tools/animated.gif']:
    if not original.exists():continue
    wrapped=OUT/(original.stem+'-original.png');call('--scramble',original,wrapped,-1,OUT/'report.json')
    b,r=read_record(wrapped);assert r['Length']==original.stat().st_size
    assert b[r['ImageLength']:r['ImageLength']+r['Length']]==original.read_bytes()
    assert hashlib.sha256(b[:r['ImageLength']]).hexdigest()==r['ImageSha256']
    assert hashlib.sha256(original.read_bytes()).hexdigest()==r['Sha256']
    folder=OUT/'restored';call('--scramble-restore',wrapped,folder,OUT/'restore.json');p=Path(json.loads((OUT/'restore.json').read_text(encoding='utf-8-sig'))['Path'])
    assert p.read_bytes()==original.read_bytes() and p.name==original.name
    call('--scramble-restore',wrapped,folder,OUT/'restore.json');p2=Path(json.loads((OUT/'restore.json').read_text(encoding='utf-8-sig'))['Path']);assert p2!=p and p2.read_bytes()==p.read_bytes()
checks.append('Exact original bytes/SHA-256, filenames, PNG workflow metadata, JPEG, APNG and animated GIF; collision-safe restore')

base=OUT/(src.stem+'-original.png');b,r=read_record(base)
bad=OUT/'corrupt.png';changed=bytearray(b);changed[r['ImageLength']+2]^=1;bad.write_bytes(changed)
call('--scramble-restore',bad,OUT/'bad-restore',OUT/'bad.json',success=False)
changed=bytearray(b);changed[44]^=1;bad.write_bytes(changed);call('--scramble-restore',bad,OUT/'bad-restore',OUT/'bad.json',success=False)
for change in [{'Name':'../escape.png'},{'Name':'C:\\escape.png'},{'Length':2**63-1},{'ImageLength':2**63-1},{'Schema':999}]:
    bad.write_bytes(patched(base,dict(r,**change)));call('--scramble-restore',bad,OUT/'bad-restore',OUT/'bad.json',success=False)
for payload in [b[:-7],b[:50],b[:-12]+struct.pack('<I',2**31-1)+b'AIPTSCR1']:
    bad.write_bytes(payload);call('--scramble-restore',bad,OUT/'bad-restore',OUT/'bad.json',success=False)
assert not list((OUT/'bad-restore').iterdir())
call('--scramble-restore',files[1],OUT/'no-original',OUT/'bad.json',success=False)
saved=files[1].read_bytes();call('--scramble',src,files[1],50,OUT/'bad.json',success=False);assert files[1].read_bytes()==saved
assert not list(OUT.rglob('.scramble-*.tmp'))
checks.append('Pixel/payload tampering, truncation, invalid schemas/lengths, path traversal, missing-original rejection, no overwrite or temporary-file residue')
report={'result':'PASS','exe_sha256':hashlib.sha256(EXE.read_bytes()).hexdigest(),'checks':checks,'quality_file_sizes':{p.name:p.stat().st_size for p in files},'fixture_folder':str(OUT)}
(ROOT/'build/verification/obfuscation-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2))
