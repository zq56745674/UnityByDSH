from PIL import Image, ImageChops, ImageDraw, ImageFilter
import numpy as np

src = r"E:\pytest\unitydsh1\_art\whale_girl_stand_alpha.png"
out = r"E:\pytest\unitydsh1\_art"

im = Image.open(src).convert("RGBA")
a = np.array(im)[:, :, 3]
ys, xs = np.where(a > 8)
pad = 24
box = (max(0, xs.min()-pad), max(0, ys.min()-pad),
       min(im.width, xs.max()+1+pad), min(im.height, ys.max()+1+pad))
im = im.crop(box)
print("cropped:", im.size, "ratio %.3f" % (im.width / im.height))

# 高度 1024，宽度按比例
h = 1024
w = round(im.width * h / im.height)
im = im.resize((w, h), Image.LANCZOS)
print("resized:", im.size)

im.save(out + r"\hero_v2.png")

# 边缘径向淡出（面板底纹用）
mask = Image.new("L", im.size, 0)
inset_x, inset_y = int(im.width * 0.07), int(im.height * 0.06)
ImageDraw.Draw(mask).ellipse((inset_x, inset_y, im.width-inset_x, im.height-inset_y), fill=255)
mask = mask.filter(ImageFilter.GaussianBlur(im.width * 0.07))
fade = im.copy()
fade.putalpha(ImageChops.multiply(fade.getchannel("A"), mask))
fade.save(out + r"\hero_v2_fade.png")

# 顺手拼一张白底+棋盘格预览，肉眼确认边缘
bg = Image.new("RGBA", im.size, (255, 255, 255, 255))
Image.alpha_composite(bg, im).convert("RGB").resize((w//3, h//3), Image.LANCZOS).save(out + r"\_preview_white.png")
print("saved hero_v2.png / hero_v2_fade.png / _preview_white.png")
