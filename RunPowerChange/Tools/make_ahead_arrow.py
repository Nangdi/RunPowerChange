# -*- coding: utf-8 -*-
"""
85형 "화면 밖으로 나간 레인" 표시의 위쪽 화살표(▲) 그림 만들기. 흰색으로 그리고 Unity 에서 레인 색으로 칠한다.
디자이너 그림을 받으면 같은 파일명으로 덮어쓴다.

  python Tools/make_ahead_arrow.py

출력: Assets/4.Sprite/Parts/Spectator_AheadArrow.png (96x64, 흰 삼각형, 모서리 살짝 둥글게)
필요: pip install pillow
"""
import os

from PIL import Image, ImageDraw, ImageFilter

PROJECT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUT = os.path.join(PROJECT, "Assets", "4.Sprite", "Parts", "Spectator_AheadArrow.png")

W, H = 96, 64
SCALE = 8   # 크게 그린 뒤 줄여 가장자리를 매끄럽게


def main():
    big = Image.new("L", (W * SCALE, H * SCALE), 0)
    m = 4 * SCALE
    ImageDraw.Draw(big).polygon([(W * SCALE / 2, m), (W * SCALE - m, H * SCALE - m), (m, H * SCALE - m)], fill=255)
    # 모서리를 살짝 둥글게: 흐리게 한 뒤 다시 날카롭게 자른다
    big = big.filter(ImageFilter.GaussianBlur(3 * SCALE)).point(lambda v: 255 if v > 128 else 0)
    alpha = big.resize((W, H), Image.LANCZOS)
    out = Image.new("RGBA", (W, H), (255, 255, 255, 0))
    out.putalpha(alpha)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    out.save(OUT)
    print(OUT, out.size)


if __name__ == "__main__":
    main()
