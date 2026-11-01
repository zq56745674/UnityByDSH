import os, math, random
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import numpy as np

ROOT = 'E:/pytest/unitydsh1'
RES = ROOT + '/HelloWorldProject/Assets/Resources'
OUT = ROOT + '/docs/images'
os.makedirs(OUT, exist_ok=True)

S = 2
W, H = 1280, 720
CW, CH = W * S, H * S
FD = 'C:/Windows/Fonts/'

def sc(v): return int(round(v * S))

FPATH = {
  'zsong': FD + 'STZHONGS.TTF',
  'kai': FD + 'simkai.ttf',
  'hei': FD + 'simhei.ttf',
  'yh': FD + 'msyh.ttc',
  'yhbd': FD + 'msyhbd.ttc',
  'yhl': FD + 'msyhl.ttc',
  'xhei': FD + 'STXIHEI.TTF',
  'din': FD + 'bahnschrift.ttf',
}
_fc = {}
def F(key, size):
    k = (key, size)
    if k not in _fc:
        _fc[k] = ImageFont.truetype(FPATH[key], sc(size))
    return _fc[k]

def layer():
    return Image.new('RGBA', (CW, CH), (0, 0, 0, 0))

def put(base, lay):
    base.alpha_composite(lay)

def txt(d, xy, s, f, fill, anchor='la', sh=None, so=(1.0, 1.0)):
    x, y = sc(xy[0]), sc(xy[1])
    if sh is not None:
        d.text((x + sc(so[0]), y + sc(so[1])), s, font=f, fill=sh, anchor=anchor)
    d.text((x, y), s, font=f, fill=fill, anchor=anchor)

def txt_track(d, xy, s, f, fill, track=0.0, anchor='la', sh=None, so=(1.0, 1.0)):
    x, y = sc(xy[0]), sc(xy[1])
    widths = [f.getlength(c) for c in s]
    total = sum(widths) + sc(track) * max(0, len(s) - 1)
    if anchor == 'ma': x -= total / 2
    elif anchor == 'ra': x -= total
    for i, c in enumerate(s):
        if sh is not None:
            d.text((x + sc(so[0]), y + sc(so[1])), c, font=f, fill=sh)
        d.text((x, y), c, font=f, fill=fill)
        x += widths[i] + sc(track)

def rrect(d, box, r, fill=None, outline=None, width=1):
    x0, y0, x1, y1 = [sc(v) for v in box]
    d.rounded_rectangle([x0, y0, x1, y1], radius=sc(r), fill=fill, outline=outline, width=sc(width))

def soft_shadow(size, radius, color, blur, offset, spread=0):
    lay = Image.new('RGBA', (CW, CH), (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    x0, y0, x1, y1 = size
    d.rounded_rectangle([sc(x0 + offset[0]), sc(y0 + offset[1]), sc(x1 + offset[0]), sc(y1 + offset[1])],
                        radius=sc(radius), fill=color)
    return lay.filter(ImageFilter.GaussianBlur(sc(blur)))

def cover(img, w, h):
    iw, ih = img.size
    w, h = sc(w), sc(h)
    s = max(w / iw, h / ih)
    n = img.resize((max(1, int(iw * s)), max(1, int(ih * s))), Image.LANCZOS)
    x = (n.width - w) // 2
    y = (n.height - h) // 2
    return n.crop((x, y, x + w, y + h))

def fit_height(img, h):
    w = int(img.width * (sc(h) / img.height))
    return img.resize((max(1, w), sc(h)), Image.LANCZOS)

def round_mask(size, radius):
    m = Image.new('L', size, 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, size[0] - 1, size[1] - 1], radius=sc(radius), fill=255)
    return m

def noise(canvas, strength=6):
    arr = np.array(canvas.convert('RGB')).astype(np.int16)
    rnd = np.random.normal(0, strength, (arr.shape[0], arr.shape[1], 1))
    arr = np.clip(arr + rnd, 0, 255).astype(np.uint8)
    return Image.fromarray(arr).convert('RGBA')

def vignette(canvas, color, max_alpha, power=2.4):
    yy, xx = np.mgrid[0:CH, 0:CW]
    cx, cy = CW / 2, CH / 2
    d = np.sqrt(((xx - cx) / cx) ** 2 + ((yy - cy) / cy) ** 2) / 1.414
    a = np.clip((d ** power) * max_alpha, 0, 255).astype(np.uint8)
    lay = Image.new('RGBA', (CW, CH), color + (0,))
    lay.putalpha(Image.fromarray(a))
    canvas.alpha_composite(lay)

HERO = Image.open(RES + '/HeroPortrait.png').convert('RGBA')
MON = Image.open(RES + '/Sprites/Monsters/goblin_melee.png').convert('RGBA')
BG = Image.open(RES + '/Sprites/Backgrounds/bg_wilds.jpg').convert('RGB')
ICO = {}
def icon(name):
    if name not in ICO:
        ICO[name] = Image.open(RES + '/Icons/' + name + '.png').convert('RGBA')
    return ICO[name]

DATA = {
  'name': '深渊回响',
  'hero': '无名旅者',
  'lv': 118,
  'paragon': 120,
  'hp': 12480,
  'hpmax': 12480,
  'exp': 0.62,
  'gold': '3,845,210',
  'stats': [('攻击', '1,284'), ('防御', '692'), ('速度', '1.36'), ('暴击', '38.5%')],
  'chapter': '第 5 章 · 荒野',
  'stage': '5-7',
  'monster': '石鳞哥布林',
  'mtag': '精英 · Lv.124',
  'gear': [('武器', '破军之刃', '+9'), ('衣服', '幽影皮甲', '+7'), ('头盔', '铁箍盔', '+5'),
           ('战靴', '疾风战靴', '+6'), ('饰品', '焦石护符', '+4')],
  'skills': [('skill_whirlwind', '旋风斩', 1.0), ('skill_iron_body', '铁躯', 0.72),
             ('skill_berserk', '狂暴', 1.0), ('skill_execute', '处决', 0.45),
             ('skill_crit_chain', '连锁暴击', 1.0), ('skill_swiftness', '疾风', 0.88)],
  'log': [('拾取', '稀有 · 破军之刃', 'gold'), ('暴击', '4,812 伤害', 'red'),
          ('状态', '石鳞哥布林 获得 剧毒', 'muted'), ('击杀', 'Lv.124 石鳞哥布林', 'normal'),
          ('升级', 'Lv.118 → 119', 'blue'), ('成就', '百战·解锁', 'gold')],
}
