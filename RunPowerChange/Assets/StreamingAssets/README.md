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
| `runSeconds` | 50 | 발전(달리기) 시간(초). 시안 기준. 설정창에서도 바꿀 수 있음 |
| `guideSeconds` | 10 | 체험 안내 화면 자동 넘김(초) |
| `joinWaitSeconds` | 9 | 한 명이 시작한 뒤 옆 쳇바퀴 화면에 "참여하기" 버튼이 나오는 시간(초, 참가 대기 화면에 남은 초 표시). 그 뒤에 누르면 다음 회차 대기 |
| `readySeconds` | 3 | 참가 마감 뒤 "준비하세요!" 화면(초) |
| `wheelCircumferenceM` | 6.283 | 쳇바퀴 1회전 이동거리(m). 센서가 1회전마다 보내는 신호 1개당 이만큼 달린 것으로 계산 (직경 2m 기준). 설정창에서도 바꿀 수 있음 |
| `rotationStopSeconds` | 5 | 이 시간(초) 동안 회전 신호가 없으면 속도 0. 설정창에서도 바꿀 수 있음 |
| `maxValidSpeedKmh` | 40 | 직전 회전과의 간격이 이보다 빠른 속도로 환산되면 센서 튐으로 보고 무시 |
| `firstRotationSeconds` | 2 | 멈췄다가 처음 들어온 회전 신호는 속도를 아직 모르므로, 화면 캐릭터가 그 1회전 거리를 이 시간(초)에 걸쳐 걸어감. 설정창에서도 바꿀 수 있음 |
| `wattPerKmh`, `maxWatt` | 15, 300 | 환산 출력(W) = min(maxWatt, 속도 km/h × wattPerKmh) |
| `saleScaleMinutes` | 6000 | 누적 환산 발전량·판매는 "이 평균 출력으로 이 시간(분) 달렸을 때"의 발전량으로 계산 (시안: 100시간) |
| `saleReferenceKmh` | 20 | 판매 기준 속도(km/h). 15세 평균 최고속도(50m 평균 약 8.7초 ≈ 20km/h) |
| `saleReferenceWon` | 5000 | 기준 속도로 체험 시간 내내 달리고 유리한 전압을 고르면 받는 판매 금액(원). 판매단가(원/kWh)는 이 값에 맞춰 자동 계산 |
| `voltages[]` | 낮은/높은 전압 | `lossRate` 송전손실 비율, `facilityCostWon` 승압·강압 설비 비용(판매금액에서 차감, 높은 전압 300원) |
| `saleSeconds` | 10 | 최종 전력 판매 금액 화면(초) |
| `saleStepSeconds` | 0.6 | 판매 금액 화면에서 카드가 하나씩 나타나는 간격(초) |
| `idleTimeoutSeconds` | 30 | 전압 선택 화면에서 무입력 시 대기화면 복귀(초, 0=끔, 랭킹 기록 안 함). 다른 화면은 정해진 시간에 자동으로 넘어감 |
| `resultAutoCloseSeconds` | 10 | 랭킹 화면 자동 넘김(초) |
| `speedometerMaxKmh` | 20 | 속도계 바늘이 끝까지 가는 속도(km/h) |
| `finishApproachSeconds` | 10 | 결승선이 체험 끝나기 몇 초 전부터 다가오는지 (0=끔) |
| `raceScrollPerMeter` | 1.5 | 3D 배경(기둥·레일)이 달린 1m 마다 흘러가는 거리. 클수록 배경이 빨리 지나감 |
| `raceGapPerMeter` | 0.1 | 거리 차 1m 를 화면에서 앞뒤로 벌리는 정도. 클수록 조금만 앞서도 크게 벌어짐 |
| `spectatorInfoSeconds` | 10 | 85형 아래 칸(랭킹 → 왜 그럴까요? → 우리와 어떤 관련이?)이 바뀌는 간격(초) |
| `wheels[]` | 2대 | 쳇바퀴 번호, 센서 컨트롤러 id, 32형 디스플레이 번호(0부터), 시뮬레이션 키(누를 때마다 회전 신호 1개) |
| `spectatorDisplayIndex` | 2 | 85형 관전 화면(세로) 디스플레이 번호 |

계수는 모두 체험용 임시값입니다. 관람객 화면에는 정량 계수를 표시하지 않습니다.
