# -*- coding: utf-8 -*-
"""
85형 관전 화면 안내 카드 부품(임시 그림) 만들기. 디자이너 시안에 없는 화면이라, 기존 체험 안내 카드(반투명 흰 카드 + 흰 테두리 빛)와
STEP 배지(남색 알약)와 같은 모양으로 코드로 그린다. 디자이너 그림을 받으면 같은 파일명으로 덮어쓴다 (9-slice 테두리는 PowerSpriteImporter 가 정함).

  python Tools/make_spectator_notice_parts.py          없는 파일만 만든다
  python Tools/make_spectator_notice_parts.py --force  모두 다시 만든다

출력: Assets/4.Sprite/Parts/Spectator_NoticeCard.png (9-slice, 테두리 110px)
      Assets/4.Sprite/Parts/Spectator_NoticePill.png (9-slice, 테두리 50px)
필요: pip install pillow
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFilter

PROJECT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUT = os.path.join(PROJECT, "Assets", "4.Sprite", "Parts")

NAVY = (52, 55, 90)  # PowerUIKit.TextColor


def rounded(size, box, radius, fill):
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    ImageDraw.Draw(img).rounded_rectangle(box, radius, fill=fill)
    return img


def card():
    # 300x300, 바깥 30px 은 흰 빛 번짐. 카드 몸통은 반투명 흰색(뒤 3D 트랙이 살짝 비침), 가장자리는 조금 더 밝게.
    size, margin, radius = (300, 300), 30, 80
    box = (margin, margin, size[0] - margin - 1, size[1] - margin - 1)
    glow = rounded(size, box, radius, (255, 255, 255, 150)).filter(ImageFilter.GaussianBlur(14))
    body = rounded(size, box, radius, (236, 238, 244, 226))
    rim = rounded(size, box, radius, (255, 255, 255, 255))
    inner = rounded(size, (box[0] + 5, box[1] + 5, box[2] - 5, box[3] - 5), radius - 5, (255, 255, 255, 255))
    # 테두리 5px 만 남긴다
    rim.putalpha(Image.eval(Image.composite(Image.new("L", size, 0), rim.getchannel("A"), inner.getchannel("A")), lambda a: a * 200 // 255))
    out = Image.alpha_composite(glow, body)
    return Image.alpha_composite(out, rim)


def pill():
    # 200x100 남색 알약 (STEP 배지와 같은 색)
    return rounded((200, 100), (0, 0, 199, 99), 50, NAVY + (255,))


def save(name, img, force):
    path = os.path.join(OUT, name)
    if os.path.exists(path) and not force:
        print("skip", name)
        return
    img.save(path)
    print("write", name, img.size)


def main():
    force = "--force" in sys.argv
    os.makedirs(OUT, exist_ok=True)
    save("Spectator_NoticeCard.png", card(), force)
    save("Spectator_NoticePill.png", pill(), force)


if __name__ == "__main__":
    main()
