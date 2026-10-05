"""Check Lara's opposed thumb in palm space; --apply updates only thumb endpoints.

This checks analytic probes at full grip weight, not skinned visual acceptance.
Uses the imported thumb's identity rest rotations and direct rHand parent.
"""
import math, re, sys
from pathlib import Path

def add(a,b): return tuple(x+y for x,y in zip(a,b))
def mul(a,s): return tuple(x*s for x in a)
def dot(a,b): return sum(x*y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def norm(a): return mul(a,1/math.sqrt(dot(a,a)))
def qm(a,b): return (*add(add(mul(b[:3],a[3]),mul(a[:3],b[3])),cross(a[:3],b[:3])),a[3]*b[3]-dot(a[:3],b[:3]))
def qi(q): return (*mul(q[:3],-1),q[3])
def rot(q,v): return qm(qm(q,(*v,0)),qi(q))[:3]
def aa(axis,d): return (*mul(norm(axis),math.sin(math.radians(d/2))),math.cos(math.radians(d/2)))
def ft(a,b):
    a,b=norm(a),norm(b); q=(*cross(a,b),1+dot(a,b)); return mul(q,1/math.sqrt(dot(q,q)))
def sl(a,b,t):
    d=dot(a,b)
    if d<0: b=mul(b,-1); d=-d
    if d>.9999: return norm(add(mul(a,1-t),mul(b,t)))
    ang=math.acos(min(1,d)); return add(mul(a,math.sin((1-t)*ang)/math.sin(ang)),mul(b,math.sin(t*ang)/math.sin(ang)))
def vecs(s): return [tuple(float(v) for v in re.findall(r'[xyzw]: ([^,}]+)',m)) for m in re.findall(r'\{[^}]+\}',s)]
path=Path(__file__).resolve().parents[1] / 'validation/DazPoseUnityValidation/Assets/DazPose/Generated/HandGrip/Profiles/LaraRightHandGrip.asset'
src=path.read_text()
thumb=src.split('  - digit: 0')[1].split('  - digit: 1')[0]
pos=vecs(thumb.split('jointLocalPositions:')[1].split('openLocalRotations:')[0])
anchor=vecs(src.split('palmAnchorLocalRotation:')[1])[0]
ap=vecs(src.split('palmAnchorLocalPosition:')[1])[0]
x,y,z=[rot(anchor,v) for v in [(1,0,0),(0,1,0),(0,0,1)]]
print('thumb base in palm',rot(qi(anchor),add(pos[0],mul(ap,-1))))
for lift in [0]:
    op=ft(pos[1],add(mul(x,-1),mul(y,lift)))
    opens=[op,(0,0,0,1),(0,0,0,1)]
    closes=[qm(aa(z,100),op),aa(rot(qi(op),z),65),aa(rot(qi(op),z),55)]
    probes=vecs(thumb.split('probes:')[1]); probes=[probes[i] for i in range(len(probes))]
    def clearance(t,r):
        p=(0,0,0); q=(0,0,0,1); poses=[]
        for off,o,c in zip(pos,opens,closes): p=add(p,rot(q,off));q=qm(q,sl(o,c,t));poses.append((p,q))
        values=[]
        for i,(j,pr) in enumerate(zip([0,1,2,2],[.006834838,.0049211,.005,.007])):
            pp=add(poses[j][0],rot(poses[j][1],probes[i])); cp=rot(qi(anchor),add(pp,mul(ap,-1)));values.append(math.hypot(cp[0],cp[1]+r+.003)-r-pr-.003)
        return min(values)
    assert dot(norm(rot(op,pos[1])),x) < -.999, 'thumb must extend opposite finger direction'
    for r in [.015,.025,.035,.055]:
        first=next((i/1000 for i in range(1001) if clearance(i/1000,r)<0),None)
        assert clearance(0,r)>0 and first is not None and first>0, 'missing thumb contact bracket'
        low,high=0,1
        for step in range(1,33):
            t=step/32
            if clearance(t,r)<0: high=t; break
            low=t
        for _ in range(10):
            t=(low+high)/2
            if clearance(t,r)<0: high=t
            else: low=t
        assert 0<=clearance(low,r)<.00001, 'contact must stop outside the analytic surface'
        print('PASS radius_mm=',r*1000,'thumb_curl=',round(low,4),'min_clearance_mm=',round(clearance(low,r)*1000,6))
    def rows(qs):
        return ''.join('    - {'+', '.join(f'{k}: {v:.9g}' for k,v in zip('xyzw',q))+'}\n' for q in qs)
    candidate=re.sub(r'    openLocalRotations:\n.*?    curlBias:',
        '    openLocalRotations:\n'+rows(opens)+'    closedLocalRotations:\n'+rows(closes)+'    curlBias:',thumb,flags=re.S)
    if '--apply' in sys.argv:
        path.write_text(src.replace(thumb,candidate),newline='\n')
        print('Updated only thumb endpoints in',path)
    else:
        actual_open=vecs(thumb.split('openLocalRotations:')[1].split('closedLocalRotations:')[0])
        actual_closed=vecs(thumb.split('closedLocalRotations:')[1].split('curlBias:')[0])
        assert all(abs(abs(dot(norm(a),norm(b)))-1)<1e-6 for a,b in zip(actual_open+actual_closed,opens+closes)), 'profile differs from opposed thumb candidate'

