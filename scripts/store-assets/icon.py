"""Jeepney Rush launcher / Play icon: the jeepney charging forward (sticker outline, speed
lines) in front of the Philippine sun, flag-coloured sunburst, three stars and a RUSH! badge.
Usage: python3 icon.py <raw jeepney render.png> <out dir>
  writes icon-512.png / icon-1024.png (Play), icon_adaptive_bg.png / icon_adaptive_fg.png
  (Android 8+ layers, art inside the 66/108 dp safe circle) and preview.png (launcher masks).
The raw render comes from render.mjs ('icon' job: low, wide-angle front three-quarter)."""
import math, os, sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops
RES=os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','..','unity','JeepneyRush','Assets','JeepneyRush','Resources','Fonts')+os.sep
DISPLAY=RES+'LuckiestGuy-Regular.ttf'
INK=(27,16,51); YEL=(252,209,22); RED=(206,17,38); BLUE=(0,56,168); WHITE=(255,255,255)
N=1024

def background(sy=0.30, sr=1.0):
    im=Image.new('RGB',(N,N),BLUE); d=ImageDraw.Draw(im)
    d.polygon([(N*0.0,N*1.0),(N*1.0,N*0.30),(N,N),],fill=RED)  # red lower-right half (diagonal like the flag's split)
    im=im.convert('RGBA')
    # sunburst rays from the sun centre
    c=(N*0.5,N*sy)
    rays=Image.new('RGBA',(N,N),(0,0,0,0)); rd=ImageDraw.Draw(rays)
    for i in range(24):
        a0=i*2*math.pi/24; a1=a0+math.pi/24*0.9
        rd.polygon([c,(c[0]+1500*math.cos(a0),c[1]+1500*math.sin(a0)),(c[0]+1500*math.cos(a1),c[1]+1500*math.sin(a1))],fill=(255,255,255,34))
    im=Image.alpha_composite(im,rays)
    # warm glow
    glow=Image.new('RGBA',(N,N),(0,0,0,0)); gd=ImageDraw.Draw(glow)
    gd.ellipse((c[0]-260*sr,c[1]-260*sr,c[0]+260*sr,c[1]+260*sr),fill=(255,236,150,120))
    im=Image.alpha_composite(im,glow.filter(ImageFilter.GaussianBlur(70)))
    # Philippine sun: disc + 8 main rays (each a long spike flanked by two short ones)
    sun=Image.new('RGBA',(N,N),(0,0,0,0)); sd=ImageDraw.Draw(sun)
    R0,R1=int(100*sr),int(290*sr)
    for k in range(8):
        base=k*math.pi/4-math.pi/8
        for off,L,w in ((0,R1,0.085),(-0.2,R1*0.78,0.045),(0.2,R1*0.78,0.045)):
            a=base+off
            sd.polygon([(c[0]+R0*0.9*math.cos(a-w),c[1]+R0*0.9*math.sin(a-w)),(c[0]+L*math.cos(a),c[1]+L*math.sin(a)),(c[0]+R0*0.9*math.cos(a+w),c[1]+R0*0.9*math.sin(a+w))],fill=YEL+(255,))
    sd.ellipse((c[0]-R0,c[1]-R0,c[0]+R0,c[1]+R0),fill=YEL+(255,))
    edge=sun.split()[3].filter(ImageFilter.MaxFilter(9))
    ring=Image.new('RGBA',(N,N),(214,140,0,255)); ring.putalpha(edge)
    im=Image.alpha_composite(im,ring); im=Image.alpha_composite(im,sun)
    # three stars (flag triangle), inside the rounded-corner safe area
    def star(cx,cy,r,col):
        pts=[]
        for i in range(10):
            a=-math.pi/2+i*math.pi/5; rr=r if i%2==0 else r*0.42
            pts.append((cx+rr*math.cos(a),cy+rr*math.sin(a)))
        ImageDraw.Draw(im).polygon(pts,fill=col)
    for (x,y,r) in ((165,175,44),(859,175,44),(140,610,32)): star(x,y,r+7,INK+(255,)); star(x,y,r,YEL+(255,))
    return im

def outline(img, width, col):
    a=img.split()[3].point(lambda v:255 if v>40 else 0)
    big=a.filter(ImageFilter.MaxFilter(width*2+1))
    o=Image.new('RGBA',img.size,col+(255,)); o.putalpha(big)
    return o

def jeepney(size):
    j=Image.open(sys.argv[1]).convert('RGBA'); j=j.crop(j.getbbox())
    s=size/max(j.size); j=j.resize((int(j.width*s),int(j.height*s)),Image.LANCZOS)
    pad=40; c=Image.new('RGBA',(j.width+2*pad,j.height+2*pad),(0,0,0,0)); c.alpha_composite(j,(pad,pad))
    c=c.rotate(4,resample=Image.BICUBIC,expand=False)   # nose down a little: charging forward
    out=Image.alpha_composite(outline(c,16,INK),outline(c,9,WHITE)); out.alpha_composite(c)
    return out

def speed_lines(im, box):
    d=ImageDraw.Draw(im)
    x0,y0,x1,y1=box
    for i,(fy,L,w) in enumerate(((0.25,230,14),(0.45,300,18),(0.62,260,14),(0.80,200,12))):
        y=y0+(y1-y0)*fy; x=x1-40
        d.rounded_rectangle((x,y-w/2,x+L,y+w/2),w/2,fill=(255,255,255,235))

def badge(text, size):
    f=ImageFont.truetype(DISPLAY,size)
    tmp=Image.new('RGBA',(N,N),(0,0,0,0)); d=ImageDraw.Draw(tmp)
    d.text((N/2,N/2),text,font=f,anchor='mm',fill=YEL,stroke_width=int(size*0.13),stroke_fill=INK)
    t=tmp.crop(tmp.getbbox())
    sh=Image.new('RGBA',(t.width+30,t.height+30),(0,0,0,0)); sh.alpha_composite(t,(15,15))
    shadow=Image.new('RGBA',sh.size,INK+(255,)); shadow.putalpha(sh.split()[3])
    out=Image.new('RGBA',(sh.width+14,sh.height+14),(0,0,0,0)); out.alpha_composite(shadow,(12,12)); out.alpha_composite(sh,(0,0))
    return out.rotate(7,resample=Image.BICUBIC,expand=True)

def foreground(scale, cy):
    fg=Image.new('RGBA',(N,N),(0,0,0,0))
    j=jeepney(int(780*scale))
    x=(N-j.width)//2+int(10*scale); y=int(cy-j.height/2)
    speed_lines(fg,(x+j.width*0.55,y+j.height*0.25,x+j.width*0.80,y+j.height*0.75))
    sh=Image.new('RGBA',(N,N),(0,0,0,0)); ImageDraw.Draw(sh).ellipse((x+j.width*0.08,y+j.height*0.80,x+j.width*0.95,y+j.height*0.98),fill=(0,0,0,110))
    fg=Image.alpha_composite(fg,sh.filter(ImageFilter.GaussianBlur(22)))
    fg.alpha_composite(j,(x,y))
    b=badge('RUSH!',int(170*scale))
    fg.alpha_composite(b,((N-b.width)//2+int(150*scale),int(cy+j.height*0.30)))
    return fg

out=sys.argv[2]; os.makedirs(out,exist_ok=True)
bg=background()
full=Image.alpha_composite(bg,foreground(0.92,560))
full.resize((512,512),Image.LANCZOS).save(os.path.join(out,'icon-512.png'),optimize=True)  # 32-bit PNG as Play asks
full.convert('RGB').save(os.path.join(out,'icon-1024.png'))
# adaptive: background layer + foreground inside the 66/108 dp safe circle
bgA=background(0.36,0.62)
bgA.save(os.path.join(out,'icon_adaptive_bg.png'),optimize=True)
fgA=foreground(0.56,535); fgA.save(os.path.join(out,'icon_adaptive_fg.png'),optimize=True)
# previews: rounded square (Play), circle and squircle launcher masks, small sizes
prev=Image.new('RGB',(1200,420),(236,236,240))
def mask(kind,s):
    m=Image.new('L',(s,s),0); d=ImageDraw.Draw(m)
    if kind=='circle': d.ellipse((0,0,s-1,s-1),fill=255)
    else: d.rounded_rectangle((0,0,s-1,s-1),int(s*(0.22 if kind=='round' else 0.32)),fill=255)
    return m
adapt=Image.alpha_composite(bgA,fgA)
# a launcher shows the middle 72/108 of the adaptive layers
crop=adapt.crop((int(N*18/108),int(N*18/108),int(N*90/108),int(N*90/108)))
items=[(full,'round',300),(crop,'circle',300),(crop,'squircle',300),(full,'round',96),(full,'round',48)]
x=20
for img,k,s in items:
    t=img.convert('RGB').resize((s,s),Image.LANCZOS); prev.paste(t,(x,60),mask(k,s)); x+=s+24
prev.save(os.path.join(out,'preview.png'))
