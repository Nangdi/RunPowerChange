# StreamingAssets - 달려라! 전력 챌린지

이 폴더의 JSON은 **기본값 원본**입니다. 실제로 읽고 쓰는 파일은 아래 설정 폴더에 있습니다.

- 설정 폴더: `%USERPROFILE%\AppData\LocalLow\<회사명>\<제품명>\` (`Application.persistentDataPath`)
- 설정 폴더에 파일이 없을 때만 여기서 복사합니다. 이미 있으면 덮어쓰지 않으므로 빌드를 새로 덮어써도 현장 값이 유지됩니다.
- 기본값으로 되돌리려면 설정 폴더의 해당 파일을 지우고 다시 실행합니다.
- 파일이 없거나 깨져 있어도 앱은 코드 기본값으로 실행하고 로그만 남깁니다.

| 파일 | 내용 |
|---|---|
| `Setting.json` | 체험 시간, 센서 환산, 발전·판매 계수, 쳇바퀴별 포트·디스플레이 연결 |
| `port.json` | 컨트롤러별 COM 포트 (`controllerId`는 `Setting.json`의 `wheels[].controllerId`와 맞춤) |

## Setting.json 주요 항목

| 항목 | 기본 | 설명 |
|---|---|---|
| `runSeconds` | 50 | 발전(달리기) 시간(초). 시안 기준. 설정창에서도 바꿀 수 있음. 바꾸면 체험 안내 그림(Static_Guide.png)의 "제한시간 50초" 글자도 따로 바꿔야 함 |
| `guideSeconds` | 10 | 체험 안내 화면 자동 넘김(초) |
| `joinWaitSeconds` | 15 | 한 명이 시작한 뒤 옆 쳇바퀴 화면에 "참여하기" 버튼이 나오는 시간(초, 참가 대기 화면에 남은 초 표시). 그 뒤에 누르면 다음 회차 대기 |
| `readySeconds` | 0 | 참가 마감 뒤 "준비하세요!" 화면(초). 0이면 이 화면 없이 바로 체험 안내로 넘어감 |
| `wheelCircumferenceM` | 6.283 | 쳇바퀴 1회전 이동거리(m). 센서가 1회전마다 보내는 신호 1개당 이만큼 달린 것으로 계산 (직경 2m 기준). 설정창에서도 바꿀 수 있음 |
| `rotationStopSeconds` | 5 | 이 시간(초) 동안 회전 신호가 없으면 속도 0. 설정창에서도 바꿀 수 있음 |
| `maxValidSpeedKmh` | 40 | 직전 회전과의 간격이 이보다 빠른 속도로 환산되면 센서 튐으로 보고 무시 |
| `firstRotationSeconds` | 2 | 멈췄다가 처음 들어온 회전 신호는 속도를 아직 모르므로, 화면 캐릭터가 그 1회전 거리를 이 시간(초)에 걸쳐 걸어감. 설정창에서도 바꿀 수 있음 |
| `wattPerKmh` | 1 | 환산 출력(W) = min(최대 출력, 속도 km/h × wattPerKmh). 최대 출력은 따로 정하지 않고 wattPerKmh × speedometerMaxKmh 로 계산(기본 1 × 30 = 30W). 20 으로 두면 걷기 5km/h 100W, 구성안 예시 150W = 7.5km/h (직경 2m 쳇바퀴 1km/h ≈ 2.65rpm) |
| `saleScaleMinutes` | 6000 | 누적 환산 발전량·판매는 "이 평균 출력으로 이 시간(분) 달렸을 때"의 발전량으로 계산 (시안: 100시간) |
| `wonPerKWh` | 120 | 판매단가(원/kWh). 판매 금액 = 도착 전력(kWh) × 이 값 − 설비 비용. 최근 육지 SMP(계통한계가격) kWh 당 약 110~130원 수준에 맞춤. 한전 자문 후 조정 |
| `voltages[]` | 낮은/높은 전압 | `lossRate` 송전손실 비율(낮은 0.3, 높은 0.05), `facilityCostWon` 승압·강압 설비 비용(원, 판매금액에서 차감. 낮은 0, 높은 34). 높은 전압 34원 = 평균 체험자(10km/h)가 끝까지 달렸을 때 높은 전압 판매금액 114원의 약 30%. 이 설정이면 약 11km/h 보다 빨라야 높은 전압이 유리. wattPerKmh·wonPerKWh 를 바꾸면 같은 기준으로 다시 계산 |
| `saleSeconds` | 10 | 최종 전력 판매 금액 화면(초) |
| `saleStepSeconds` | 0.6 | 판매 금액 화면에서 카드가 하나씩 나타나는 간격(초) |
| `saleSlotSeconds` | 1.8 | 판매 금액이 0 부터 얻은 금액까지 올라가는 시간(초). 0이면 바로 표시 |
| `idleTimeoutSeconds` | 30 | 전압 선택 화면에서 무입력 시 대기화면 복귀(초, 0=끔, 랭킹 기록 안 함). 다른 화면은 정해진 시간에 자동으로 넘어감 |
| `resultAutoCloseSeconds` | 15 | 랭킹 화면 자동 넘김(초) |
| `speedometerMaxKmh` | 30 | 속도계 바늘이 끝까지 가는 속도(km/h)이자 출력이 최대가 되는 속도. 이보다 빨리 달려도 출력은 늘지 않음 |
| `finishApproachSeconds` | 10 | 결승선이 체험 끝나기 몇 초 전부터 다가오는지 (0=끔) |
| `raceScrollPerMeter` | 1.5 | 3D 배경(기둥·레일)이 달린 1m 마다 흘러가는 거리. 클수록 배경이 빨리 지나감 |
| `raceGapPerMeter` | 0.1 | 거리 차 1m 를 화면에서 앞뒤로 벌리는 정도. 클수록 조금만 앞서도 크게 벌어짐 |
| `characterAnimMinSpeed`, `characterAnimMaxSpeed`, `characterAnimMaxKmh` | 0.5, 2, 18 | 캐릭터 달리기 동작 배속. 막 움직이기 시작할 때 최소(걷듯이 느리게), characterAnimMaxKmh(km/h)에서 최대. 그 사이는 속도에 비례. 초등 고학년~중학생 쳇바퀴 예상 속도 기준: 4km/h 0.7배, 7km/h 1배, 12km/h 1.4배, 18km/h 2배 |
| `spectatorInfoSeconds` | 10 | 85형 아래 칸(랭킹 → 왜 그럴까요? → 우리와 어떤 관련이?)이 바뀌는 간격(초) |
| `masterVolume`, `bgmVolume`, `sfxVolume` | 1, 0.5, 1 | 전체·배경음악·효과음 볼륨 (0~1). 소리 파일은 `Audio/` 폴더 README 참고 |
| `bgmMuted`, `sfxMuted` | false | 배경음악·효과음 음소거 (볼륨 값은 그대로 두고 소리만 끔) |
| `bgmFadeSeconds` | 1 | 배경음악이 바뀔 때 겹쳐 넘어가는 시간(초) |
| `wheels[]` | 2대 | 쳇바퀴 번호, 센서 컨트롤러 id, 32형 디스플레이 번호(0부터), 시뮬레이션 키(누를 때마다 회전 신호 1개) |
| `operatorDisplayIndex` | 2 | 설정창(ESC)·RS232 디버그 패널·F1 도움말을 띄울 디스플레이 (0 = Display 1). 관람객 32형 화면을 가리지 않게 85형 쪽. 재시작 후 적용 |
| `spectatorDisplayIndex` | 2 | 85형 관전 화면(세로) 디스플레이 번호 |

계수는 모두 체험용 임시값입니다. 관람객 화면에는 정량 계수를 표시하지 않습니다.
