"""把选中的鲸鱼娘立绘处理成可用的透明 PNG。

两步：
1. 裁掉底部那条深灰色斜带（它左边起点高、右边低，取 y=1010 保守裁，够保住领口）。
2. 去掉白底。直接按「接近白」做阈值会把人物内部的白头饰、白领子一起删掉，
   所以只把**与图像四边连通**的白色区域判为背景（BFS 泛洪），内部的白色保留。

另出一版边缘径向淡出的水印版，供面板底纹用。
"""

from PIL import Image, ImageChops, ImageDraw, ImageFilter
from collections import deque
import numpy as np
import os

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "fish_hi.webp")
CUT_Y = 1010


def cut_bottom(im):
    return im.crop((0, 0, im.width, min(CUT_Y, im.height)))


def drop_white_bg(im, white_thresh=232):
    arr = np.array(im.convert("RGBA"))
    h, w = arr.shape[:2]
    white = (arr[:, :, 0] > white_thresh) & \
            (arr[:, :, 1] > white_thresh) & \
            (arr[:, :, 2] > white_thresh)

    bg = np.zeros((h, w), dtype=bool)
    q = deque()

    def push(y, x):
        if white[y, x] and not bg[y, x]:
            bg[y, x] = True
            q.append((y, x))

    for x in range(w):
        push(0, x)
        push(h - 1, x)
    for y in range(h):
        push(y, 0)
        push(y, w - 1)

    while q:
        y, x = q.popleft()
        if y > 0: push(y - 1, x)
        if y < h - 1: push(y + 1, x)
        if x > 0: push(y, x - 1)
        if x < w - 1: push(y, x + 1)

    # 半透明羽化：对背景边缘做一点模糊再乘，避免锯齿和白边
    mask = np.where(bg, 0, 255).astype(np.uint8)
    mask_img = Image.fromarray(mask, "L").filter(ImageFilter.GaussianBlur(1.2))
    alpha = ImageChops.multiply(im.convert("RGBA").getchannel("A"), mask_img)

    out = im.convert("RGBA").copy()
    out.putalpha(alpha)
    return out


def with_radial_fade(img, blur_ratio=0.10):
    w, h = img.size
    mask = Image.new("L", (w, h), 0)
    inset = int(min(w, h) * 0.02)
    ImageDraw.Draw(mask).ellipse((inset, inset, w - inset, h - inset), fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(min(w, h) * blur_ratio))
    out = img.copy()
    out.putalpha(ImageChops.multiply(out.getchannel("A"), mask))
    return out


def main():
    im = Image.open(SRC).convert("RGBA")
    print("src", im.size)
    im = cut_bottom(im)
    print("after cut", im.size)
    clean = drop_white_bg(im)
    clean.save(os.path.join(HERE, "hero_clean.png"))
    with_radial_fade(clean).save(os.path.join(HERE, "hero_clean_fade.png"))
    print("saved hero_clean.png + hero_clean_fade.png", clean.size)


if __name__ == "__main__":
    main()
