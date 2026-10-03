"""Independent metadata/container verification using Python's standard library."""
from pathlib import Path
import json
import struct
import subprocess
import tempfile
import zlib
from verify_apng import ART, EXE, chunks, static_image


SIGNATURE = b'\x89PNG\r\n\x1a\n'
COLOR = {b'IHDR', b'PLTE', b'tRNS', b'iCCP', b'gAMA', b'cHRM', b'sRGB', b'sBIT', b'cICP', b'mDCV', b'cLLI', b'bKGD', b'pHYs'}


def chunk(tag, data):
    return struct.pack('>I', len(data)) + tag + data + struct.pack('>I', zlib.crc32(tag+data))


def run(*args):
    result = subprocess.run([str(EXE), *map(str, args)], capture_output=True)
    assert result.returncode == 0, result.stderr.decode('utf-8', errors='replace')


def clean(path, directory):
    before = {p for p in directory.iterdir()} if directory.exists() else set()
    run('--clean', path, directory)
    after = set(directory.iterdir()) - before
    assert len(after) == 1
    return after.pop()


def gif_blocks(data):
    assert data[:6] in (b'GIF89a', b'GIF87a')
    pos = 13 + (3*(1 << ((data[10]&7)+1)) if data[10]&128 else 0)
    result = []
    def subblocks(p):
        out = bytearray()
        while data[p]:
            n = data[p]; out.extend(data[p+1:p+n+1]); p += n+1
        return p+1, bytes(out)
    while pos < len(data):
        start = pos; kind = data[pos]; pos += 1
        if kind == 0x3b:
            assert pos == len(data); return result
        if kind == 0x21:
            label = data[pos]; pos += 1
            pos, payload = subblocks(pos)
            result.append(('ext', label, payload, data[start:pos]))
        elif kind == 0x2c:
            descriptor = data[pos:pos+9]; pos += 9
            if descriptor[8]&128: pos += 3*(1 << ((descriptor[8]&7)+1))
            minimum = data[pos]; pos += 1
            pos, codes = subblocks(pos)
            result.append(('image', descriptor, (minimum, codes), data[start:pos]))
        else:
            raise AssertionError(f'Unexpected GIF block {kind}')
    raise AssertionError('Missing GIF end')


def riff(data):
    assert data[:4] == b'RIFF' and data[8:12] == b'WEBP'
    assert len(data) == struct.unpack_from('<I', data, 4)[0]+8
    pos, out = 12, []
    while pos < len(data):
        tag = data[pos:pos+4]; length = struct.unpack_from('<I', data, pos+4)[0]
        payload = data[pos+8:pos+8+length]
        assert len(payload) == length
        out.append((tag, payload)); pos += 8+length+(length&1)
    assert pos == len(data)
    return out


def riff_chunk(tag, data):
    return tag+struct.pack('<I', len(data))+data+(b'\0' if len(data)&1 else b'')


def exif(orientation):
    return b'II*\0'+struct.pack('<IH', 8, 1)+struct.pack('<HHIHHI', 0x112, 3, 1, orientation, 0, 0)


def main():
    with tempfile.TemporaryDirectory(prefix='tools-check-', dir=ART) as temporary:
        root = Path(temporary)
        run('--tools-test', root)
        out = root / 'clean'
        workflow = json.dumps({'nodes': [{'id': 1, 'type': 'KSampler'}], 'links': []}).encode()
        text_chunks = [chunk(b'tEXt', b'workflow\0'+workflow), chunk(b'tEXt', b'prompt\0{"1":{"class_type":"KSampler"}}'),
                       chunk(b'zTXt', b'parameters\0\0'+zlib.compress(b'secret prompt')),
                       chunk(b'iTXt', b'workflow\0\x01\x00\0\0'+zlib.compress(workflow)),
                       chunk(b'comf', b'workflow\0'+workflow), chunk(b'eXIf', exif(1)+b'secret workflow'),
                       chunk(b'tIME', b'\x07\xe8\x01\x01\x01\x01\x01'), chunk(b'cuSt', b'private note')]
        profiles = [chunk(b'gAMA', struct.pack('>I', 45455)), chunk(b'cHRM', struct.pack('>8I',31270,32900,64000,33000,30000,60000,15000,6000)),
                    chunk(b'iCCP', b'Profile\0\0'+zlib.compress(b'opaque unchanged ICC fixture')),
                    chunk(b'cICP', bytes([1,13,0,1])), chunk(b'mDCV', bytes(24)), chunk(b'cLLI', bytes(8))]
        source = root / 'comfy.png'
        original = (root/'frame0.png').read_bytes()
        source.write_bytes(original[:33]+b''.join(text_chunks+profiles)+original[33:])
        result = clean(source, out)
        before, after = chunks(source), chunks(result)
        assert [(t,p) for t,p in before if t in COLOR or t==b'IDAT'] == [(t,p) for t,p in after if t in COLOR or t==b'IDAT']
        assert not any(t in {b'tEXt',b'iTXt',b'zTXt',b'comf',b'eXIf',b'tIME',b'cuSt'} for t,p in after)
        assert b'KSampler' not in result.read_bytes() and b'secret' not in result.read_bytes()
        assert static_image(source) == static_image(result)
        generation=b'a portrait, soft lighting\nNegative prompt: low quality\nSteps: 30, Sampler: Euler a, CFG scale: 7, Seed: 123456, Size: 160x100, Model: private-model, Model hash: abc123'
        webui_png=root/'webui.png';webui_png.write_bytes(original[:33]+chunk(b'tEXt',b'parameters\0'+generation)+original[33:])
        webui_clear=clean(webui_png,out)
        assert not any(t in {b'tEXt',b'iTXt',b'zTXt',b'eXIf'} for t,p in chunks(webui_clear))
        assert static_image(webui_png)==static_image(webui_clear) and b'private-model' not in webui_clear.read_bytes()
        # Header, bit depth, compressed samples and palette are copied, including 16-bit data.
        for depth, mode, raw, palette in [(16,6,b'\0'+bytes(range(32)),b''), (8,3,b'\0\x00\x01\x02\x01',chunk(b'PLTE',b'\xff\x00\x00\x00\xff\x00\x00\x00\xff')+chunk(b'tRNS',b'\xff\x80\x00'))]:
            sample = root / f'color-{depth}-{mode}.png'
            h = struct.pack('>IIBBBBB',4,1,depth,mode,0,0,0)
            sample.write_bytes(SIGNATURE+chunk(b'IHDR',h)+palette+b''.join(text_chunks)+chunk(b'IDAT',zlib.compress(raw))+chunk(b'IEND',b''))
            cleared = clean(sample, out)
            assert [(t,p) for t,p in chunks(sample) if t in COLOR or t==b'IDAT'] == [(t,p) for t,p in chunks(cleared) if t in COLOR or t==b'IDAT']
        # APNG's animation data remains intact and the hidden image can still be restored.
        animated_png = root / 'animated.png'
        run('--encode', root/'frame0.png', root/'frame1.png', animated_png)
        raw = animated_png.read_bytes(); animated_png.write_bytes(raw[:33]+chunk(b'comf',b'workflow\0'+workflow)+raw[33:])
        cleared = clean(animated_png, out)
        tags = {b'IHDR',b'IDAT',b'acTL',b'fcTL',b'fdAT'}
        assert [(t,p) for t,p in chunks(animated_png) if t in tags] == [(t,p) for t,p in chunks(cleared) if t in tags]
        restored = root / 'restored.png'; run('--restore', cleared, restored)
        assert static_image(restored) == static_image(root/'frame1.png')
        # Only orientation is retained from EXIF, never descriptive/workflow tags.
        oriented = root/'oriented.png'; oriented.write_bytes(original[:33]+chunk(b'eXIf',exif(6)+b'workflow:'+workflow)+original[33:])
        cleared = clean(oriented, out)
        assert next(p for t,p in chunks(cleared) if t==b'eXIf') == exif(6)
        # Real GIF output: count, delays, loop extension and compressed frames preserved by cleanup.
        gif = (root/'animated.gif').read_bytes(); blocks = gif_blocks(gif)
        assert len([b for b in blocks if b[0]=='image']) == 3
        controls = [b[2] for b in blocks if b[0]=='ext' and b[1]==0xf9]
        assert all(struct.unpack_from('<H', c,1)[0] == 40 for c in controls)
        assert any(b[0]=='ext' and b[1]==0xff and b[2].startswith(b'NETSCAPE2.0') for b in blocks)
        info_gif = root/'annotated.gif'; info_gif.write_bytes(gif[:-1]+b'\x21\xfe'+bytes([len(workflow)])+workflow+b'\0\x3b')
        cleared_gif = clean(info_gif, out).read_bytes()
        assert cleared_gif == gif and b'KSampler' not in cleared_gif
        plain = gif_blocks((root/'no-loop.gif').read_bytes())
        assert not any(b[0]=='ext' and b[1]==0xff for b in plain)
        assert all(struct.unpack_from('<H',b[2],1)[0] == 23 for b in plain if b[0]=='ext' and b[1]==0xf9)
        # Minimal valid lossless WebP fixture; remove EXIF/XMP but preserve ICC and compressed pixels.
        vp8l = bytes.fromhex('2f00000000075081fa00')
        base = b'WEBP'+riff_chunk(b'VP8X',bytes([0x2c,0,0,0,0,0,0,0,0,0]))+riff_chunk(b'ICCP',b'unchanged ICC')+riff_chunk(b'VP8L',vp8l)+riff_chunk(b'EXIF',exif(1)+workflow)+riff_chunk(b'XMP ',workflow)
        webp = root/'comfy.webp'; webp.write_bytes(b'RIFF'+struct.pack('<I',len(base))+base)
        cleared = riff(clean(webp,out).read_bytes())
        assert dict(cleared)[b'VP8L'] == vp8l and dict(cleared)[b'ICCP'] == b'unchanged ICC'
        assert b'EXIF' not in dict(cleared) and b'XMP ' not in dict(cleared)
        assert dict(cleared)[b'VP8X'][0] == 0x20
        rotated_base = b'WEBP'+riff_chunk(b'VP8X',bytes([0x2c,0,0,0,0,0,0,0,0,0]))+riff_chunk(b'ICCP',b'unchanged ICC')+riff_chunk(b'VP8L',vp8l)+riff_chunk(b'EXIF',b'Exif\0\0'+exif(6)+workflow)+riff_chunk(b'XMP ',workflow)
        rotated_webp=root/'oriented.webp';rotated_webp.write_bytes(b'RIFF'+struct.pack('<I',len(rotated_base))+rotated_base)
        rotated=riff(clean(rotated_webp,out).read_bytes())
        assert dict(rotated)[b'EXIF']==exif(6) and dict(rotated)[b'VP8X'][0]==0x28
        # Real JPEG: strip APP1 and comments, keep original scan bytes and minimal orientation.
        jpeg=(root/'photo.jpg').read_bytes()
        payload=b'Exif\0\0'+exif(6)+workflow
        metadata=b'\xff\xe1'+struct.pack('>H',len(payload)+2)+payload+b'\xff\xfe'+struct.pack('>H',len(workflow)+2)+workflow
        photo=root/'comfy-photo.jpg';photo.write_bytes(jpeg[:2]+metadata+jpeg[2:]+b'private trailing workflow')
        cleared_photo=clean(photo,out).read_bytes()
        assert cleared_photo.endswith(b'\xff\xd9') and b'KSampler' not in cleared_photo and b'private' not in cleared_photo
        scan=jpeg.index(b'\xff\xda');clear_scan=cleared_photo.index(b'\xff\xda')
        assert jpeg[scan:]==cleared_photo[clear_scan:]
        assert b'Exif\0\0'+exif(6) in cleared_photo
        comment=b'ASCII\0\0\0'+generation
        webui_exif=b'II*\0'+struct.pack('<IH',8,1)+struct.pack('<HHII',0x8769,4,1,26)+struct.pack('<I',0)+struct.pack('<H',1)+struct.pack('<HHII',0x9286,7,len(comment),44)+struct.pack('<I',0)+comment
        jpg_payload=b'Exif\0\0'+webui_exif
        webui_jpg=root/'webui.jpg';webui_jpg.write_bytes(jpeg[:2]+b'\xff\xe1'+struct.pack('>H',len(jpg_payload)+2)+jpg_payload+jpeg[2:])
        cleared_jpg=clean(webui_jpg,out).read_bytes()
        assert cleared_jpg==jpeg and b'private-model' not in cleared_jpg
        webui_base=b'WEBP'+riff_chunk(b'VP8X',bytes([0x08,0,0,0,0,0,0,0,0,0]))+riff_chunk(b'VP8L',vp8l)+riff_chunk(b'EXIF',jpg_payload)
        webui_webp=root/'webui.webp';webui_webp.write_bytes(b'RIFF'+struct.pack('<I',len(webui_base))+webui_base)
        webui_webp_clean=riff(clean(webui_webp,out).read_bytes())
        assert b'EXIF' not in dict(webui_webp_clean) and dict(webui_webp_clean)[b'VP8L']==vp8l
        # Existing paths are not overwritten by repeat cleanup.
        first = result.read_bytes(); second=clean(source,out)
        assert second != result and result.read_bytes() == first
        failed = root/'broken.png'; broken=bytearray(source.read_bytes());broken[40]^=1;failed.write_bytes(broken)
        proc=subprocess.run([str(EXE),'--clean',str(failed),str(out)],capture_output=True)
        assert proc.returncode != 0
        report = {'result':'PASS','checks':[
            'ComfyUI workflow/prompt text, compressed and international text, comf and EXIF removed',
            'WebUI PNG parameters and JPEG/WebP EXIF UserComment removed, compressed image bytes unchanged',
            'PNG IDAT and ICC/gamma/chromaticity/HDR color blocks byte-identical',
            'decoded RGBA unchanged, palette/alpha and 16-bit samples preserved',
            'APNG frames byte-identical, cleaned disguise still restores original',
            'orientation-only EXIF retained without workflow',
            'GIF frame count, exact delays, loop option and lossless cleanup',
            'WebP ICC and compressed image data unchanged, EXIF/XMP removed and flags fixed',
            'JPEG scan bytes unchanged, workflow/comments/trailer removed and orientation retained',
            'existing output preserved and corrupted PNG rejected',
            'mosaic/blur/solid masks, brush segment and configurable padding tested in C#'
        ]}
        (ART/'tools-verification.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
        print(json.dumps(report,indent=2))


if __name__=='__main__': main()
