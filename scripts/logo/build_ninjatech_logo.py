# Rebuilds ninjatech_logo.png from a frame of the studio intro video:
# decomposes every pixel into a mix of the logo's flat palette colours (removes video
# compression noise and colour fringes), keys out the background, and upscales the
# coverage maps 2x with edge sharpening so the result is crisp at phone resolution.
import sys
from PIL import Image
import numpy as np

src, out = sys.argv[1], sys.argv[2]
im = np.array(Image.open(src).convert('RGB')).astype(np.float32)
CX, CY = 540, 788                      # crop centre (frame is 1080x1920; text is centred on x=540)
x0, x1, y0, y1 = CX - 351, CX + 351, 534, 1331
im = im[y0:y1, x0:x1]
H, W, _ = im.shape
pal = np.array([
    [10, 24, 63],     # 0 background (transparent outside the emblem, chevron ink inside it)
    [29, 45, 92],     # 1 emblem fill
    [245, 241, 232],  # 2 cream
    [205, 14, 36],    # 3 red
    [253, 209, 30],   # 4 yellow
    [10, 58, 167],    # 5 blue slash
], np.float32)
K = len(pal)
px = im.reshape(-1, 3)
best_err = np.full(px.shape[0], np.inf, np.float32)
cov = np.zeros((px.shape[0], K), np.float32)
for i in range(K):
    for j in range(i, K):
        a, b = pal[i], pal[j]
        d = a - b
        dd = float((d * d).sum())
        t = np.ones(px.shape[0], np.float32) if dd == 0 else np.clip(((px - b) @ d) / dd, 0, 1)
        rec = b + t[:, None] * d
        err = ((px - rec) ** 2).sum(1)
        better = err < best_err
        best_err[better] = err[better]
        cov[better] = 0
        cov[better, i] = t[better]
        cov[better, j] += 1 - t[better]
cov = cov.reshape(H, W, K)

# 2x upscale of each coverage map, then sharpen the transition (flat vector-style edges)
S = 2
def up(m):
    return np.asarray(Image.fromarray(m, 'F').resize((W * S, H * S), Image.LANCZOS))
def sharpen(c):
    c = np.clip((c - 0.5) * 1.6 + 0.5, 0, 1)      # steeper ramp, still anti-aliased
    return c * c * (3 - 2 * c)
covU = np.stack([sharpen(np.clip(up(cov[:, :, k]), 0, 1)) for k in range(K)], 2)
covU /= np.maximum(covU.sum(2, keepdims=True), 1e-6)

# inside the emblem disk the background colour is ink (the < > chevrons), so it stays opaque
yy, xx = np.mgrid[0:H * S, 0:W * S]
ccx, ccy = (491 - x0 + 0.5) * S, (788 - y0 + 0.5) * S
inside = np.hypot(xx - ccx, yy - ccy) < 214 * S
bgcov = np.where(inside, 0, covU[:, :, 0])
alpha = 1 - bgcov
ink = np.array([13, 24, 60], np.float32)
colour = sum(covU[:, :, k:k + 1] * pal[k] for k in range(1, K)) + np.where(inside, covU[:, :, 0], 0)[:, :, None] * ink
colour = colour / np.maximum(alpha[:, :, None], 1e-6)
rgba = np.dstack([np.clip(colour, 0, 255), np.clip(alpha * 255, 0, 255)]).round().astype(np.uint8)
# fully transparent pixels take the splash background colour, so filtering never shows light halos on the navy screen
rgba[rgba[:, :, 3] == 0, :3] = [10, 24, 63]
img = Image.fromarray(rgba, 'RGBA')
# final size: 1024 tall (the Android import limit), exact aspect of the crop
h = 1024; w = round(W * h / H)
img.resize((w, h), Image.LANCZOS).save(out, optimize=True)
print('wrote', out, w, h, 'aspect', W / H)
