#!/usr/bin/env python3
"""Generate macOS icon assets from pinned original PCL XAML/VB geometries.

No SF Symbols or replacement artwork is used. Source paths, fill/stroke rules,
rotations and byte hashes are recorded so every asset can be audited/rebuilt.
"""
from pathlib import Path
import argparse,hashlib,importlib.util,json,math,re
BASE=Path(__file__).resolve().parents[1]
OUT=BASE/'Resources/SourceIcons'
spec=importlib.util.spec_from_file_location('vectors',Path(__file__).with_name('make-version-action-icons.py'))
v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)
inputs=v.load_icon_sources()
assets=inputs['sourceIcons']

def rotate(commands,angle):
 if not angle:return commands
 theta=math.radians(angle);c,s=math.cos(theta),math.sin(theta)
 return [(op,*[q for i in range(0,len(values),2) for q in (values[i]*c-values[i+1]*s,values[i]*s+values[i+1]*c)]) for op,*values in commands]
def pdf(commands,bounds,even_odd,stroke=None,round_caps=False):
 if not stroke:return v.make_pdf(commands,bounds,even_odd=even_odd)
 x0,y0,x1,y1=bounds;fmt=v.fmt
 lines=[f'q 1 0 0 -1 {fmt(-x0)} {fmt(y1)} cm 0 G {fmt(stroke)} w '+('1 J 1 j' if round_caps else '0 J 0 j')]
 ops={'M':'m','L':'l','C':'c','Z':'h'}
 for op,*values in commands:lines.append(' '.join([*[fmt(n) for n in values],ops[op]]))
 lines.append('S Q');stream=('\n'.join(lines)+'\n').encode()
 objects=[b'<< /Type /Catalog /Pages 2 0 R >>',b'<< /Type /Pages /Kids [3 0 R] /Count 1 >>',f'<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {fmt(x1-x0)} {fmt(y1-y0)}] /Resources << >> /Contents 4 0 R >>'.encode(),b'<< /Length '+str(len(stream)).encode()+b' >>\nstream\n'+stream+b'endstream']
 out=bytearray(b'%PDF-1.4\n%\xe2\xe3\xcf\xd3\n');offsets=[0]
 for i,obj in enumerate(objects,1):offsets.append(len(out));out.extend(f'{i} 0 obj\n'.encode()+obj+b'\nendobj\n')
 start=len(out);out.extend(f'xref\n0 {len(objects)+1}\n0000000000 65535 f \n'.encode())
 for off in offsets[1:]:out.extend(f'{off:010d} 00000 n \n'.encode())
 out.extend(f'trailer\n<< /Size {len(objects)+1} /Root 1 0 R >>\nstartxref\n{start}\n%%EOF\n'.encode());return bytes(out)
def main():
 parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args();report={}
 if not args.check:OUT.mkdir(parents=True,exist_ok=True)
 for name,a in assets.items():
  geometry=a['geometry'];even_odd=not geometry.startswith('F1 ');raw=re.sub(r'^F[01]\s*','',geometry)
  commands=rotate(v.parse_path(raw),a.get('rotation',0));bounds=a.get('viewport') or v.geometry_bounds(commands)
  if a.get('stroke') and not a.get('viewport'):pad=a['stroke']/2;bounds=[bounds[0]-pad,bounds[1]-pad,bounds[2]+pad,bounds[3]+pad]
  data=pdf(commands,bounds,even_odd,a.get('stroke'),a.get('roundCaps',False));path=OUT/(name+'.pdf')
  v.write_or_check(path,data,args.check)
  report[name]={k:val for k,val in a.items() if k!='geometry'};report[name].update(sourceGeometrySHA256=hashlib.sha256(geometry.encode()).hexdigest(),pdfSHA256=hashlib.sha256(data).hexdigest(),geometryBounds=bounds,fillRule='stroke' if a.get('stroke') else 'evenOdd' if even_odd else 'nonzero',template=True,resource=name+'.pdf')
 for name,a in inputs['bitmaps'].items():
  data=(BASE/'Support/IconSources'/a['input']).read_bytes();dest=OUT/(name+'.png')
  v.write_or_check(dest,data,args.check)
  report[name]={'source':a['source'],'sourceLine':1,'resource':dest.name,'sha256':hashlib.sha256(data).hexdigest(),'template':False}
 manifest=(json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode();target=OUT/'source-map.json'
 v.write_or_check(target,manifest,args.check)
 print(('Verified' if args.check else 'Generated')+f' {len(report)} original PCL assets.')
if __name__=='__main__':main()
