"""Composes the final Google Play assets from the raw renders (see render.mjs).
Output (store/play/):
  icon-512.png                 512x512, 32-bit PNG (Play applies the rounded mask itself)
  feature-graphic-1024x500.png 24-bit PNG, no alpha
  phone/       1080x1920  (9:16)
  tablet-7in/  1152x2048  (9:16)
  tablet-10in/ 1440x2560  (9:16, every side >= 1080 px as Play requires for 10-inch)
"""
import math, os
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
RAW = os.path.join(HERE, '.raw')
OUT = os.path.join(ROOT, 'store', 'play')
RES = os.path.join(ROOT, 'unity', 'JeepneyRush', 'Assets', 'JeepneyRush', 'Resources')
DISPLAY = os.path.join(RES, 'Fonts', 'LuckiestGuy-Regular.ttf')
BODY = os.path.join(RES, 'Fonts', 'Nunito-Black.ttf')
INK = (27, 16, 51)
YELLOW, RED, BLUE, WHITE = (252, 209, 22), (200, 16, 46), (31, 63, 191), (255, 255, 255)

CAPTIONS = [  # (headline line 1, line 2 highlighted, sub-line)
    ('01_stop', 'PICK UP', 'PASSENGERS', '"Bayad po!"  Stop at the right sakayan'),
    ('02_edsa', 'BEAT THE', 'EDSA TRAFFIC', 'Buses, jams and rush hour'),
    ('03_driver', "DRIVER'S SEAT", 'VIEW', 'Old-style speedometer and mirror'),
    ('04_night', 'NIGHT RIDES', 'IN THE RAIN', 'Day turns to night, typhoons roll in'),
    ('05_street', 'A LIVING', 'PINOY STREET', 'Barkers, vendors and tricycles'),
    ('06_garage', 'COLLECT', '13 JEEPNEYS', 'Patok, Modern, Fiesta and more'),
]
DEVICES = {'phone': (1080, 1920), 'tablet-7in': (1152, 2048), 'tablet-10in': (1440, 2560)}
RAWNAME = {'phone': 'phone', 'tablet-7in': 'tablet7', 'tablet-10in': 'tablet10'}


def font(path, size):
    return ImageFont.truetype(path, size)


def gradient(w, h, top, bottom):
    g = Image.new('RGB', (1, h))
    for y in range(h):
        t = y / max(1, h - 1)
        g.putpixel((0, y), tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)))
    return g.resize((w, h))


def rays(size, colours, n=16, centre=(0.5, 0.62)):
    """Sunburst background (Philippine flag colours), drawn supersampled."""
    S = 2
    w, h = size[0] * S, size[1] * S
    im = Image.new('RGB', (w, h), colours[0])
    d = ImageDraw.Draw(im)
    cx, cy, R = w * centre[0], h * centre[1], max(w, h) * 1.6
    for i in range(n):
        a0, a1 = 2 * math.pi * i / n, 2 * math.pi * (i + 0.5) / n
        d.polygon([(cx, cy), (cx + R * math.cos(a0), cy + R * math.sin(a0)), (cx + R * math.cos(a1), cy + R * math.sin(a1))], fill=colours[1 + i % (len(colours) - 1)])
    glow = Image.new('L', (w, h), 0)
    ImageDraw.Draw(glow).ellipse((cx - w * 0.42, cy - w * 0.42, cx + w * 0.42, cy + w * 0.42), fill=255)
    glow = glow.filter(ImageFilter.GaussianBlur(w * 0.12))
    im = Image.composite(Image.new('RGB', (w, h), (255, 214, 102)), im, glow.point(lambda v: int(v * 0.55)))
    return im.resize(size, Image.LANCZOS)


def outlined(draw, xy, text, f, fill, stroke, width, anchor='mm'):
    draw.text(xy, text, font=f, fill=fill, stroke_width=width, stroke_fill=stroke, anchor=anchor)


def screenshot(dev, key, l1, l2, sub):
    W, H = DEVICES[dev]
    k = W / 1080
    canvas = gradient(W, H, (64, 30, 140), (24, 12, 60)).convert('RGBA')
    # flag stripe along the bottom
    d = ImageDraw.Draw(canvas)
    sh = int(26 * k)
    d.rectangle((0, H - sh, int(W * 0.47), H), fill=BLUE)
    d.rectangle((int(W * 0.47), H - sh, int(W * 0.53), H), fill=YELLOW)
    d.rectangle((int(W * 0.53), H - sh, W, H), fill=RED)
    # caption
    outlined(d, (W / 2, int(118 * k)), l1, font(DISPLAY, int(92 * k)), WHITE, INK, int(9 * k))
    outlined(d, (W / 2, int(222 * k)), l2, font(DISPLAY, int(112 * k)), YELLOW, INK, int(10 * k))
    d.text((W / 2, int(318 * k)), sub, font=font(BODY, int(40 * k)), fill=(225, 220, 255), anchor='mm')
    # the game scene in a rounded frame
    shot = Image.open(os.path.join(RAW, f'{RAWNAME[dev]}_{key}.png')).convert('RGB')
    iw, ih = shot.size
    x0, y0 = (W - iw) // 2, H - sh - int(34 * k) - ih
    r = int(46 * k)
    mask = Image.new('L', (iw * 2, ih * 2), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, iw * 2 - 1, ih * 2 - 1), r * 2, fill=255)
    mask = mask.resize((iw, ih), Image.LANCZOS)
    shadow = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle((x0 - 4, y0 + int(14 * k), x0 + iw + 4, y0 + ih + int(18 * k)), r, fill=(0, 0, 0, 150))
    canvas = Image.alpha_composite(canvas, shadow.filter(ImageFilter.GaussianBlur(int(18 * k))))
    border = int(8 * k)
    d = ImageDraw.Draw(canvas)
    d.rounded_rectangle((x0 - border, y0 - border, x0 + iw + border, y0 + ih + border), r + border, fill=WHITE)
    canvas.paste(shot, (x0, y0), mask)
    out = os.path.join(OUT, dev)
    os.makedirs(out, exist_ok=True)
    canvas.convert('RGB').save(os.path.join(out, f'{key}.png'), optimize=True)


def icon():
    bg = rays((1024, 1024), [(31, 63, 191), (200, 16, 46), (31, 63, 191)], n=20, centre=(0.5, 0.58))
    jeep = Image.open(os.path.join(RAW, 'icon.png')).convert('RGBA')
    bbox = jeep.getbbox()
    jeep = jeep.crop(bbox)
    s = 800 / max(jeep.size)  # keeps clear of the rounded corners Play applies
    jeep = jeep.resize((int(jeep.width * s), int(jeep.height * s)), Image.LANCZOS)
    # soft shadow under the jeepney
    canvas = bg.convert('RGBA')
    sh = Image.new('RGBA', canvas.size, (0, 0, 0, 0))
    ImageDraw.Draw(sh).ellipse((170, 760, 870, 880), fill=(0, 0, 0, 120))
    canvas = Image.alpha_composite(canvas, sh.filter(ImageFilter.GaussianBlur(30)))
    canvas.alpha_composite(jeep, ((1024 - jeep.width) // 2, 1024 - jeep.height - 120))
    canvas = canvas.resize((512, 512), Image.LANCZOS)
    canvas.save(os.path.join(OUT, 'icon-512.png'), optimize=True)


def feature():
    hero = Image.open(os.path.join(RAW, 'feature.png')).convert('RGB').resize((1024, 500), Image.LANCZOS).convert('RGBA')
    # darken the right side so the logo pops
    shade = Image.new('RGBA', hero.size, (0, 0, 0, 0))
    sd = ImageDraw.Draw(shade)
    for x in range(1024):
        t = max(0.0, (x - 380) / 644)
        sd.line([(x, 0), (x, 500)], fill=(24, 12, 60, int(205 * min(1, t * 1.4))))
    hero = Image.alpha_composite(hero, shade)
    logo = Image.open(os.path.join(RES, 'Textures', 'logo.png')).convert('RGBA')
    logo = logo.crop(logo.getbbox())
    s = 470 / logo.width
    logo = logo.resize((int(logo.width * s), int(logo.height * s)), Image.LANCZOS)
    hero.alpha_composite(logo, (1024 - logo.width - 36, 70))
    d = ImageDraw.Draw(hero)
    f = font(BODY, 30)
    text = 'Pasada na!  Drive the Philippines'
    tw = d.textlength(text, font=f)
    cx = 1024 - 36 - logo.width / 2
    d.rounded_rectangle((cx - tw / 2 - 26, 372, cx + tw / 2 + 26, 428), 28, fill=YELLOW, outline=INK, width=5)
    d.text((cx, 400), text, font=f, fill=INK, anchor='mm')
    hero.convert('RGB').save(os.path.join(OUT, 'feature-graphic-1024x500.png'), optimize=True)


def android_icons():
    """Launcher icons for Unity (Assets/JeepneyRush/Art/Icon):
    icon-512.png  legacy / round / default icon (full artwork)
    icon_adaptive_bg.png / icon_adaptive_fg.png  Android 8+ adaptive icon layers (432 dp grid at 1024 px);
    the jeepney stays inside the central safe circle (66 of 108 dp) so no launcher mask cuts it."""
    out = os.path.join(ROOT, 'unity', 'JeepneyRush', 'Assets', 'JeepneyRush', 'Art', 'Icon')
    os.makedirs(out, exist_ok=True)
    bg = rays((1024, 1024), [(31, 63, 191), (200, 16, 46), (31, 63, 191)], n=20, centre=(0.5, 0.55))
    bg.save(os.path.join(out, 'icon_adaptive_bg.png'), optimize=True)
    jeep = Image.open(os.path.join(RAW, 'icon.png')).convert('RGBA')
    jeep = jeep.crop(jeep.getbbox())
    safe = int(1024 * 56 / 108)  # inside the 66 dp safe circle, with room for the flag poles
    s = safe / max(jeep.size)
    jeep = jeep.resize((int(jeep.width * s), int(jeep.height * s)), Image.LANCZOS)
    fg = Image.new('RGBA', (1024, 1024), (0, 0, 0, 0))
    sh = Image.new('RGBA', (1024, 1024), (0, 0, 0, 0))
    ImageDraw.Draw(sh).ellipse((512 - safe * 0.42, 512 + jeep.height * 0.36, 512 + safe * 0.42, 512 + jeep.height * 0.52), fill=(0, 0, 0, 110))
    fg = Image.alpha_composite(fg, sh.filter(ImageFilter.GaussianBlur(18)))
    fg.alpha_composite(jeep, ((1024 - jeep.width) // 2, (1024 - jeep.height) // 2 + 10))
    fg.save(os.path.join(out, 'icon_adaptive_fg.png'), optimize=True)
    # full icon (legacy launchers, round icon, Unity default icon): same art as the Play icon
    Image.open(os.path.join(OUT, 'icon-512.png')).convert('RGB').save(os.path.join(out, 'icon-512.png'), optimize=True)


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    icon()
    android_icons()
    feature()
    for dev in DEVICES:
        for key, l1, l2, sub in CAPTIONS:
            screenshot(dev, key, l1, l2, sub)
    print('wrote', OUT)
