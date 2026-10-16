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
| `runSeconds` | 180 | 발전 체험 시간(초). 설정창에서도 바꿀 수 있음 |
| `pulsesPerRevolution`, `rollerCircumferenceM` | 1, 0.5 | 펄스당 이동거리 = 둘레 / 펄스 수. 실제 센서 장착 후 맞춤 |
| `maxValidSpeedKmh` | 40 | 이보다 빠르게 환산되는 펄스 증가는 잡음으로 무시 |
| `simulateSpeedKmh` | 12 | 장치 없이 `wheels[].simulateKey`를 누르고 있을 때의 속도 |
| `wattPerKmh`, `maxWatt` | 15, 300 | 환산 출력(W) = min(maxWatt, 속도 km/h × wattPerKmh) |
| `saleScaleMinutes` | 60 | 판매는 "이 평균 출력으로 이 시간(분) 달렸을 때"의 발전량으로 계산 |
| `smpWonPerKWh` | 150 | 기준 판매단가(원/kWh) |
| `voltages[]` | 낮은/높은 전압 | `lossRate` 송전손실 비율, `facilityCostWon` 승압·강압 설비 비용(판매금액에서 차감) |
| `transmitSeconds` | 6 | 송전 시뮬레이션 연출 시간(초) |
| `idleTimeoutSeconds` | 60 | 안내·선택·결과 화면에서 무입력 시 대기화면 복귀(초) |
| `resultAutoCloseSeconds` | 20 | 결과 화면 자동 넘김(초) |
| `wheels[]` | 2대 | 쳇바퀴 번호, 센서 컨트롤러 id, 32형 디스플레이 번호(0부터), 시뮬레이션 키 |
| `spectatorDisplayIndex` | 2 | 85형 관전 화면 디스플레이 번호 |

계수는 모두 체험용 임시값입니다. 관람객 화면에는 정량 계수를 표시하지 않습니다.
