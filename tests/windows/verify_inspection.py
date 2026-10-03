"""Independent metadata fixtures and UTF-8 report verification (standard library)."""
from pathlib import Path
import hashlib
import json
import struct
import subprocess
import zlib
from verify_apng import ART, EXE
from verify_tools import chunk, riff_chunk

OUT = ART / 'inspection-fixtures'
OUT.mkdir(exist_ok=True)
BASE = (b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB',1,1,8,6,0,0,0))
        + chunk(b'IDAT',zlib.compress(b'\0\xff\x80\x00\xff')) + chunk(b'IEND',b''))
PARAMS = ('明亮的湖泊, (Steps: 12), soft light\nsecond line\n'
          'Negative prompt: 模糊, watermark\nSteps: 28, Sampler: DPM++ 2M, '
          'Schedule type: Karras, CFG scale: 6.5, Seed: 18446744073709551615, '
          'Size: 1024x1536, Model: "model, custom", Clip skip: 2, '
          'Lora hashes: "style: abc, detail: def", Extra: {"a":"b,c"}')

def graph():
    return {
        '1': {'class_type':'CheckpointLoaderSimple','inputs':{'ckpt_name':'model.safetensors'}},
        '2': {'class_type':'CLIPTextEncode','inputs':{'text':'正向, lake','clip':['1',1]}},
        '3': {'class_type':'CLIPTextEncode','inputs':{'text':'负向, blur','clip':['1',1]}},
        '4': {'class_type':'EmptyLatentImage','inputs':{'width':1024,'height':1536,'batch_size':1}},
        '5': {'class_type':'KSampler','inputs':{'positive':['2',0],'negative':['3',0],
              'model':['1',0],'latent_image':['4',0],'cfg':6.5,'steps':28,
              'seed':18446744073709551615,'sampler_name':'euler','scheduler':'normal','denoise':0.8}},
        '6': {'class_type':'CLIPTextEncode','inputs':{'text':'另一组正向','clip':['1',1]}},
        '7': {'class_type':'KSampler','inputs':{'positive':['6',0],'negative':['3',0],
              'model':['1',0],'latent_image':['4',0],'cfg':9,'steps':40,'seed':42}},
        '8': {'class_type':'CLIPTextEncode','inputs':{'text':'UNCONNECTED DO NOT MIX'}},
        '9': {'class_type':'CFGGuider','inputs':{'positive':['2',0],'negative':['3',0],'model':['1',0],'cfg':3.5}},
        '10': {'class_type':'RandomNoise','inputs':{'noise_seed':1234567890123456}},
        '11': {'class_type':'SamplerCustomAdvanced','inputs':{'guider':['9',0],'noise':['10',0],'latent_image':['4',0]}}
    }

def inspect(path, expected_success=True):
    before=hashlib.sha256(path.read_bytes()).digest()
    output=path.with_suffix(path.suffix+'.json')
    proc=subprocess.run([str(EXE),'--inspect',str(path),str(output)],capture_output=True)
    assert (proc.returncode==0)==expected_success, path
    assert hashlib.sha256(path.read_bytes()).digest()==before, 'Reader modified source'
    return json.loads(output.read_text(encoding='utf-8-sig')) if expected_success else None

def png(name, blocks):
    path=OUT/name
    path.write_bytes(BASE[:33]+b''.join(blocks)+BASE[33:])
    return path

def itxt(key,text,compressed=False):
    data=text.encode('utf-8')
    return chunk(b'iTXt',key.encode()+b'\0'+bytes([compressed,0])+b'\0\0'+(zlib.compress(data) if compressed else data))

def check_web(data):
    fields={f['Name']:f['Value'] for f in data['WebUI']}
    assert fields['正面 tag']=='明亮的湖泊, (Steps: 12), soft light\nsecond line'
    assert fields['负面 tag']=='模糊, watermark'
    assert fields['CFG / Guidance']=='6.5'
    assert fields['种子 Seed']=='18446744073709551615'
    assert fields['模型 Model']=='model, custom'
    assert fields['Lora hashes']=='style: abc, detail: def'
    assert fields['Extra']=='{"a":"b,c"}' and fields['Clip skip']=='2'

def check_comfy(data):
    groups={g['Name'].split(' ')[0]:g['Fields'] for g in data['Comfy']}
    assert len(groups)==3
    assert groups['#5'][0]['Value']=='正向, lake' and groups['#5'][1]['Value']=='负向, blur'
    assert groups['#7'][0]['Value']=='另一组正向'
    assert groups['#11'][0]['Value']=='正向, lake' and groups['#11'][1]['Value']=='负向, blur'
    assert any(f['Value']=='18446744073709551615' for f in groups['#5'])
    assert any(f['Name'].startswith('CFG') and f['Value']=='3.5' for f in groups['#11'])
    assert any(f['Value']=='1234567890123456' for f in groups['#11'])
    assert not any('UNCONNECTED' in f['Value'] for fields in groups.values() for f in fields)

def tiff(tags):
    offset=8+2+len(tags)*12+4
    entries=[];payload=b''
    for tag,typ,data in tags:
        entries.append(struct.pack('<HHII',tag,typ,len(data),offset))
        payload+=data;offset+=len(data)
    return b'II*\0'+struct.pack('<IH',8,len(tags))+b''.join(entries)+struct.pack('<I',0)+payload

def check_custom_nodes():
    def run(name,nodes,positive,negative='bad quality', workflow=None):
        blocks=[itxt('prompt',json.dumps(nodes,ensure_ascii=False))]
        if workflow: blocks.append(itxt('workflow',json.dumps(workflow,ensure_ascii=False)))
        data=inspect(png(name+'.png',blocks))
        assert len(data['Comfy'])==1, (name,data)
        fields=data['Comfy'][0]['Fields']
        assert fields[0]['Value']==positive, (name,'positive',fields[0])
        assert fields[1]['Value']==negative, (name,'negative',fields[1])
        assert 'unconnected secret' not in fields[0]['Value']+fields[1]['Value']
        return data
    def base():
        return {'99':{'class_type':'CLIPTextEncode','inputs':{'text':'unconnected secret'}},
                '2':{'class_type':'CLIPTextEncode','inputs':{'text':'bad quality'}},
                '3':{'class_type':'KSampler','inputs':{'positive':['1',0],'negative':['2',0],'steps':20,'cfg':7,'seed':123}}}
    nodes=base();nodes.update({'1':{'class_type':'CLIPTextEncode','inputs':{'text':['4',0]}},
                              '4':{'class_type':'PrimitiveStringMultiline','inputs':{'value':'literal primitive prompt'}}})
    run('custom-primitive',nodes,'literal primitive prompt')
    nodes['4']={'class_type':'Custom Text Concatenate','inputs':{'text_a':['5',0],'text_b':['6',0],'delimiter':', '}}
    nodes['5']={'class_type':'StringConstant','inputs':{'string':'lake'}}
    nodes['6']={'class_type':'UserNodeXYZ','inputs':{'my_value':'soft light','ckpt_name':'secret.safetensors','mode':'enable'}}
    run('custom-linked-concat',nodes,'lake, soft light')
    nodes['4']={'class_type':'MyCustomEncoder','inputs':{'value':'mystery custom text','model':['5',0],'sampler_name':'euler','filename':'secret.png'}}
    run('custom-encoder-value',nodes,'mystery custom text')
    nodes['4']={'class_type':'ImpactWildcardEncode','inputs':{'wildcard_text':'__random_unresolved__','populated_text':'resolved wildcard prompt','mode':'reproduce'}}
    run('custom-wildcard-snapshot',nodes,'resolved wildcard prompt')
    for kind,positive_slot,negative_slot in [('Efficient Loader',1,2),('easy fullLoader',4,5)]:
        nodes=base();nodes['1']={'class_type':kind,'inputs':{'positive':'loader positive','negative':'loader negative','ckpt_name':'secret.safetensors','positive_strength':1}}
        nodes['3']['inputs'].update(positive=['1',positive_slot],negative=['1',negative_slot])
        run('custom-loader-'+str(positive_slot),nodes,'loader positive','loader negative')
        nodes['3']={'class_type':'easy fullSampler' if positive_slot==4 else 'KSampler SDXL (Efficient)',
                    'inputs':{'pipe' if positive_slot==4 else 'sdxl_tuple':['1',0],'steps':20,'cfg':7,'seed':123}}
        run('custom-pipe-'+str(positive_slot),nodes,'loader positive','loader negative')
    nodes=base();nodes['1']={'class_type':'OpaquePipeMaker','inputs':{'positive_prompt':'custom positive','negative_prompt':'custom negative','positive_prompt_style':'DO NOT USE STYLE'}}
    nodes['3']={'class_type':'UserSamplingNode','inputs':{'basic_pipe':['1',0],'steps':20,'cfg':7,'seed':123}}
    run('custom-sampling-fields',nodes,'custom positive','custom negative')
    nodes['3']={'class_type':'SamplerCustomAdvanced','inputs':{'guider':['4',0]}}
    nodes['4']={'class_type':'WrappedGuider','inputs':{'guider':['1',0]}}
    run('custom-guider-wrapper',nodes,'custom positive','custom negative')
    nodes['1']={'class_type':'BasicGuider','inputs':{'conditioning':['2',0],'model':['99',0]}}
    run('basic-guider-no-negative',nodes,'bad quality','')
    nodes=base();nodes['1']={'class_type':'CustomEncoder','inputs':{'text':'generic conditioning prompt'}}
    nodes['3']={'class_type':'CustomOneSampler','inputs':{'conditioning':['1',0]}}
    run('sampler-generic-conditioning',nodes,'generic conditioning prompt','')
    # Unknown shared loader has reversed output slots, explicitly named in workflow.
    nodes=base();nodes['1']={'class_type':'UnknownDualEncoder','inputs':{'positive':'saved positive','negative':'saved negative'}}
    nodes['3']['inputs'].update(positive=['1',1],negative=['1',0])
    wf={'nodes':[{'id':1,'type':'UnknownDualEncoder','widgets_values':{'positive':'stale workflow text'},
                  'outputs':[{'name':'negative','type':'CONDITIONING'},{'name':'positive','type':'CONDITIONING'}]}]}
    run('custom-output-names',nodes,'saved positive','saved negative',wf)
    nodes['3']['inputs'].update(positive=['1',0],negative=['1',1])
    run('custom-output-names-reversed',nodes,'saved negative','saved positive',wf)
    nodes=base();nodes['1']={'class_type':'CLIPTextEncode','inputs':{'text':['4',0]}}
    nodes['4']={'class_type':'CustomTextRouter','inputs':{'text':['5',0]}}
    nodes['5']={'class_type':'CustomTextRouter','inputs':{'text':['4',0]}}
    run('custom-cycle',nodes,'')
    # Workflow-only named widgets and single STRING widget, plus object-form links.
    wf={'nodes':[{'id':1,'type':'UserLiteral','widgets_values':['workflow custom positive'],'outputs':[{'name':'STRING','type':'STRING'}]},
                 {'id':2,'type':'CustomEncoder','widgets_values':{'text':'workflow custom negative'}},
                 {'id':3,'type':'CLIPTextEncode','inputs':[{'name':'text','link':1}]},
                 {'id':4,'type':'KSampler','widgets_values':[123,'fixed',20,7,'euler','normal',1],
                  'inputs':[{'name':'positive','link':2},{'name':'negative','link':3}]}],
        'links':[{'id':1,'origin_id':1,'origin_slot':0,'target_id':3,'target_slot':0},[2,3,0,4,0,'CONDITIONING'],[3,2,0,4,1,'CONDITIONING']]}
    data=inspect(png('workflow-custom-widgets.png',[itxt('workflow',json.dumps(wf))]))
    assert data['Comfy'][0]['Fields'][0]['Value']=='workflow custom positive'
    assert data['Comfy'][0]['Fields'][1]['Value']=='workflow custom negative'
    return 'custom text/value/string, linked concatenation, resolved wildcards, separate loader outputs, pipe/tuple sampling, wrapped guiders, workflow named widgets/output names, snapshot priority, cycles and disconnected branches'

def main():
    prompt=json.dumps(graph(),ensure_ascii=False)
    # Pillow's Latin text, compressed Latin text, UTF-8 iTXt and animated comf.
    plain='a lake\nNegative prompt: blur\nSteps: 20, CFG scale: 7, Seed: 42'
    for tag,payload in [(b'tEXt',b'parameters\0'+plain.encode()),(b'zTXt',b'parameters\0\0'+zlib.compress(plain.encode()))]:
        result=inspect(png(tag.decode()+'.png',[chunk(tag,payload)]))
        assert result['WebUI'][0]['Value']=='a lake'
    for compressed in (False,True):
        path=png('utf8-'+str(compressed)+'.png',[itxt('parameters',PARAMS,compressed),itxt('prompt',prompt,compressed)])
        data=inspect(path);check_web(data);check_comfy(data)
    ascii_prompt=json.dumps(graph())
    check_comfy(inspect(png('comf.png',[chunk(b'comf',b'prompt\0'+ascii_prompt.encode())])))
    flux=graph();flux['2']={'class_type':'CLIPTextEncodeFlux','inputs':{'clip_l':'Flux CLIP prompt','t5xxl':'Flux T5 prompt','guidance':3.5,'clip':['1',1]}}
    flux_data=inspect(png('flux.png',[itxt('prompt',json.dumps(flux))]))
    fields=flux_data['Comfy'][0]['Fields'];assert fields[0]['Value']=='Flux CLIP prompt\nFlux T5 prompt'
    assert any(f['Name'].startswith('CFG') and f['Value']=='3.5' for f in fields)
    assert any(f['Name'].startswith('t5xxl') and f['Value']=='Flux T5 prompt' for f in fields)
    control=graph();control['12']={'class_type':'ControlNetApplyAdvanced','inputs':{'positive':['2',0],'negative':['3',0],'strength':0.7}}
    control['5']['inputs'].update(positive=['12',0],negative=['12',1])
    check_comfy(inspect(png('controlnet.png',[itxt('prompt',json.dumps(control))])))
    # Known workflow-only widgets and input link reconstruction.
    workflow={'nodes':[
        {'id':1,'type':'CLIPTextEncode','widgets_values':['workflow positive'],'inputs':[]},
        {'id':2,'type':'CLIPTextEncode','widgets_values':['workflow negative'],'inputs':[]},
        {'id':3,'type':'KSampler','widgets_values':[123,'fixed',32,8,'euler','normal',1],
         'inputs':[{'name':'positive','link':1},{'name':'negative','link':2}]}],
        'links':[[1,1,0,3,0,'CONDITIONING'],[2,2,0,3,1,'CONDITIONING']]}
    fallback=inspect(png('workflow-only.png',[itxt('workflow',json.dumps(workflow))]))
    fields=fallback['Comfy'][0]['Fields']
    assert fields[0]['Value']=='workflow positive' and fields[1]['Value']=='workflow negative'
    assert any(f['Name'].startswith('CFG') and f['Value']=='8' for f in fields)
    assert fallback['Notes'] and fallback['WorkflowJson']
    # ComfyUI EXIF Model/Make and WebUI EXIF UserComment in JPEG / WebP.
    comfy_exif=tiff([(0x110,2,('prompt:'+prompt+'\0').encode()),(0x10f,2,('workflow:'+json.dumps(workflow)+'\0').encode())])
    web_exif=tiff([(0x9286,7,b'UNICODE\0'+PARAMS.encode('utf-16-be'))])
    for kind,payload,check in [('comfy',comfy_exif,check_comfy),('web',web_exif,check_web)]:
        body=b'Exif\0\0'+payload
        jpeg=OUT/(kind+'.jpg');jpeg.write_bytes(b'\xff\xd8\xff\xe1'+struct.pack('>H',len(body)+2)+body+b'\xff\xd9')
        check(inspect(jpeg))
        riff=b'WEBP'+riff_chunk(b'VP8L',bytes.fromhex('2f00000000075081fa00'))+riff_chunk(b'EXIF',body)
        webp=OUT/(kind+'.webp');webp.write_bytes(b'RIFF'+struct.pack('<I',len(riff))+riff)
        check(inspect(webp))
    # Actual cleaning removes readable tags; pixel/color chunks stay unchanged.
    source=png('combined.png',[itxt('parameters',PARAMS),itxt('prompt',prompt),itxt('workflow',json.dumps(workflow))])
    cleaned=OUT/'clean';cleaned.mkdir(exist_ok=True)
    before=set(cleaned.iterdir());subprocess.run([str(EXE),'--clean',str(source),str(cleaned)],check=True)
    cleared=(set(cleaned.iterdir())-before).pop();blank=inspect(cleared)
    assert blank['WebUI']==[] and blank['Comfy']==[] and blank['PromptJson'] is None
    # Invalid JSON keeps the original metadata for copying; CRC corruption fails.
    invalid=inspect(png('invalid-json.png',[itxt('prompt','{broken JSON')]))
    assert invalid['PromptJson']=='{broken JSON' and invalid['Notes']
    corrupt=png('broken-crc.png',[itxt('prompt',prompt)]);data=bytearray(corrupt.read_bytes());data[45]^=1;corrupt.write_bytes(data);inspect(corrupt,False)
    custom_check=check_custom_nodes()
    txtdir=OUT/'separate-txt';txtdir.mkdir(exist_ok=True)
    before=set(txtdir.glob('*.txt'))
    subprocess.run([str(EXE),'--inspect-txt',str(source),str(txtdir)],check=True)
    exported=set(txtdir.glob('*.txt'))-before;assert len(exported)==2
    texts=[p.read_text(encoding='utf-8-sig') for p in exported]
    assert all(p.read_bytes().startswith(b'\xef\xbb\xbf') for p in exported)
    webtext=next(t for t in texts if '===== WebUI =====' in t)
    comfytext=next(t for t in texts if '===== ComfyUI =====' in t)
    assert '===== ComfyUI =====' not in webtext and '===== WebUI =====' not in comfytext
    assert all(term in webtext for term in ['正面 tag','负面 tag','18446744073709551615','明亮的湖泊','CFG'])
    assert '原始 workflow JSON' in comfytext and '18446744073709551615' in comfytext
    empty_params=png('empty-webui.png',[itxt('parameters','')])
    for inputpath,count,label in [(OUT/'comf.png',1,'ComfyUI'),(OUT/'tEXt.png',1,'WebUI'),(cleared,0,None),(empty_params,0,None)]:
        before=set(txtdir.glob('*.txt'));subprocess.run([str(EXE),'--inspect-txt',str(inputpath),str(txtdir)],check=True)
        added=set(txtdir.glob('*.txt'))-before;assert len(added)==count
        assert all(('===== '+label+' =====') in p.read_text(encoding='utf-8-sig') for p in added)
    before=set(txtdir.glob('*.txt'));subprocess.run([str(EXE),'--inspect-txt',str(source),str(txtdir)],check=True)
    assert len(set(txtdir.glob('*.txt'))-before)==2, 'Repeated export overwrote documents'
    report={'result':'PASS','checks':['PNG tEXt/zTXt/iTXt/comf, UTF-8 and compressed text','WebUI multiline positive/negative tags, quoted commas, arbitrary parameters and exact 64-bit seeds','ComfyUI sampler-specific positive/negative links, disconnected nodes excluded, CFGGuider and multiple samplers','Flux CLIP-L/T5 tag fields, Guidance and ControlNet positive/negative output separation','workflow-only known widgets and links','JPEG/WebP EXIF ComfyUI prompt/workflow and Unicode WebUI UserComment','reading preserves source bytes, metadata-cleaned files contain no readable tags','malformed JSON retained with explanation, corrupted CRC rejected','UTF-8 BOM TXT preserves separate blocks, all parameters and raw workflow']}
    report['checks'][-1]='Separate UTF-8 BOM WebUI/ComfyUI TXT files, missing metadata skipped, repeated export never overwrites'
    report['checks'].append(custom_check)
    (ART/'inspection-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report,ensure_ascii=False,indent=2))

if __name__=='__main__':main()
