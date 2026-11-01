import math, random
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import numpy as np
from render_base import *

SKY_TOP = (245, 251, 254)
SKY_BOT = (211, 232, 245)
INK = (54, 70, 90)
INK_MUT = (142, 162, 180)
PINK = (232, 148, 172)
CORAL = (240, 126, 110)
BLUE = (62, 156, 206)
WHITE = (255, 255, 255)

def bg_gradient():
    yy, xx = np.mgrid[0:CH, 0:CW].astype(np.float32)
    t = (yy / (CH - 1))[..., None]
    arr = np.array(SKY_TOP, dtype=np.float32) * (1 - t) + np.array(SKY_BOT, dtype=np.float32) * t
    gx = ((xx - CW * 0.22) / (CW * 0.55)) ** 2
    gy = ((yy - CH * 0.10) / (CH * 0.60)) ** 2
    glow = np.clip(1.0 - np.sqrt(gx + gy), 0, 1)[..., None] ** 1.6
    arr = arr + glow * 16.0
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8)).convert('RGBA')

def sparkle(d, x, y, s, col):
    d.line([sc(x - s), sc(y), sc(x + s), sc(y)], fill=col, width=sc(1.4))
    d.line([sc(x), sc(y - s), sc(x), sc(y + s)], fill=col, width=sc(1.4))

def panel(canvas, box, r=18, alpha=208, sh=(96, 136, 168, 55), blur=18, off=(0, 8)):
    canvas.alpha_composite(soft_shadow(box, r, sh, blur, off))
    lay = layer()
    d = ImageDraw.Draw(lay)
    rrect(d, box, r, fill=WHITE + (alpha,), outline=WHITE + (235,), width=1.6)
    canvas.alpha_composite(lay)

def build():
    random.seed(3)
    canvas = bg_gradient()

    dec = layer()
    d = ImageDraw.Draw(dec)
    d.ellipse([sc(940), sc(-210), sc(1520), sc(370)], fill=(255, 255, 255, 46))
    d.ellipse([sc(1080), sc(520), sc(1600), sc(1040)], fill=(232, 148, 172, 22))
    d.ellipse([sc(-220), sc(500), sc(220), sc(940)], fill=(62, 156, 206, 18))
    d.polygon([(sc(300), sc(720)), (sc(560), sc(720)), (sc(430), sc(600)), (sc(170), sc(600))], fill=(255, 255, 255, 34))
    for i in range(14):
        x = random.uniform(30, 1260)
        y = random.uniform(30, 690)
        if 700 < x < 1010 or (30 < x < 360 and 90 < y < 700) or (1000 < x < 1260 and 90 < y < 700):
            continue
        if i % 3 == 0:
            sparkle(d, x, y, random.uniform(4, 7), (255, 255, 255, 150))
        else:
            r = random.uniform(1.5, 3)
            d.ellipse([sc(x - r), sc(y - r), sc(x + r), sc(y + r)], fill=(255, 255, 255, 160))
    canvas.alpha_composite(dec)

    panel(canvas, (372, 120, 1000, 600), r=26, alpha=225, sh=(88, 132, 166, 62), blur=22, off=(0, 12))
    panel(canvas, (24, 92, 352, 700), r=22, alpha=210)
    panel(canvas, (1012, 96, 1256, 372), r=18, alpha=208)
    panel(canvas, (1012, 386, 1256, 700), r=18, alpha=208)

    stage = cover(BG, 624, 476)
    arr = np.array(stage.convert('RGB')).astype(np.float32)
    arr = arr * 0.52 + np.array([246, 250, 253], dtype=np.float32) * 0.48
    stage = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8)).convert('RGBA')
    stage.putalpha(round_mask(stage.size, 26))
    canvas.alpha_composite(stage, (sc(374), sc(122)))

    fade = Image.new('RGBA', (sc(624), sc(476)), (0, 0, 0, 0))
    fa = np.zeros((sc(476), sc(624), 4), dtype=np.uint8)
    ys = np.arange(sc(476))
    a = np.clip((ys - sc(250)) / sc(200), 0, 1) ** 1.4 * 210
    fa[:, :, 0:3] = 250
    fa[:, :, 3] = a.astype(np.uint8)[:, None]
    fade = Image.fromarray(fa)
    fade.putalpha(Image.composite(fade.getchannel('A'), Image.new('L', fade.size, 0), round_mask(fade.size, 26)))
    canvas.alpha_composite(fade, (sc(374), sc(122)))

    d = ImageDraw.Draw(canvas)
    rrect(d, (372, 120, 1000, 600), 26, outline=WHITE + (200,), width=1.6)

    glow = layer()
    gd = ImageDraw.Draw(glow)
    gd.ellipse([sc(400), sc(520), sc(660), sc(596)], fill=(255, 255, 255, 120))
    gd.ellipse([sc(742), sc(528), sc(964), sc(592)], fill=(255, 255, 255, 110))
    canvas.alpha_composite(glow.filter(ImageFilter.GaussianBlur(sc(10))))

    hh = fit_height(HERO, 336)
    canvas.alpha_composite(hh, (int(sc(520) - hh.width / 2), sc(584) - hh.height))
    mm = fit_height(MON, 272)
    shadow_mon = Image.new('RGBA', mm.size, (30, 40, 50, 90))
    shadow_mon.putalpha(mm.getchannel('A').point(lambda v: v // 5))
    canvas.alpha_composite(shadow_mon, (int(sc(852) - mm.width / 2) + sc(4), sc(584) - mm.height + sc(4)))
    canvas.alpha_composite(mm, (int(sc(852) - mm.width / 2), sc(584) - mm.height))

    d = ImageDraw.Draw(canvas)
    pill = layer()
    pd = ImageDraw.Draw(pill)
    rrect(pd, (726, 138, 968, 172), 17, fill=WHITE + (238,))
    pd.ellipse([sc(740), sc(150), sc(748), sc(158)], fill=PINK + (255,))
    txt(pd, (758, 145), DATA['monster'], F('yhbd', 12), INK)
    txt(pd, (958, 147), DATA['mtag'], F('yhl', 9.5), INK_MUT, anchor='ra')
    canvas.alpha_composite(pill)
    rrect(d, (740, 182, 954, 190), 4, fill=(255, 255, 255, 210))
    rrect(d, (740, 182, 740 + int(214 * 0.72), 190), 4, fill=CORAL + (255,))

    chip = layer()
    cd = ImageDraw.Draw(chip)
    rrect(cd, (372, 86, 512, 112), 13, fill=WHITE + (232,))
    txt(cd, (386, 92), DATA['chapter'] + '  ' + DATA['stage'], F('yhl', 9.5), INK_MUT)
    canvas.alpha_composite(chip)

    dmg = layer()
    dd = ImageDraw.Draw(dmg)
    txt(dd, (700, 236), '4,812', F('din', 44), CORAL + (255,), anchor='mm', sh=WHITE + (235,), so=(2.5, 3))
    txt(dd, (700, 272), '暴 击', F('yhbd', 11), (236, 108, 92, 255), anchor='mm', sh=WHITE + (200,), so=(1.4, 2))
    dmg = dmg.rotate(-4, resample=Image.BICUBIC, center=(sc(700), sc(254)))
    canvas.alpha_composite(dmg)

    d = ImageDraw.Draw(canvas)
    txt(d, (40, 16), DATA['name'], F('xhei', 26), INK, sh=WHITE + (150,), so=(1, 2))
    txt_track(d, (42, 50), 'ABYSS ECHO', F('din', 9), (150, 172, 190), track=3.4)
    d.rectangle([sc(26), sc(22), sc(29), sc(52)], fill=PINK + (255,))

    bx = 760
    for j, name in enumerate(['存档', '选关', '成就', '无尽', '转生', '商店']):
        if j == 0:
            lay = layer()
            ld = ImageDraw.Draw(lay)
            rrect(ld, (bx, 22, bx + 76, 54), 16, fill=WHITE + (250,))
            canvas.alpha_composite(soft_shadow((bx, 22, bx + 76, 54), 16, (90, 130, 165, 40), 8, (0, 3)))
            canvas.alpha_composite(lay)
        txt(d, (bx + 38, 30), name, F('yh', 11.5), INK if j == 0 else (120, 142, 162), anchor='ma')
        bx += 84

    hx, hw = 24, 328
    hp = HERO.resize((sc(196), int(HERO.height * sc(196) / HERO.width)), Image.LANCZOS)
    canvas.alpha_composite(hp, (int(sc(hx + hw / 2) - hp.width / 2), sc(104)))
    txt(d, (hx + hw / 2, 368), DATA['hero'], F('xhei', 21), INK, anchor='ma')
    txt(d, (hx + hw / 2, 400), 'Lv.118   ·   巅峰 120', F('yhl', 9.5), INK_MUT, anchor='ma')

    txt(d, (hx + 34, 426), '生命', F('yhl', 9), INK_MUT)
    txt(d, (hx + 294, 426), '12,480 / 12,480', F('din', 9.5), INK, anchor='ra')
    rrect(d, (hx + 34, 442, hx + 294, 452), 5, fill=(226, 236, 244, 255))
    rrect(d, (hx + 34, 442, hx + 294, 452), 5, fill=(236, 132, 126, 245))

    for i, (k, v) in enumerate(DATA['stats']):
        cx = hx + 26 + (i % 2) * 148
        cy = 466 + (i // 2) * 42
        lay = layer()
        ld = ImageDraw.Draw(lay)
        rrect(ld, (cx, cy, cx + 138, cy + 38), 12, fill=(255, 255, 255, 150))
        canvas.alpha_composite(lay)
        txt(d, (cx + 14, cy + 6), k, F('yhl', 8.5), INK_MUT)
        txt(d, (cx + 124, cy + 12), v, F('din', 14), INK, anchor='ra')

    txt(d, (hx + 26, 552), '装 备', F('yhl', 8.5), INK_MUT)
    txt(d, (hx + 306, 552), '战力 18,420', F('yhl', 8.5), (176, 152, 96), anchor='ra')
    for i, (slot, name, plus) in enumerate(DATA['gear']):
        y = 566 + i * 19
        txt(d, (hx + 26, y), slot, F('yhl', 9), (172, 190, 204))
        txt(d, (hx + 74, y - 1), name, F('yh', 10.5), (66, 84, 104))
        txt(d, (hx + 306, y), plus, F('din', 10.5), (188, 150, 80), anchor='ra')
        d.line([(sc(hx + 26), sc(y + 15)), (sc(hx + 306), sc(y + 15))], fill=(255, 255, 255, 170), width=sc(1))

    txt(d, (1032, 112), '战 报', F('xhei', 12), INK)
    d.line([(sc(1032), sc(136)), (sc(1236), sc(136))], fill=(210, 226, 238, 255), width=sc(1))
    for i, (tag, body, kind) in enumerate(DATA['log']):
        y = 150 + i * 30
        col = {'gold': (196, 158, 78), 'red': (224, 112, 96), 'blue': (86, 148, 200), 'muted': (150, 168, 184), 'normal': (108, 128, 148)}[kind]
        d.ellipse([sc(1032), sc(y + 6), sc(1040), sc(y + 14)], fill=col + (255,))
        txt(d, (1048, y), tag, F('yhl', 8.5), col)
        txt(d, (1048, y + 12), body, F('yh', 10), (72, 90, 110))
    txt(d, (1236, 340), '全部记录 ›', F('yhl', 9), (150, 174, 194), anchor='ra')

    txt(d, (1032, 404), '本 局 数 据', F('xhei', 12), INK)
    rows = [('层数', '137 / 200'), ('击杀', '428'), ('总伤害', '1.24 M'), ('用时', '12:48'), ('最高连击', '62')]
    for i, (k, v) in enumerate(rows):
        y = 436 + i * 30
        txt(d, (1032, y), k, F('yhl', 9.5), INK_MUT)
        txt(d, (1236, y - 2), v, F('din', 12), INK, anchor='ra')
        d.line([(sc(1032), sc(y + 20)), (sc(1236), sc(y + 20))], fill=(222, 234, 244, 255), width=sc(1))

    txt(d, (1032, 594), '自动战斗', F('yhl', 9.5), INK_MUT)
    lay = layer()
    ld = ImageDraw.Draw(lay)
    rrect(ld, (1150, 588, 1236, 608), 10, fill=(216, 240, 232, 255))
    canvas.alpha_composite(lay)
    d = ImageDraw.Draw(canvas)
    d.ellipse([sc(1158), sc(593), sc(1168), sc(603)], fill=(70, 168, 130, 255))
    txt(d, (1176, 592), '开启中', F('yhl', 9), (56, 140, 110))

    txt(d, (1032, 626), '近 7 波 伤害', F('yhl', 9), INK_MUT)
    vals = [0.42, 0.55, 0.48, 0.72, 0.64, 0.88, 1.0]
    for i, v in enumerate(vals):
        bx = 1032 + i * 30
        bh = int(42 * v)
        rrect(d, (bx, 690 - bh, bx + 18, 690), 5, fill=(206, 228, 242, 255))
        rrect(d, (bx, 690 - bh, bx + 18, 690), 5, fill=(122, 182, 220, 235) if i < 6 else (232, 148, 172, 245))
    d.line([(sc(1032), sc(692)), (sc(1236), sc(692))], fill=(214, 230, 242, 255), width=sc(1))

    for i, (ic, nm, cdv) in enumerate(DATA['skills']):
        cw1, ch1 = 84, 100
        cx0 = 384 + i * 104
        box = (cx0, 612, cx0 + cw1, 612 + ch1)
        canvas.alpha_composite(soft_shadow(box, 16, (90, 132, 166, 50), 12, (0, 5)))
        lay = layer()
        ld = ImageDraw.Draw(lay)
        rrect(ld, box, 16, fill=WHITE + (222,), outline=WHITE + (240,), width=1.4)
        canvas.alpha_composite(lay)
        d = ImageDraw.Draw(canvas)
        ic_img = icon(ic).resize((sc(50), sc(50)), Image.LANCZOS)
        canvas.alpha_composite(ic_img, (sc(cx0 + 17), sc(622)))
        txt(d, (cx0 + cw1 / 2, 680), nm, F('yh', 9.5), (78, 96, 116), anchor='ma')
        if cdv < 1.0:
            cx, cy = cx0 + 42, 647
            bb = [sc(cx - 22), sc(cy - 22), sc(cx + 22), sc(cy + 22)]
            d.arc(bb, start=-90, end=-90 + 360 * cdv, fill=CORAL + (255,), width=sc(3))
            d.arc(bb, start=-90 + 360 * cdv, end=270, fill=(255, 255, 255, 220), width=sc(3))
            txt(d, (cx, cy), str(int(cdv * 100)) + '%', F('din', 11), (232, 120, 104), anchor='mm')

    canvas = noise(canvas, 1.8)
    out = canvas.convert('RGB').resize((W, H), Image.LANCZOS)
    out.save(OUT + '/ui-mockup-c-water.png')
    print('saved C', out.size)

if __name__ == '__main__':
    build()