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
| `guideSeconds`, `characterSelectSeconds` | 12, 12 | 체험 안내·캐릭터 선택 화면 자동 넘김(초) |
| `joinWaitSeconds` | 15 | 한 명이 시작한 뒤 옆 쳇바퀴가 같은 회차(2인 경주)로 합류할 수 있는 시간(초). 그 뒤에 누르면 다음 회차 대기 |
| `wheelCircumferenceM` | 6.283 | 쳇바퀴 1회전 이동거리(m). 센서가 1회전마다 보내는 신호 1개당 이만큼 달린 것으로 계산 (직경 2m 기준). 설정창에서도 바꿀 수 있음 |
| `rotationStopSeconds` | 5 | 이 시간(초) 동안 회전 신호가 없으면 속도 0. 설정창에서도 바꿀 수 있음 |
| `maxValidSpeedKmh` | 40 | 직전 회전과의 간격이 이보다 빠른 속도로 환산되면 센서 튐으로 보고 무시 |
| `firstRotationSeconds` | 2 | 멈췄다가 처음 들어온 회전 신호는 속도를 아직 모르므로, 화면 캐릭터가 그 1회전 거리를 이 시간(초)에 걸쳐 걸어감. 설정창에서도 바꿀 수 있음 |
| `wattPerKmh`, `maxWatt` | 15, 300 | 환산 출력(W) = min(maxWatt, 속도 km/h × wattPerKmh) |
| `saleScaleMinutes` | 6000 | 누적 환산 발전량·판매는 "이 평균 출력으로 이 시간(분) 달렸을 때"의 발전량으로 계산 (시안: 100시간) |
| `smpWonPerKWh` | 150 | 기준 판매단가(원/kWh) |
| `voltages[]` | 낮은/높은 전압 | `lossRate` 송전손실 비율, `facilityCostWon` 승압·강압 설비 비용(판매금액에서 차감, 높은 전압 300원) |
| `transmitSeconds` | 20 | 송전 연출 시간(초) |
| `idleTimeoutSeconds` | 60 | 전압 선택 화면에서 무입력 시 대기화면 복귀(초, 랭킹 기록 안 함) |
| `resultAutoCloseSeconds` | 15 | 발전 완료·결과 확인 화면 자동 넘김(초) |
| `rankingShowCount` | 5 | 결과 화면·85형 랭킹 표 인원 |
| `recordLineCount` | 3 | 85형 레코드 라인 수 (오늘 상위 기록, 없으면 전날 기록) |
| `rankingPopupSeconds` | 6 | 85형 새 랭킹 팝업 표시 시간(초) |
| `wheels[]` | 2대 | 쳇바퀴 번호, 센서 컨트롤러 id, 32형 디스플레이 번호(0부터), 시뮬레이션 키(누를 때마다 회전 신호 1개) |
| `spectatorDisplayIndex` | 2 | 85형 관전 화면(세로) 디스플레이 번호 |

계수는 모두 체험용 임시값입니다. 관람객 화면에는 정량 계수를 표시하지 않습니다.
