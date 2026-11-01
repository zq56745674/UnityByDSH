
import os, numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = 'E:/pytest/unitydsh1/HelloWorldProject/Assets/Resources/UI'
os.makedirs(OUT, exist_ok=True)

# ---------- 木桌背景 1600x900 ----------
W, H = 1600, 900
rng = np.random.default_rng(7)
yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)

# 木板：横向 4 条，每条内部纹理
plank_h = H / 4.0
plank_id = (yy / plank_h).astype(int)
tint = np.array([1.0, 0.94, 0.88, 1.02])
base = np.zeros((H, W, 3), dtype=np.float32)
base[..., 0] = 44.0; base[..., 1] = 32.0; base[..., 2] = 24.0
base *= tint[plank_id][..., None]

# 纹理：沿 x 的长条纹 + 多频噪声
grain = (np.sin(xx * 0.014 + yy * 0.05) * 0.5 + np.sin(xx * 0.063 + yy * 0.011) * 0.3 + np.sin(xx * 0.21) * 0.12)
base += (grain * 7.0)[..., None]
sm = np.random.normal(0, 9, (H, W, 1))
base += sm

# 板缝
for i in range(1, 4):
    y = int(i * plank_h + rng.integers(-6, 6))
    base[max(0, y - 3):y + 3, :, :] *= 0.42
    base[y - 4:y - 2, :, :] *= 1.35

# 暗角
vx = (xx - W / 2) / (W / 2)
vy = (yy - H / 2) / (H / 2)
rad = np.sqrt(vx ** 2 + vy ** 2) / 1.414
vig = np.clip(1.0 - (rad ** 2.1) * 0.62, 0.22, 1.0)
base *= vig[..., None]

wood = Image.fromarray(np.clip(base, 0, 255).astype(np.uint8)).convert('RGB')
wood = wood.filter(ImageFilter.GaussianBlur(0.6))
wood.save(OUT + '/ui_bg_wood.png')
print('wood', wood.size)

# ---------- 噪点叠加 256 ----------
n = rng.normal(0.5, 0.16, (256, 256, 1))
a = np.clip(n * 255 * 0.55, 0, 255).astype(np.uint8)
arr = np.zeros((256, 256, 4), dtype=np.uint8)
arr[..., 3] = a[..., 0]
Image.fromarray(arr).save(OUT + '/ui_noise.png')
print('noise ok')

# ---------- 羊皮纸底纹（9 宫格，96x96，边缘 24）----------
S = 96
yy2, xx2 = np.mgrid[0:S, 0:S].astype(np.float32)
paper = np.zeros((S, S, 3), dtype=np.float32)
paper[..., 0] = 238.0; paper[..., 1] = 227.0; paper[..., 2] = 201.0
# 顶部微亮、底部微沉
grad = (yy2 / (S - 1)) * 16.0
paper -= grad[..., None]
# 纤维噪点
paper += rng.normal(0, 3.2, (S, S, 1))
img = Image.fromarray(np.clip(paper, 0, 255).astype(np.uint8)).convert('RGBA')
img.save(OUT + '/ui_paper_tex.png')
print('paper ok')

# ---------- 硬投影（9 宫格 24px 边）----------
S2 = 96
sh = Image.new('RGBA', (S2, S2), (0, 0, 0, 0))
d = ImageDraw.Draw(sh)
d.rounded_rectangle([6, 6, S2 - 7, S2 - 7], radius=14, fill=(0, 0, 0, 120))
sh = sh.filter(ImageFilter.GaussianBlur(3))
sh.save(OUT + '/ui_card_shadow.png')
print('shadow ok')

# ---------- 金光渐变条（顶部高光）----------
print('done')
