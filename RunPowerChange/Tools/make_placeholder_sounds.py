# -*- coding: utf-8 -*-
"""
임시 소리 만들기. 실제 소리 파일을 받기 전에 쓰는 합성음(wav)을 코드로 만든다 (외부 음원 없음, 저작권 문제 없음).
콘셉트: 검은 밤 네온 트랙을 달려 전기를 만들고 송전·판매한다 → 신스웨이브 BGM + 전기 느낌 효과음.

  python Tools/make_placeholder_sounds.py          없는 파일만 만든다 (받아 넣은 실제 소리는 그대로 둔다)
  python Tools/make_placeholder_sounds.py --force  모두 다시 만든다

출력: Assets/StreamingAssets/Audio/*.wav (44.1kHz 16bit 스테레오), sound.json (없을 때만)
필요: pip install numpy
"""
import json
import os
import sys
import wave

import numpy as np

PROJECT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUT = os.path.join(PROJECT, "Assets", "StreamingAssets", "Audio")
RATE = 44100
rng = np.random.default_rng(7)


# ── 기본 소리 ─────────────────────────────────────────

def t_axis(seconds):
    return np.arange(int(RATE * seconds)) / RATE


def hz(n):  # 미디 번호 → Hz
    return 440.0 * 2 ** ((n - 69) / 12)


def osc(freq, seconds, shape="sine", phase=0.0):
    """freq 는 숫자 또는 시간별 배열(음높이가 변하는 소리)"""
    n = int(RATE * seconds)
    f = np.full(n, float(freq)) if np.isscalar(freq) else np.asarray(freq, float)[:n]
    ph = phase + np.cumsum(f) / RATE
    if shape == "saw":
        return 2 * (ph - np.floor(ph + 0.5))
    if shape == "square":
        return np.sign(np.sin(2 * np.pi * ph))
    if shape == "tri":
        return 2 * np.abs(2 * (ph - np.floor(ph + 0.5))) - 1
    return np.sin(2 * np.pi * ph)


def adsr(n, a=0.01, d=0.1, s=0.7, r=0.1):
    a, d, r = int(RATE * a), int(RATE * d), int(RATE * r)
    env = np.full(n, s)
    a = min(a, n)
    env[:a] = np.linspace(0, 1, a, endpoint=False) if a else env[:a]
    d = min(d, n - a)
    if d > 0: env[a:a + d] = np.linspace(1, s, d)
    r = min(r, n)
    if r > 0: env[-r:] *= np.linspace(1, 0, r)
    return env


def lowpass(x, cutoff):
    """한 극 저역 통과. cutoff 는 숫자 또는 시간별 배열(Hz)"""
    if np.isscalar(cutoff):
        # 고정 차단 주파수: 같은 응답의 지수 커널 합성곱 (반복문보다 훨씬 빠름)
        alpha = 1 - np.exp(-2 * np.pi * float(cutoff) / RATE)
        k = int(np.ceil(np.log(1e-5) / np.log(1 - alpha))) + 1
        kernel = alpha * (1 - alpha) ** np.arange(k)
        return np.convolve(x, kernel)[:len(x)]
    c = np.asarray(cutoff, float)[:len(x)]
    alpha = 1 - np.exp(-2 * np.pi * c / RATE)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc += alpha[i] * (x[i] - acc)
        y[i] = acc
    return y


def lp(x, cutoff, poles=2):
    for _ in range(poles):
        x = lowpass(x, cutoff)
    return x


def highpass(x, cutoff):
    return x - lowpass(x, cutoff)


def noise(seconds):
    return rng.uniform(-1, 1, int(RATE * seconds))


def supersaw(freq, seconds, voices=5, detune=0.12):
    """살짝 어긋나게 겹친 톱니파 (신스웨이브 패드·리드)"""
    out = np.zeros(int(RATE * seconds))
    for v in range(voices):
        cents = (v - (voices - 1) / 2) * detune * 100 / max(1, (voices - 1) / 2)
        out += osc(freq * 2 ** (cents / 1200), seconds, "saw", rng.random())
    return out / voices


def place(out, start, s, gain=1.0):
    i = int(RATE * start)
    if i >= len(out): return
    n = min(len(s), len(out) - i)
    out[i:i + n] += s[:n] * gain


def place_loop(out, start, s, gain=1.0):
    """루프 끝을 넘는 부분은 앞으로 감아 붙인다 (이음매 없이 반복되게)"""
    i = int(RATE * start) % len(out)
    for k in range(0, len(s), len(out)):
        part = s[k:k + len(out)]
        end = i + len(part)
        if end <= len(out):
            out[i:end] += part * gain
        else:
            cut = len(out) - i
            out[i:] += part[:cut] * gain
            out[:end - len(out)] += part[cut:] * gain


def echo_loop(x, delay, feedback=0.35, mix=0.3, taps=4):
    """루프용 메아리 (꼬리를 앞으로 감는다)"""
    out = x.copy()
    d = int(RATE * delay)
    for k in range(1, taps + 1):
        out += np.roll(x, d * k) * (feedback ** k) * mix
    return out


def echo(x, delay, feedback=0.35, mix=0.3, taps=4):
    d = int(RATE * delay)
    out = np.concatenate([x, np.zeros(d * taps)])
    for k in range(1, taps + 1):
        out[d * k:d * k + len(x)] += x * (feedback ** k) * mix
    return out


def stereo(left, right=None, width=0.0):
    right = left if right is None else right
    return np.stack([left * (1 - width * 0.5) + right * width * 0.5, right * (1 - width * 0.5) + left * width * 0.5], 1)


def normalize(x, peak=0.85):
    m = np.abs(x).max()
    return x / m * peak if m > 0 else x


# ── 드럼 머신 ─────────────────────────────────────────

def kick(seconds=0.35):
    t = t_axis(seconds)
    f = 45 + 120 * np.exp(-t * 35)
    return osc(f, seconds) * np.exp(-t * 9) + noise(seconds) * np.exp(-t * 200) * 0.15


def snare(seconds=0.25):
    t = t_axis(seconds)
    body = osc(190, seconds) * np.exp(-t * 25) * 0.5
    return body + highpass(noise(seconds), 1500) * np.exp(-t * 18) * 0.8


def hat(seconds=0.05, open_=False):
    t = t_axis(seconds)
    return highpass(noise(seconds), 7000) * np.exp(-t * (12 if open_ else 70)) * 0.5


# ── 배경음악 ─────────────────────────────────────────

def synthwave(bpm, chords, bars, *, drums="none", arp=True, lead=None, pad_cut=1200, bass_cut=600, swing_hat=True):
    beat = 60 / bpm
    length = bars * 4 * beat
    n = int(RATE * length)
    pad_l, pad_r, bass, arp_l, arp_r, lead_bus, drum = (np.zeros(n) for _ in range(7))

    for bar in range(bars):
        chord = chords[bar % len(chords)]
        t0 = bar * 4 * beat
        # 패드: 마디 내내 이어지는 화음, 필터가 천천히 열린다
        for k, note in enumerate(chord):
            s = supersaw(hz(note), 4 * beat + 0.4, voices=5, detune=0.10)
            s = lp(s, pad_cut * 0.75) * adsr(len(s), 0.4, 0.5, 0.8, 0.6)
            place_loop(pad_l if k % 2 == 0 else pad_r, t0, s, 0.33)
            place_loop(pad_r if k % 2 == 0 else pad_l, t0, s, 0.18)
        # 베이스: 8분음 근음 (신스웨이브식 질주감)
        root = chord[0] - 24
        for e in range(8):
            s = osc(hz(root), beat / 2, "saw") * 0.6 + osc(hz(root - 12), beat / 2, "sine") * 0.5
            s = lp(s, bass_cut) * adsr(len(s), 0.003, 0.08, 0.5, 0.05)
            place_loop(bass, t0 + e * beat / 2, s)
        # 아르페지오: 16분음 화음 오르내림
        if arp:
            pattern = [0, 1, 2, 1, 2, 3, 2, 1]
            notes = chord + [chord[0] + 12]
            for e in range(16):
                note = notes[pattern[e % len(pattern)] % len(notes)] + 12
                s = osc(hz(note), beat / 4, "square") * 0.5 + osc(hz(note) * 1.005, beat / 4, "saw") * 0.5
                s = lp(s, 2600) * adsr(len(s), 0.002, 0.06, 0.25, 0.04)
                place_loop(arp_l if e % 2 == 0 else arp_r, t0 + e * beat / 4, s, 0.22)
        # 리드
        if lead:
            for k, note in enumerate(lead[bar % len(lead)]):
                if not note: continue
                dur = beat * 0.95
                f = hz(note) * (1 + 0.004 * np.sin(2 * np.pi * 5.5 * t_axis(dur)))  # 비브라토
                s = osc(f, dur, "saw") * 0.6 + osc(f * 1.003, dur, "square") * 0.4
                s = lp(s, 3200) * adsr(len(s), 0.02, 0.15, 0.7, 0.12)
                place_loop(lead_bus, t0 + k * beat, s, 0.28)
        # 드럼
        for b in range(4):
            tb = t0 + b * beat
            if drums in ("full", "light"):
                place_loop(drum, tb, kick(), 0.9 if drums == "full" else 0.55)
            if drums == "full" and b in (1, 3):
                place_loop(drum, tb, snare(), 0.55)
            if drums in ("full", "light"):
                place_loop(drum, tb + beat / 2, hat(0.12, open_=True) if swing_hat else hat(), 0.35)
                if drums == "full":
                    place_loop(drum, tb + beat / 4, hat(), 0.2)
                    place_loop(drum, tb + beat * 3 / 4, hat(), 0.2)

    # 신스웨이브 특유의 메아리
    arp_l = echo_loop(arp_l, beat * 0.75, 0.4, 0.5)
    arp_r = echo_loop(arp_r, beat * 0.5, 0.4, 0.5)
    lead_bus = echo_loop(lead_bus, beat * 0.75, 0.35, 0.4)

    left = pad_l + bass * 0.9 + arp_l + lead_bus * 0.9 + drum
    right = pad_r + bass * 0.9 + arp_r + lead_bus * 0.9 + drum
    out = np.stack([left, right], 1)
    # 반복 이음매에서 틱 소리가 나지 않게 끝과 처음을 아주 짧게(5ms) 줄였다 키운다
    k = int(RATE * 0.005)
    ramp = np.linspace(0, 1, k)[:, None]
    out[:k] *= ramp
    out[-k:] *= ramp[::-1]
    return normalize(out, 0.8)


# ── 효과음 (전기·네온 콘셉트) ────────────────────────────

def zap(seconds, f0, f1, shape="saw", cut=4000):
    t = t_axis(seconds)
    f = f0 * (f1 / f0) ** (t / seconds)
    s = osc(f, seconds, shape) + osc(f * 1.01, seconds, "square") * 0.4
    return lp(s, cut)


def buzz(seconds, freq=60, amount=0.3):
    """전기 지지직 (낮은 웅웅거림 + 잘게 끊기는 잡음)"""
    t = t_axis(seconds)
    gate = (rng.random(len(t)) > 0.7).astype(float)
    gate = lp(gate, 300, 1)
    return osc(freq, seconds, "saw") * 0.4 * amount + highpass(noise(seconds), 2500) * gate * amount


def sparkle(seconds=0.6, base=84):
    out = np.zeros(int(RATE * seconds))
    for k in range(6):
        note = base + rng.integers(0, 12)
        s = osc(hz(note), 0.15) * adsr(int(RATE * 0.15), 0.002, 0.03, 0.3, 0.1)
        place(out, k * seconds / 8, s, 0.25)
    return out


def sfx_join():
    # 전원이 켜지는 소리: 아래에서 위로 충전 + 확정 핑
    up = zap(0.28, 180, 1400, "saw", 3500) * adsr(int(RATE * 0.28), 0.01, 0.1, 0.8, 0.05) * 0.5
    ping = osc(hz(88), 0.35) * adsr(int(RATE * 0.35), 0.002, 0.05, 0.4, 0.3) * 0.5
    out = np.zeros(int(RATE * 0.7))
    place(out, 0, up)
    place(out, 0.26, ping)
    place(out, 0.26, osc(hz(83), 0.35, "tri") * adsr(int(RATE * 0.35), 0.002, 0.05, 0.3, 0.3) * 0.3)
    return echo(out, 0.09, 0.3, 0.3, 3)


def sfx_ready():
    # 충전 완료: 두 음이 차례로 켜지는 네온 차임
    out = np.zeros(int(RATE * 0.9))
    for k, n in enumerate((76, 83, 88)):
        s = (osc(hz(n), 0.45, "tri") + osc(hz(n) * 2, 0.45) * 0.3) * adsr(int(RATE * 0.45), 0.003, 0.08, 0.35, 0.3)
        place(out, k * 0.09, s, 0.4)
    return echo(out, 0.11, 0.3, 0.3, 3)


def sfx_count():
    # 디지털 카운트 삑
    s = (osc(hz(81), 0.16, "square") * 0.5 + osc(hz(93), 0.16) * 0.5)
    return lp(s, 5000) * adsr(len(s), 0.002, 0.04, 0.5, 0.08) * 0.7


def sfx_go():
    # 출발: 전기가 확 흐르는 상승음 + 높은 음
    surge = zap(0.45, 300, 2400, "saw", 6000) * adsr(int(RATE * 0.45), 0.005, 0.1, 0.8, 0.2) * 0.45
    tone = (osc(hz(93), 0.6, "square") * 0.4 + osc(hz(105), 0.6) * 0.4) * adsr(int(RATE * 0.6), 0.005, 0.1, 0.6, 0.35)
    out = np.zeros(int(RATE * 0.9))
    place(out, 0, surge)
    place(out, 0.05, lp(tone, 6000), 0.6)
    place(out, 0, buzz(0.4, 90, 0.35))
    return echo(out, 0.12, 0.3, 0.25, 3)


def sfx_finish():
    # 결승: 짧은 신스 팡파르 (올라가는 3음 + 긴 화음)
    out = np.zeros(int(RATE * 1.6))
    for k, n in enumerate((72, 76, 79)):
        s = (osc(hz(n), 0.14, "saw") * 0.6 + osc(hz(n) * 1.004, 0.14, "square") * 0.4)
        place(out, k * 0.13, lp(s, 4000) * adsr(len(s), 0.003, 0.05, 0.7, 0.05), 0.45)
    for n in (72, 76, 79, 84):
        s = supersaw(hz(n), 1.1, 5, 0.1)
        place(out, 0.39, lp(s, 3500) * adsr(len(s), 0.01, 0.2, 0.6, 0.6), 0.22)
    return echo(out, 0.15, 0.3, 0.3, 3)


def sfx_select():
    # 스위치 딸깍 + 전기 지직
    click = highpass(noise(0.015), 2000) * np.exp(-t_axis(0.015) * 300)
    zz = zap(0.18, 900, 2200, "square", 5000) * adsr(int(RATE * 0.18), 0.002, 0.04, 0.4, 0.08) * 0.35
    out = np.zeros(int(RATE * 0.35))
    place(out, 0, click, 0.8)
    place(out, 0.01, zz)
    place(out, 0.0, buzz(0.2, 120, 0.25))
    return out


def sfx_card():
    # 카드가 휙 나타남: 필터가 열리는 바람 소리 + 짧은 네온 음
    t = t_axis(0.3)
    whoosh = lp(noise(0.3), 300 + 5000 * (t / 0.3)) * np.sin(np.pi * t / 0.3) * 0.6
    ping = osc(hz(91), 0.2, "tri") * adsr(int(RATE * 0.2), 0.002, 0.03, 0.3, 0.15) * 0.25
    out = np.zeros(int(RATE * 0.4))
    place(out, 0, whoosh)
    place(out, 0.18, ping)
    return out


def sfx_money():
    # 판매 금액: 동전 두 번 + 반짝임
    out = np.zeros(int(RATE * 1.0))
    for k, n in enumerate((95, 100)):
        s = (osc(hz(n), 0.35) + osc(hz(n) * 2.76, 0.35) * 0.3) * adsr(int(RATE * 0.35), 0.001, 0.02, 0.4, 0.3)
        place(out, k * 0.08, s, 0.45)
    place(out, 0.15, sparkle(0.7, 88), 0.9)
    return echo(out, 0.1, 0.3, 0.25, 3)


def sfx_tick():
    # 판매 금액이 오르는 동안: 짧은 금속성 딸깍 (높은 음 + 아주 짧은 잡음)
    t = t_axis(0.04)
    click = highpass(noise(0.04), 3000) * np.exp(-t * 180) * 0.6
    ping = osc(hz(100), 0.04, "square") * np.exp(-t * 120) * 0.25
    return lp(click + ping, 9000)


def sfx_ranking():
    # 랭킹: 밝게 올라가는 신스 아르페지오 + 화음
    out = np.zeros(int(RATE * 1.8))
    for k, n in enumerate((72, 76, 79, 84, 88)):
        s = osc(hz(n), 0.18, "square") * 0.5 + osc(hz(n) * 1.004, 0.18, "saw") * 0.5
        place(out, k * 0.08, lp(s, 4500) * adsr(len(s), 0.002, 0.05, 0.5, 0.08), 0.35)
    for n in (76, 79, 84, 88):
        s = supersaw(hz(n), 1.2, 5, 0.1)
        place(out, 0.42, lp(s, 4000) * adsr(len(s), 0.02, 0.3, 0.5, 0.7), 0.2)
    place(out, 0.42, sparkle(0.9, 91), 0.7)
    return echo(out, 0.14, 0.3, 0.3, 3)


# ── 저장 ─────────────────────────────────────────────

def save(name, samples, force, peak=0.85):
    path = os.path.join(OUT, name)
    if os.path.exists(path) and not force:
        print("skip", name)
        return
    if samples.ndim == 1:
        samples = stereo(normalize(samples, peak))
    data = (np.clip(samples, -1, 1) * 32767).astype(np.int16)
    with wave.open(path, "wb") as f:
        f.setnchannels(2)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(data.tobytes())
    print("write", name, f"{len(samples) / RATE:.1f}s")


TABLE = {
    "bgm": [
        {"key": "idle", "file": "bgm_idle.wav", "volume": 0.9},
        {"key": "race", "file": "bgm_race.wav", "volume": 1.0},
        {"key": "result", "file": "bgm_result.wav", "volume": 0.9},
    ],
    "sfx": [
        {"key": "join", "file": "sfx_join.wav", "volume": 1.0},
        {"key": "ready", "file": "sfx_ready.wav", "volume": 1.0},
        {"key": "count", "file": "sfx_count.wav", "volume": 1.0},
        {"key": "go", "file": "sfx_go.wav", "volume": 1.0},
        {"key": "finish", "file": "sfx_finish.wav", "volume": 1.0},
        {"key": "select", "file": "sfx_select.wav", "volume": 1.0},
        {"key": "card", "file": "sfx_card.wav", "volume": 0.8},
        {"key": "money", "file": "sfx_money.wav", "volume": 1.0},
        {"key": "tick", "file": "sfx_tick.wav", "volume": 0.6},
        {"key": "ranking", "file": "sfx_ranking.wav", "volume": 1.0},
    ],
}


def main():
    force = "--force" in sys.argv
    os.makedirs(OUT, exist_ok=True)

    Am, F, C, G, Em, Dm = [57, 60, 64], [53, 57, 60], [48, 52, 55, 59], [55, 59, 62], [52, 55, 59], [50, 53, 57]
    # 대기: 느리고 몽환적인 네온 (드럼 거의 없이)
    save("bgm_idle.wav", synthwave(92, [Am + [67], F + [64], C + [62], G + [66]], 8, drums="none", arp=True, pad_cut=1400), force)
    # 경주: 빠른 킥 + 아르페지오 + 리드 (질주감)
    save("bgm_race.wav", synthwave(132, [Am, F, C, G], 8, drums="full", arp=True, pad_cut=2200, bass_cut=900,
                                   lead=[[81, 0, 84, 83], [81, 0, 77, 76], [79, 0, 76, 74], [74, 76, 79, 83]]), force)
    # 결과: 밝은 장조로 마무리
    save("bgm_result.wav", synthwave(104, [[53, 57, 60, 64], [55, 59, 62, 65], [48, 52, 55, 59], [48, 52, 55, 59]], 4, drums="light",
                                     arp=True, pad_cut=2400, lead=[[84, 0, 86, 0], [88, 0, 91, 0], [96, 0, 0, 0], [0, 0, 0, 0]]), force)

    save("sfx_join.wav", sfx_join(), force)
    save("sfx_ready.wav", sfx_ready(), force)
    save("sfx_count.wav", sfx_count(), force, 0.8)
    save("sfx_go.wav", sfx_go(), force)
    save("sfx_finish.wav", sfx_finish(), force)
    save("sfx_select.wav", sfx_select(), force, 0.8)
    save("sfx_card.wav", sfx_card(), force, 0.7)
    save("sfx_money.wav", sfx_money(), force)
    save("sfx_ranking.wav", sfx_ranking(), force)
    save("sfx_tick.wav", sfx_tick(), force, 0.7)

    # sound.json: 없으면 새로 쓰고, 있으면 없는 키만 덧붙인다 (현장에서 바꾼 파일 이름·볼륨은 지킨다)
    table_path = os.path.join(OUT, "sound.json")
    table = TABLE
    if os.path.exists(table_path) and not force:
        with open(table_path, encoding="utf-8") as f:
            table = json.load(f)
        added = 0
        for kind in ("bgm", "sfx"):
            keys = {e["key"] for e in table.get(kind, [])}
            for entry in TABLE[kind]:
                if entry["key"] not in keys:
                    table.setdefault(kind, []).append(entry)
                    added += 1
        if added == 0:
            return
    with open(table_path, "w", encoding="utf-8") as f:
        json.dump(table, f, ensure_ascii=False, indent=4)
    print("write sound.json")


if __name__ == "__main__":
    main()
