# 소리 (배경음악·효과음)

실행할 때 이 폴더의 파일을 읽습니다. 빌드 후에도 `<앱>_Data/StreamingAssets/Audio/` 의 파일만 바꾸면 다시 빌드하지 않고 반영됩니다 (앱 재시작 필요).

- **지금 들어 있는 파일은 `Tools/make_placeholder_sounds.py` 가 코드로 만든 임시 합성음입니다** (외부 음원을 쓰지 않아 저작권 문제 없음).
  콘셉트에 맞춰 신스웨이브 배경음악(네온 트랙)과 전기 느낌 효과음(충전·지지직·동전)으로 만들었습니다. 실제 소리를 받으면 같은 파일명으로 덮어쓰세요.
  (다른 이름이나 mp3/ogg 로 넣으려면 `sound.json` 의 `file` 을 바꿉니다)
- 파일 형식: wav, ogg, mp3
- 파일이 없거나 깨져 있으면 그 소리만 나지 않고 앱은 계속 동작합니다 (로그에 `[Sound]` 로 남음).
- 볼륨: ESC 설정창의 조절바(전체·배경음악·효과음)와 음소거(배경음악·효과음), 또는 `Setting.json` 의 `masterVolume` · `bgmVolume` · `sfxVolume`(0~1), `bgmMuted` · `sfxMuted`.
  음소거해도 볼륨 값은 그대로라 풀면 원래 크기로 돌아옵니다.
  소리마다 따로 줄이려면 `sound.json` 의 `volume`(0~1)을 바꿉니다.
- 임시 소리를 다시 만들려면: `python Tools/make_placeholder_sounds.py` (없는 파일만 만듦, `--force` 는 모두 다시)

## sound.json

```json
{
    "bgm": [ { "key": "idle", "file": "bgm_idle.wav", "volume": 1.0 } ],
    "sfx": [ { "key": "count", "file": "sfx_count.wav", "volume": 1.0 } ]
}
```
`key` 는 프로그램이 부르는 이름이라 바꾸면 안 됩니다.

## 배경음악 (반복 재생, 바뀔 때 `bgmFadeSeconds` 동안 겹쳐 넘어감)

| key | 파일 | 나오는 때 |
|---|---|---|
| `idle` | bgm_idle.wav | 대기 화면, 참가 대기, 준비, 체험 안내 |
| `race` | bgm_race.wav | 카운트다운, 달리기 |
| `result` | bgm_result.wav | 달리기가 끝난 뒤 (전압 선택, 판매, 랭킹) |

PC 1대에 스피커가 하나라 두 쳇바퀴가 같은 배경음악을 듣습니다 (회차 단계 기준).

## 효과음

| key | 파일 | 나오는 때 |
|---|---|---|
| `join` | sfx_join.wav | 체험시작 / 참여하기를 눌렀을 때 |
| `ready` | sfx_ready.wav | 참가 마감 ("준비하세요!") |
| `count` | sfx_count.wav | 카운트다운 3, 2, 1 |
| `go` | sfx_go.wav | GO (달리기 시작) |
| `finish` | sfx_finish.wav | 달리기 끝 |
| `select` | sfx_select.wav | 송전 전압을 골랐을 때 |
| `card` | sfx_card.wav | 판매 화면 발전량·송전 방식·판매 금액 카드가 나타날 때 |
| `money` | sfx_money.wav | 판매 금액이 얻은 금액까지 다 올랐을 때 |
| `tick` | sfx_tick.wav | 판매 금액이 0 부터 올라가는 동안 (0.09초마다) |
| `ranking` | sfx_ranking.wav | 랭킹 화면 |
