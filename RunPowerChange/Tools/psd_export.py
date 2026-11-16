# -*- coding: utf-8 -*-
"""
디자이너 PSD 2개에서 Unity용 이미지를 뽑는 도구.

  python Tools/psd_export.py "<32형 PSD 경로>" "<85형 PSD 경로>"
    32형: 03-달려라 전력챌린지-프로그램용(수정-1110).psd (1920x1080 가로)
    85형: 03-달려라 전력챌린지(80인치화면)-프로그램용.psd (2160x3840 세로)

출력 (모두 같은 경로, 같은 파일명으로 덮어쓴다 → Unity 연결(GUID) 유지)
  Assets/4.Sprite/Static/Static_<패널>.png   화면별 정적 레이어 (화면 전체 크기, 배경 투명 - 뒤에 3D 장면)
  Assets/4.Sprite/Parts/<부품>.png            동적 요소 이미지 (투명 여백을 잘라냄)
  Assets/4.Sprite/_Layout/psd_layout.json     부품/텍스트 자리의 PSD 좌표 (에디터 프리팹 생성 도구가 읽음)
  Assets/StreamingAssets/Spectator/info_n.png  85형 아래 칸 설명 그림 (실행 중 읽음, 현장 교체용)

배경 샘플 레이어(3D 장면 캡처)는 뽑지 않는다. 실행 화면에서는 Unity 3D 장면이 그 자리에 보인다.
필요: pip install psd-tools numpy
"""
import json
import os
import sys

from PIL import Image
from psd_tools import PSDImage

PROJECT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
SPRITE_DIR = os.path.join(PROJECT, "Assets", "4.Sprite")
STATIC_DIR = os.path.join(SPRITE_DIR, "Static")
PARTS_DIR = os.path.join(SPRITE_DIR, "Parts")
LAYOUT_PATH = os.path.join(SPRITE_DIR, "_Layout", "psd_layout.json")
# 85형 아래 칸 설명 그림은 현장에서 바꿀 수 있는 콘텐츠라 StreamingAssets 에도 둔다 (실행 중 이 파일을 읽음)
SPECTATOR_INFO_DIR = os.path.join(PROJECT, "Assets", "StreamingAssets", "Spectator")

# ── 32형 PSD ─────────────────────────────────────────
G1 = "01-대기화면"
G2 = "02-참가대기"
G2A = f"{G2}/01-먼저 참가한 체험자 화면"
G2B = f"{G2}/02-참가 대기화면"
G2C = f"{G2}/03-모두 준비완료"
G4 = "04-카운트"
G5 = "05-게임화면"
G5W = f"{G5}/경고문구(추가)"
G6 = "06-고,저전압 선택화면"
G7 = "07-전력판매 화면"
G7C = f"{G7}/중앙-순서대로 나오게"
G8 = "08-랭킹화면"

STATICS = {
    "Idle": [f"{G1}/그림자 레이어", f"{G1}/타이틀", f"{G1}/달려서 전기를 만들고 송전하여 더 많은 전기를 판매해보세요!"],
    "Join": [f"{G2}/그림자 레이어", f"{G2}/1~2p 배경", f"{G2}/타이틀"],
    # 자막 = "※ 양쪽 손잡이를 모두 잡아야 전력이 생산됩니다!" + 손잡이 그림 (1110 수정본에서 체험안내 화면을 없애고 여기로 옮김)
    "Countdown": [f"{G4}/그림자 레이어", f"{G4}/타이틀", f"{G4}/자막"],
    "Run": [f"{G5}/자막", f"{G5}/타임/타임/<Group>", f"{G5}/속도계/배경1", f"{G5}/속도계/배경2",
            f"{G5}/하단게이지/배경", f"{G5}/하단게이지/데코"],
    "Voltage": [f"{G6}/그림자 레이어", f"{G6}/하단자막", f"{G6}/상단자막", f"{G6}/타이틀",
                f"{G6}/01-발전량 및 랭킹/오늘의 발전왕 순위", f"{G6}/타임게이지(5~10초)/게이지 배경"],
    "Sale": [f"{G7}/그림자 레이어", f"{G7}/하단자막", f"{G7}/타이틀", f"{G7}/타임 게이지/게이지 배경"],
    "Ranking": [f"{G8}/그림자 레이어", f"{G8}/하단자막", f"{G8}/타이틀", f"{G8}/발전왕 순위/배경", f"{G8}/판매왕 순위/배경",
                f"{G8}/타임게이지(5~10초)/게이지 배경"],
}

PARTS = {
    "Button_Start": [f"{G1}/버튼"],

    "Join_Msg_Wait": [f"{G2A}/자막1"],
    "Join_Sub_Wait": [f"{G2A}/자막2"],
    "Join_Msg_Invite": [f"{G2B}/자막1"],
    "Join_Sub_Invite": [f"{G2B}/자막2"],
    "Join_Button": [f"{G2B}/버튼그림자", f"{G2B}/버튼"],
    "Join_Msg_Ready": [f"{G2C}/자막1"],
    "Join_Ring": [f"{G2A}/카운터 원형(회전)"],
    "Badge_Ready_1": [f"{G2}/1P (래디-웨이트)/레디"],
    "Badge_Wait_1": [f"{G2}/1P (래디-웨이트)/웨이트"],
    "Badge_Ready_2": [f"{G2}/2P (래디-웨이트)/레디"],
    "Badge_Wait_2": [f"{G2}/2P (래디-웨이트)/웨이트"],

    # 화면 하단 남은 시간 바. 1110 수정본에서 03-체험안내가 빠져 같은 그림인 06 화면 게이지에서 뽑는다.
    "Auto_Gauge": [f"{G6}/타임게이지(5~10초)/게이지"],

    "Count_3": [f"{G4}/3"],
    "Count_2": [f"{G4}/2"],
    "Count_1": [f"{G4}/1"],
    "Count_Go": [f"{G4}/go"],

    "Run_TimerRing": [f"{G5}/타임/타임/타임바(반시계방향 없어지게)"],
    "Run_Needle": [f"{G5}/속도계/바늘"],
    "Tag_L_Me": [f"{G5}/플레이어1(시작후 2초뒤 없어지게)/자신"],
    "Tag_L_Other": [f"{G5}/플레이어1(시작후 2초뒤 없어지게)/상대"],
    "Tag_R_Me": [f"{G5}/플레이어2(시작후 2초뒤 없어지게)/자신"],
    "Tag_R_Other": [f"{G5}/플레이어2(시작후 2초뒤 없어지게)/상대"],
    "Run_BarFill": [f"{G5}/하단게이지/게이지1"],
    "Run_BarShine": [f"{G5}/하단게이지/게이지2"],
    # 손잡이를 놓았을 때(FREE) 경고. 빨간 테두리(Warn_RedEdge)는 PSD 에 없어 아래 make_red_edge 로 만든다.
    "Warn_Dim": [f"{G5W}/배경"],
    "Warn_Hands": [f"{G5W}/이미지"],
    "Warn_Text": [f"{G5W}/자막"],
    "Warn_Icon": [f"{G5W}/아이콘-일정간격 깜빡이게"],

    "Button_High": [f"{G6}/고전압 버튼"],
    "Button_Low": [f"{G6}/저전압 버튼"],

    # 숫자('0.0')는 Unity 가 그린다
    "Sale_Energy": [f"{G7C}/01-전력량/<Rectangle> - 픽셀 레이어", f"{G7C}/01-전력량/<Clip Group>", f"{G7C}/01-전력량/<사각형>",
                    f"{G7C}/01-전력량/누적 환산 발전량", f"{G7C}/01-전력량/wh", f"{G7C}/01-전력량/<Path>"],
    "Sale_Arrow_1": [f"{G7C}/02-화살표"],
    "Sale_High": [f"{G7C}/03-고, 저전압/고전압"],
    "Sale_Low": [f"{G7C}/03-고, 저전압/저전압"],
    "Sale_Arrow_2": [f"{G7C}/03-화살표"],
    "Sale_Money": [f"{G7C}/04-판매금액/<Group>"],
}

# 이미지 없이 자리(좌표)만 필요한 레이어 (텍스트 자리, 같은 그림의 다른 위치)
RECTS = {
    "Join_Digit": f"{G2A}/카운터/9",
    "Voltage_Gauge": f"{G6}/타임게이지(5~10초)/게이지",
    "Sale_Gauge": f"{G7}/타임 게이지/게이지",
    "Ranking_Gauge": f"{G8}/타임게이지(5~10초)/게이지",
    "Run_TimeText": f"{G5}/타임/타임/시간",
    "Run_SpeedText": f"{G5}/속도계/속도",
    "Run_BarText": f"{G5}/하단게이지/숫자",
    "Run_Dial": f"{G5}/속도계/배경1",
    "Voltage_Card": f"{G6}/01-발전량 및 랭킹/오늘의 발전왕 순위",
    "Voltage_EnergyText": f"{G6}/01-발전량 및 랭킹/발전량",
    "Voltage_RankText": f"{G6}/01-발전량 및 랭킹/순위",
    "Sale_EnergyText": f"{G7C}/01-전력량/0.0",
    "Sale_MoneyText": f"{G7C}/04-판매금액/0,000",
    "Rank_E_Times": f"{G8}/발전왕 순위/시간-샘플",
    "Rank_E_Values": f"{G8}/발전왕 순위/발전-샘플",
    "Rank_E_MyRank": f"{G8}/발전왕 순위/나의 랭킹/순위",
    "Rank_E_MyTime": f"{G8}/발전왕 순위/나의 랭킹/시간",
    "Rank_E_MyValue": f"{G8}/발전왕 순위/나의 랭킹/발전",
    "Rank_S_Times": f"{G8}/판매왕 순위/시간-샘플",
    "Rank_S_Values": f"{G8}/판매왕 순위/금액-샘플",
    "Rank_S_MyRank": f"{G8}/판매왕 순위/나의랭킹/순위",
    "Rank_S_MyTime": f"{G8}/판매왕 순위/나의랭킹/시간",
    "Rank_S_MyValue": f"{G8}/판매왕 순위/나의랭킹/금액",
}

# ── 85형 PSD ─────────────────────────────────────────
R85 = "01-랭킹(PPT참조)"
STATICS_85 = {
    # 제목(타이틀)은 달리는 동안 숨기도록 부품(Spectator_Title)으로 따로 뺀다
    "Spectator": ["그림자 레이어", "<Clip Group>"],
}
RANK_TEXTS_85 = {"4.5 4.4 4.3 4.2 4.1", "3,000 3,000 3,000 3,000 3,000", "26.10.22 1:22:43 26.10.22 1:22:43 26.10.22 1:22:43 26.10.22 1:22:43 26.10.22 1:22:43"}
PARTS_85 = {
    "Spectator_RecordTag": ["오늘 최고 랭킹"],
    "Spectator_Title": ["타이틀"],
    "Spectator_Info_1": ["02-내용1"],
    "Spectator_Info_2": ["03-내용2"],
    # Spectator_Ranking 은 아래에서 01-랭킹 그룹의 숫자/시간 텍스트만 빼고 합성한다
}
RECTS_85 = {
    "Spec_Badge_1": "플레이어2/체험 대기중",   # 레이어 그룹 이름과 화면 위치가 엇갈려 있어 위치(왼쪽 = Player 01) 기준으로 고른다
    "Spec_Value_1": "플레이어1/발전량",
    "Spec_Badge_2": "플레이어1/체험 대기중",
    "Spec_Value_2": "플레이어2/발전량",
    "Spec_E_Times": f"{R85}/26.10.22 1:22:43 26.10.22 1:22:43 26.10.22 1:22:43 26.10.22 1:22:43 26.10.22 1:22:43#0",
    "Spec_E_Values": f"{R85}/4.5 4.4 4.3 4.2 4.1",
    "Spec_S_Times": f"{R85}/26.10.22 1:22:43 26.10.22 1:22:43 26.10.22 1:22:43 26.10.22 1:22:43 26.10.22 1:22:43#1",
    "Spec_S_Values": f"{R85}/3,000 3,000 3,000 3,000 3,000",
    "Spec_Bottom": f"{R85}/<Clip Group>#0",
}


def same_name(a, b):
    return "".join(a.split()) == "".join(b.split())


def find(psd, path):
    # 레이어 이름에 '/'가 들어 있을 수 있어, 남은 조각을 가장 길게 이어 붙인 이름부터 찾는다
    node = psd
    parts = path.split("/")
    while parts:
        for n in range(len(parts), 0, -1):
            name = "/".join(parts[:n])
            index = 0
            if "#" in name:
                name, index = name.rsplit("#", 1)
                index = int(index)
            # 텍스트 레이어 이름의 줄바꿈(\r)/탭 차이는 무시한다
            matches = [layer for layer in node if same_name(layer.name, name)]
            if len(matches) > index:
                node = matches[index]
                parts = parts[n:]
                break
        else:
            raise KeyError(f"레이어를 찾지 못했습니다: {path} ({parts[0]})")
    return node


def ancestors(layer):
    result = []
    parent = layer.parent
    while parent is not None and parent.kind != "psdimage":
        result.append(parent)
        parent = parent.parent
    return result


def composite_layers(psd, targets):
    """지정한 레이어만 켜서 전체 캔버스 크기로 합성한다. (숨김 그룹 안에 있어도 강제로 켠다)"""
    forced = set()
    for t in targets:
        forced.add(id(t))
        forced.update(id(a) for a in ancestors(t))
    target_ids = {id(t) for t in targets}

    def under_target(layer):
        return any(id(a) in target_ids for a in ancestors(layer))

    # 대상 그룹 안의 레이어는 원래 표시 상태를 따른다
    original = {id(layer): layer.visible for layer in psd.descendants()}

    def layer_filter(layer):
        return id(layer) in forced or (under_target(layer) and original[id(layer)])

    # psd-tools 는 숨김 그룹의 자식을 layer_filter 만으로는 그리지 않아서, 합성하는 동안만 visible 을 바꿔 둔다
    layers = list(psd.descendants())
    for layer in layers:
        layer.visible = layer_filter(layer)
    try:
        image = psd.composite(viewport=(0, 0, psd.width, psd.height), force=True, layer_filter=layer_filter)
    finally:
        for layer in layers:
            layer.visible = original[id(layer)]
    return image


def composite(psd, paths):
    return composite_layers(psd, [find(psd, p) for p in paths])


def rect_of(bbox):
    l, t, r, b = bbox
    return {"x": l, "y": t, "w": r - l, "h": b - t}


def pad_size(n):
    return (n + 3) // 4 * 4


def pad4(image):
    """GPU 블록 압축(4x4)이 되도록 오른쪽/아래에 투명 여백을 붙여 4의 배수로 맞춘다."""
    w, h = pad_size(image.width), pad_size(image.height)
    if (w, h) == image.size:
        return image
    canvas = Image.new("RGBA", (w, h))
    canvas.paste(image, (0, 0))
    return canvas


def save_cropped(image, path):
    bbox = image.getbbox()
    if bbox is None:
        raise ValueError(f"빈 이미지: {path}")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    pad4(image.crop(bbox)).save(path)
    l, t, r, b = bbox
    return (l, t, l + pad_size(r - l), t + pad_size(b - t))


def make_red_edge(width, height):
    """화면 가장자리가 불투명하고 가운데로 갈수록 투명해지는 흰 테두리 (Unity 에서 빨간색으로 칠해 맥박 효과).
    게임에서 체력이 없을 때처럼 네 변이 고르게 붉어지도록 둥근 사각형(초타원) 거리로 만든다. 부드러운 그림이라 절반 크기로 만든다."""
    import numpy as np
    w, h = width // 2, height // 2
    x = np.linspace(-1, 1, w)[None, :]
    y = np.linspace(-1, 1, h)[:, None]
    r = (np.abs(x) ** 4 + np.abs(y) ** 4) ** 0.25
    t = np.clip((r - 0.8) / (1.0 - 0.8), 0, 1)
    alpha = t * t * (3 - 2 * t)   # smoothstep
    rgba = np.zeros((h, w, 4), np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = (alpha * 255).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


# 85형 랭킹 판에서 지울 줄 순위 숫자 (판 그림 기준 x0, y0, x1, y1). 4·5위 자리는 PLAYER 01/02 최근 기록 줄이라
# 순위가 그때마다 달라서 그림 대신 Unity 글자(Row4_Rank, Row5_Rank)로 쓴다. 발전왕 표 / 판매왕 표(+1031px).
RANKING_ERASE_BOXES = [(71, 496, 93, 520), (73, 613, 91, 637), (1102, 496, 1124, 520), (1104, 613, 1122, 637)]


def erase_boxes(image, boxes, margin=4):
    """상자 안 글자를 지운다. 줄 바탕이 가로로 부드럽게 바뀌는 그러데이션이라 상자 좌우 바깥 색을 줄마다 직선으로 이어 채운다.
    이미 지운 그림에 다시 해도 결과가 같다."""
    import numpy as np
    a = np.array(image.convert("RGBA")).astype(np.float32)
    for x0, y0, x1, y1 in boxes:
        x0, y0, x1, y1 = x0 - margin, y0 - margin, x1 + margin, y1 + margin
        left, right = a[y0:y1 + 1, x0 - 1], a[y0:y1 + 1, x1 + 1]
        t = np.linspace(0, 1, x1 - x0 + 3)[1:-1][None, :, None]
        a[y0:y1 + 1, x0:x1 + 1] = left[:, None] * (1 - t) + right[:, None] * t
    return Image.fromarray(np.clip(a + 0.5, 0, 255).astype(np.uint8), "RGBA")


def export_static(psd, name, paths):
    os.makedirs(STATIC_DIR, exist_ok=True)
    composite(psd, paths).convert("RGBA").save(os.path.join(STATIC_DIR, f"Static_{name}.png"))
    print("static", name)


def export_part(psd, layout, name, image):
    layout[name] = rect_of(save_cropped(image, os.path.join(PARTS_DIR, f"{name}.png")))
    print("part", name)


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(1)

    layout = {}

    # 32형
    psd = PSDImage.open(sys.argv[1])
    for name, paths in STATICS.items():
        export_static(psd, name, paths)
    for name, paths in PARTS.items():
        export_part(psd, layout, name, composite(psd, paths))
    # 참가 대기 카운터 숫자 0~9 (같은 크기 칸에 가운데 정렬해서 숫자가 바뀌어도 흔들리지 않게)
    digits = [composite(psd, [f"{G2A}/카운터/{d}"]) for d in range(10)]
    boxes = [d.getbbox() for d in digits]
    cell_w = pad_size(max(b[2] - b[0] for b in boxes))
    cell_h = pad_size(max(b[3] - b[1] for b in boxes))
    for d, (image, box) in enumerate(zip(digits, boxes)):
        glyph = image.crop(box)
        canvas = Image.new("RGBA", (cell_w, cell_h))
        canvas.paste(glyph, ((cell_w - glyph.width) // 2, (cell_h - glyph.height) // 2))
        canvas.save(os.path.join(PARTS_DIR, f"Join_Digit_{d}.png"))
    print("part Join_Digit_0~9", cell_w, cell_h)
    for name, path in RECTS.items():
        layout[name] = rect_of(find(psd, path).bbox)
    # 손잡이 경고 빨간 테두리 (화면 전체 자리에 늘려 쓴다)
    os.makedirs(PARTS_DIR, exist_ok=True)
    make_red_edge(psd.width, psd.height).save(os.path.join(PARTS_DIR, "Warn_RedEdge.png"))
    layout["Warn_RedEdge"] = {"x": 0, "y": 0, "w": psd.width, "h": psd.height}
    print("part Warn_RedEdge (생성)")

    # 85형
    psd85 = PSDImage.open(sys.argv[2])
    for name, paths in STATICS_85.items():
        export_static(psd85, name, paths)
    for name, paths in PARTS_85.items():
        export_part(psd85, layout, name, composite(psd85, paths))
    os.makedirs(SPECTATOR_INFO_DIR, exist_ok=True)
    for n in (1, 2):
        Image.open(os.path.join(PARTS_DIR, f"Spectator_Info_{n}.png")).save(os.path.join(SPECTATOR_INFO_DIR, f"info_{n}.png"))
    ranking = find(psd85, R85)
    keep = [layer for layer in ranking if not (layer.kind == "type" and " ".join(layer.name.split()) in RANK_TEXTS_85)]
    export_part(psd85, layout, "Spectator_Ranking", composite_layers(psd85, keep))
    ranking_path = os.path.join(PARTS_DIR, "Spectator_Ranking.png")
    erase_boxes(Image.open(ranking_path), RANKING_ERASE_BOXES).save(ranking_path)
    for name, path in RECTS_85.items():
        layout[name] = rect_of(find(psd85, path).bbox)

    os.makedirs(os.path.dirname(LAYOUT_PATH), exist_ok=True)
    with open(LAYOUT_PATH, "w", encoding="utf-8") as f:
        # Unity JsonUtility가 읽을 수 있도록 이름이 붙은 사각형 목록으로 쓴다
        rects = [dict(name=k, **v) for k, v in layout.items()]
        json.dump({"wheelWidth": psd.width, "wheelHeight": psd.height, "spectatorWidth": psd85.width,
                   "spectatorHeight": psd85.height, "rects": rects}, f, ensure_ascii=False, indent=1)
    print("layout", len(layout))


if __name__ == "__main__":
    main()
