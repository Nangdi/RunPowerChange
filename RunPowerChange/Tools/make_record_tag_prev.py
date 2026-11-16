# -*- coding: utf-8 -*-
"""
"전날 최고 랭킹" 표시 그림 만들기. 오늘 기록이 없어 1위 기록 선이 전날 1위일 때 "오늘 최고 랭킹" 대신 쓴다.
디자이너 그림(Spectator_RecordTag.png)의 알약 속 글자만 지우고 같은 크기·굵기로 "전날 최고 랭킹"을 다시 쓴다.
디자이너가 전날 그림을 주면 같은 파일명으로 덮어쓴다.

  python Tools/make_record_tag_prev.py

입력: Assets/4.Sprite/Parts/Spectator_RecordTag.png
출력: Assets/4.Sprite/Parts/Spectator_RecordTag_Prev.png
필요: pip install pillow numpy
"""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

PROJECT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
PARTS = os.path.join(PROJECT, "Assets", "4.Sprite", "Parts")
FONT = os.path.join(PROJECT, "Assets", "10.Font", "Paperlogy", "Paperlogy-7Bold.ttf")
SOURCE_TEXT = "오늘 최고 랭킹"
TEXT = "전날 최고 랭킹"


def text_box(a):
    """알약 위 흰 글자 범위 (x0, y0, x1, y1)"""
    white = (a[..., 0] > 235) & (a[..., 1] > 235) & (a[..., 2] > 235) & (a[..., 3] > 200)
    ys, xs = np.where(white)
    return xs.min(), ys.min(), xs.max(), ys.max()


def main():
    src = Image.open(os.path.join(PARTS, "Spectator_RecordTag.png")).convert("RGBA")
    a = np.array(src).astype(np.float32)
    x0, y0, x1, y1 = text_box(a)

    # 글자와 그 그림자 지우기: 글자 위아래 줄(그림자 바깥)의 색·투명도를 세로로 이어 채우고, 좌우 끝은 부드럽게 섞는다
    my, mx, feather = 13, 18, 12
    top, bottom = y0 - my, y1 + my
    left, right = x0 - mx, x1 + mx
    t = np.linspace(0, 1, bottom - top + 1)[:, None, None]
    fill = a[top, left:right + 1][None] * (1 - t) + a[bottom, left:right + 1][None] * t
    xs = np.arange(left, right + 1)
    w = np.clip(np.minimum(xs - left, right - xs) / feather, 0, 1)[None, :, None]
    region = a[top:bottom + 1, left:right + 1]
    a[top:bottom + 1, left:right + 1] = region * (1 - w) + fill * w
    out = Image.fromarray(np.clip(a + 0.5, 0, 255).astype(np.uint8), "RGBA")

    # 원래 글자 폭에 맞는 크기를 찾아 같은 자리(가운데)에 쓴다
    width = x1 - x0 + 1
    size = 10
    while ImageFont.truetype(FONT, size + 1).getbbox(SOURCE_TEXT)[2] - ImageFont.truetype(FONT, size + 1).getbbox(SOURCE_TEXT)[0] <= width:
        size += 1
    font = ImageFont.truetype(FONT, size)
    l, tp, r, b = font.getbbox(TEXT)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    pos = (cx - (l + r) / 2, cy - (tp + b) / 2)
    # 원래 그림처럼 글자 뒤에 옅은 그림자
    shadow = Image.new("RGBA", out.size, (0, 0, 0, 0))
    ImageDraw.Draw(shadow).text((pos[0], pos[1] + 2), TEXT, font=font, fill=(40, 70, 10, 150))
    out.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(5)))
    ImageDraw.Draw(out).text(pos, TEXT, font=font, fill=(255, 255, 255, 255))

    path = os.path.join(PARTS, "Spectator_RecordTag_Prev.png")
    out.save(path)
    print(path, out.size, "font", size)


if __name__ == "__main__":
    main()
