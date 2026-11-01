import math, random
from PIL import Image, ImageDraw, ImageFont, ImageFilter
from render_base import *

WOOD = (24, 18, 13)
PARCH = (236, 224, 195)
PARCH_DK = (214, 198, 162)
INK = (43, 31, 20)
INK_MUT = (116, 96, 68)
GOLD = (198, 158, 86)
GOLD_DK = (116, 84, 38)
RED = (162, 52, 36)
GREEN = (78, 108, 60)
BLUE = (58, 86, 120)
LEATHER = (46, 35, 26)

def parchment(w1, h1, radius=6, base=PARCH, dark=PARCH_DK):
    w, h = sc(w1), sc(h1)
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    arr = np.zeros((h, w, 3), dtype=np.uint8)
    for y in range(h):
        t = y / max(1, h - 1)
        c = tuple(int(base[i] * (1 - t * 0.35) + dark[i] * (t * 0.35)) for i in range(3))
        arr[y, :, :] = c
    noise_arr = np.random.normal(0, 4.0, (h, w, 1))
    arr = np.clip(arr.astype(np.int16) + noise_arr, 0, 255).astype(np.uint8)
    body = Image.fromarray(arr).convert('RGBA')
    mask = Image.new('L', (w, h), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, w - 1, h - 1], radius=sc(radius), fill=255)
    img.paste(body, (0, 0), mask)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=sc(radius), outline=(120, 92, 52, 200), width=sc(1.3))
    d.line([(sc(radius), sc(1.6)), (w - sc(radius), sc(1.6))], fill=(255, 248, 228, 90), width=sc(1))
    return img

def paste_rot(canvas, img, x1, y1, w1, h1, angle=0.0, sh=(2.0, 4.0), sh_a=135):
    cx, cy = sc(x1 + w1 / 2), sc(y1 + h1 / 2)
    alpha = img.getchannel('A')
    black = Image.new('RGBA', img.size, (0, 0, 0, 255))
    black.putalpha(alpha.point(lambda a: a * sh_a // 255))
    ri = img.rotate(angle, resample=Image.BICUBIC, expand=True)
    rb = black.rotate(angle, resample=Image.BICUBIC, expand=True)
    canvas.alpha_composite(rb, (int(cx - ri.width / 2 + sc(sh[0])), int(cy - ri.height / 2 + sc(sh[1]))))
    canvas.alpha_composite(ri, (int(cx - ri.width / 2), int(cy - ri.height / 2)))

def coin(size1):
    s = sc(size1)
    img = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([0, 0, s - 1, s - 1], fill=(214, 168, 62, 255), outline=(120, 78, 20, 255), width=sc(1.2))
    d.ellipse([sc(2.5), sc(2.5), s - sc(2.5), s - sc(2.5)], outline=(252, 232, 168, 200), width=sc(1))
    return img

def build():
    random.seed(11)
    canvas = Image.new('RGBA', (CW, CH), WOOD + (255,))
    grain = layer()
    gd = ImageDraw.Draw(grain)
    for i in range(160):
        y = random.randint(0, CH - 1)
        a = random.randint(4, 14)
        gd.line([(0, y), (CW, y + random.randint(-6, 6))], fill=(255, 235, 200, a), width=sc(random.choice([1, 1, 2])))
    canvas.alpha_composite(grain)
    vignette(canvas, (0, 0, 0), 210, 2.2)

    top = layer()
    d = ImageDraw.Draw(top)
    txt(d, (22, 7), DATA['name'], F('kai', 23), GOLD, sh=(0, 0, 0, 190), so=(1.5, 2))
    txt(d, (24, 37), '第 5 章 · 荒野    5-7', F('yh', 10), (188, 168, 132))
    bx = 872
    for name in ['存档', '选关', '成就', '无尽', '转生', '商店']:
        rrect(d, (bx, 8, bx + 60, 38), 5, fill=LEATHER + (235,), outline=(120, 92, 52, 210), width=1.2)
        txt(d, (bx + 30, 23), name, F('yh', 12), (222, 205, 172), anchor='mm')
        bx += 66
    canvas.alpha_composite(top)

    sheet = parchment(392, 654, radius=8)
    paste_rot(canvas, sheet, 12, 54, 392, 654, angle=-0.5, sh=(4, 7), sh_a=150)
    d = ImageDraw.Draw(canvas)
    ox, oy = 12, 54
    txt(d, (ox + 16, oy + 12), '角色', F('yhbd', 11), INK_MUT)
    txt(d, (ox + 16, oy + 26), DATA['hero'], F('zsong', 23), INK, sh=(255, 255, 255, 70), so=(0, 1))
    txt(d, (ox + 16, oy + 60), '巅峰 120  ·  Lv.118', F('yh', 10.5), INK_MUT)

    ph = HERO.resize((sc(120), int(HERO.height * sc(120) / HERO.width)), Image.LANCZOS)
    photo = Image.new('RGBA', (sc(128), sc(152)), (0, 0, 0, 0))
    pd = ImageDraw.Draw(photo)
    pd.rectangle([0, 0, sc(128) - 1, sc(152) - 1], fill=(250, 243, 226, 255), outline=(140, 110, 62, 220), width=sc(1.4))
    inner = ph.crop((0, 0, ph.width, min(ph.height, sc(140))))
    photo.alpha_composite(inner, (sc(4), sc(6)))
    paste_rot(canvas, photo, ox + 14, oy + 80, 128, 152, angle=1.4, sh=(3, 5), sh_a=120)

    sx = ox + 156
    for i, (k, v) in enumerate(DATA['stats']):
        y = oy + 84 + i * 24
        txt(d, (sx, y), k, F('yh', 11), INK_MUT)
        txt(d, (ox + 380, y), v, F('din', 13), INK, anchor='ra')

    txt(d, (ox + 16, oy + 238), '生命', F('yh', 10.5), INK_MUT)
    txt(d, (ox + 380, oy + 238), '12,480 / 12,480', F('din', 11), INK, anchor='ra')
    rrect(d, (ox + 16, oy + 252, ox + 380, oy + 266), 5, fill=(198, 178, 142, 255), outline=(120, 92, 52, 160), width=1)
    rrect(d, (ox + 17, oy + 253, ox + 379, oy + 265), 4, fill=(150, 44, 32, 255))

    d.line([(sc(ox + 16), sc(oy + 284)), (sc(ox + 380), sc(oy + 284))], fill=(120, 92, 52, 120), width=sc(1))
    txt(d, (ox + 16, oy + 292), '装备', F('yhbd', 11), INK_MUT)
    txt(d, (ox + 380, oy + 293), '战力 18,420', F('yh', 10.5), GOLD_DK, anchor='ra')
    for i, (slot, name, plus) in enumerate(DATA['gear']):
        y = oy + 314 + i * 30
        rrect(d, (ox + 14, y - 4, ox + 380, y + 22), 4, fill=(226, 212, 180, 205), outline=(150, 122, 78, 90), width=1)
        txt(d, (ox + 24, y + 1), slot, F('yh', 10), INK_MUT)
        txt(d, (ox + 76, y), name, F('yh', 12), INK)
        txt(d, (ox + 370, y), plus, F('din', 12), GOLD_DK, anchor='ra')

    d.line([(sc(ox + 16), sc(oy + 466)), (sc(ox + 380), sc(oy + 466))], fill=(120, 92, 52, 120), width=sc(1))
    canvas.alpha_composite(coin(19), (sc(ox + 16), sc(oy + 474)))
    txt(d, (ox + 42, oy + 471), DATA['gold'], F('din', 21), (150, 98, 26))
    txt(d, (ox + 16, oy + 508), '经验', F('yh', 10), INK_MUT)
    txt(d, (ox + 380, oy + 509), '62%', F('din', 10), INK_MUT, anchor='ra')
    rrect(d, (ox + 16, oy + 522, ox + 380, oy + 530), 4, fill=(198, 178, 142, 255), outline=(120, 92, 52, 150), width=1)
    rrect(d, (ox + 17, oy + 523, ox + 16 + int(362 * 0.62), oy + 529), 3, fill=(84, 62, 116, 255))

    txt(d, (ox + 16, oy + 556), '距下一章还剩 4 波', F('yh', 10), INK_MUT)
    rrect(d, (ox + 16, oy + 578, ox + 132, oy + 604), 5, fill=(232, 222, 196, 220), outline=(120, 92, 52, 160), width=1.2)
    d.ellipse([sc(ox + 25), sc(oy + 587), sc(ox + 33), sc(oy + 595)], fill=(88, 118, 66, 255))
    txt(d, (ox + 40, oy + 584), '自动战斗 · 开', F('yh', 10), (62, 82, 50))

    seal = Image.new('RGBA', (sc(62), sc(62)), (0, 0, 0, 0))
    sd = ImageDraw.Draw(seal)
    sd.ellipse([0, 0, sc(62) - 1, sc(62) - 1], fill=(150, 44, 32, 235), outline=(96, 26, 18, 255), width=sc(2))
    sd.ellipse([sc(6), sc(6), sc(56), sc(56)], outline=(240, 214, 190, 120), width=sc(1))
    sd.text((sc(31), sc(26)), '第', font=F('zsong', 14), fill=(250, 232, 214), anchor='mm')
    sd.text((sc(31), sc(43)), '五世', font=F('zsong', 11), fill=(250, 232, 214), anchor='mm')
    paste_rot(canvas, seal, ox + 300, oy + 566, 62, 62, angle=-9, sh=(2, 3), sh_a=90)

    plate = layer()
    pp = ImageDraw.Draw(plate)
    pts = [(sc(432), sc(56)), (sc(1252), sc(56)), (sc(1240), sc(100)), (sc(444), sc(100))]
    pp.polygon(pts, fill=LEATHER + (242,))
    pp.line([(sc(432), sc(56)), (sc(1252), sc(56))], fill=(196, 158, 86, 220), width=sc(1.6))
    pp.line([(sc(444), sc(100)), (sc(1240), sc(100))], fill=(196, 158, 86, 200), width=sc(1.4))
    pp.line([(sc(1032), sc(56)), (sc(1042), sc(100))], fill=(120, 92, 52, 160), width=sc(1))
    txt(pp, (452, 67), DATA['monster'], F('kai', 19), (238, 222, 190))
    txt(pp, (1046, 75), DATA['mtag'], F('yh', 11), (206, 184, 148))
    canvas.alpha_composite(plate)

    frame = Image.new('RGBA', (sc(852), sc(358)), (0, 0, 0, 0))
    stage = cover(BG, 852, 358)
    arr = np.array(stage.convert('RGB')).astype(np.float32) * 0.78
    stage = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8)).convert('RGBA')
    frame.paste(stage, (0, 0))
    canvas.alpha_composite(frame, (sc(416), sc(128)))
    d = ImageDraw.Draw(canvas)
    d.rectangle([sc(416), sc(128), sc(1268), sc(486)], outline=(150, 112, 52, 255), width=sc(3))
    d.rectangle([sc(419), sc(131), sc(1265), sc(483)], outline=(60, 44, 24, 200), width=sc(1))

    txt(d, (620, 106), '血量 72%', F('yh', 10), (206, 176, 140))
    rrect(d, (690, 104, 1240, 116), 5, fill=(20, 14, 10, 210), outline=(150, 112, 52, 170), width=1.2)
    rrect(d, (692, 106, 692 + int(546 * 0.72), 114), 4, fill=(150, 44, 32, 255))

    floor = layer()
    fd = ImageDraw.Draw(floor)
    fd.ellipse([sc(560), sc(438), sc(760), sc(476)], fill=(0, 0, 0, 110))
    fd.ellipse([sc(936), sc(442), sc(1100), sc(474)], fill=(0, 0, 0, 110))
    canvas.alpha_composite(floor)

    hh = fit_height(HERO, 296)
    canvas.alpha_composite(hh, (int(sc(658) - hh.width / 2), sc(470) - hh.height))
    mm = fit_height(MON, 248)
    canvas.alpha_composite(mm, (int(sc(1018) - mm.width / 2), sc(470) - mm.height))

    d = ImageDraw.Draw(canvas)
    rrect(d, (556, 458, 760, 470), 5, fill=(20, 14, 10, 200), outline=(150, 112, 52, 170), width=1.2)
    rrect(d, (558, 460, 558 + int(200 * 0.55), 468), 4, fill=(150, 44, 32, 255))

    dmg = layer()
    dd = ImageDraw.Draw(dmg)
    txt(dd, (1042, 168), '4,812', F('din', 54), (240, 96, 62), anchor='mm', sh=(28, 12, 6, 235), so=(3, 4))
    txt(dd, (1042, 214), '暴 击', F('yhbd', 12), (252, 222, 152), anchor='mm', sh=(56, 18, 8, 225), so=(1.5, 2))
    dmg = dmg.rotate(-5, resample=Image.BICUBIC, center=(sc(1042), sc(192)))
    canvas.alpha_composite(dmg)

    txt(ImageDraw.Draw(canvas), (420, 495), '技能', F('yhbd', 10), (198, 176, 140))
    for i, (ic, nm, cd) in enumerate(DATA['skills']):
        cw1, ch1 = 74, 92
        cx0 = 432 + i * 92
        card = parchment(cw1, ch1, radius=5)
        cdw = ImageDraw.Draw(card)
        ic_img = icon(ic).resize((sc(46), sc(46)), Image.LANCZOS)
        card.alpha_composite(ic_img, (sc(14), sc(8)))
        cdw.line([(sc(12), sc(60)), (sc(62), sc(60))], fill=(150, 122, 78, 140), width=sc(1))
        cdw.text((sc(37), sc(73)), nm, font=F('yh', 10), fill=INK, anchor='mm')
        if cd < 1.0:
            ov = Image.new('RGBA', card.size, (30, 22, 14, 112))
            m = Image.new('L', card.size, 0)
            ImageDraw.Draw(m).rounded_rectangle([0, 0, sc(cw1) - 1, sc(ch1) - 1], radius=sc(5), fill=255)
            card.paste(ov, (0, 0), m)
            cdw = ImageDraw.Draw(card)
            cdw.ellipse([sc(16), sc(10), sc(58), sc(52)], fill=(24, 17, 11, 200), outline=(196, 158, 86, 190), width=sc(1.2))
            cdw.text((sc(37), sc(30)), str(int(cd * 100)) + '%', font=F('din', 14), fill=(248, 232, 200), anchor='mm')
            cdw.text((sc(37), sc(73)), nm, font=F('yh', 10), fill=(232, 216, 186), anchor='mm')
        paste_rot(canvas, card, cx0, 510, cw1, ch1, angle=(-1.6 + i * 0.6), sh=(2, 4), sh_a=130)

    log = parchment(268, 212, radius=6)
    lg = ImageDraw.Draw(log)
    lg.text((sc(14), sc(10)), '战 报', font=F('yhbd', 11), fill=INK_MUT)
    for i, (tag, body, kind) in enumerate(DATA['log']):
        y = sc(32 + i * 29)
        col = {'gold': (146, 96, 26), 'red': (162, 52, 36), 'blue': (52, 78, 116), 'muted': (120, 100, 74), 'normal': (70, 56, 38)}[kind]
        lg.text((sc(14), y), '[' + tag + ']', font=F('yh', 9.5), fill=col)
        lg.text((sc(64), y), body, font=F('yh', 10.5), fill=(58, 44, 30))
    paste_rot(canvas, log, 998, 494, 268, 212, angle=0.4, sh=(3, 5), sh_a=140)

    canvas = noise(canvas, 2.6)
    vignette(canvas, (0, 0, 0), 110, 3.2)
    out = canvas.convert('RGB').resize((W, H), Image.LANCZOS)
    out.save(OUT + '/ui-mockup-b-card.png')
    print('saved B', out.size)

if __name__ == '__main__':
    build()