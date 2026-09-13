# game_data.xlsx

데이터 파일은 `Assets/Resources/Data/game_data.xlsx` 한 개입니다.

| 시트 | 기존 데이터 | 현재 행 수 |
| --- | --- | --- |
| event_catalog | 이벤트 목록 | 24 |
| stage_list | 스테이지 목록 | 11 |

기존 CSV의 컬럼 순서, ID, 텍스트, 빈칸과 셀 안 줄바꿈을 유지했습니다. `updated_at`은 Excel 날짜 셀이며 Unity에서는 기존 yyyy-MM-dd 문자열로 읽습니다. ID와 종류/속성 코드는 정수 셀입니다. 시트 이름과 첫 행의 컬럼 이름은 유지하세요.

## 수정 방법

1. Excel에서 game_data.xlsx를 엽니다.
2. 원하는 시트에서 값을 수정하거나 마지막 행 아래에 새 데이터를 추가합니다.
3. 저장하고 Unity로 돌아옵니다. Unity가 XLSX를 다시 임포트합니다.
4. Play 중이면 최대 약 0.5초 뒤 목록과 상세 정보가 갱신됩니다. 편집 모드 미리보기는 Canvas의 Event Entry Actions 또는 Stage List Actions 컴포넌트 메뉴에서 **Reload Workbook / Update Scene Preview**를 실행하세요.

자동 임포트가 꺼져 있으면 Project 창에서 game_data.xlsx를 우클릭하여 Reimport 하세요. 첫 행 위에 제목 행을 추가하지 말고, 데이터 셀에는 수식 대신 값을 입력하세요. 빈 데이터 행은 건너뜁니다. 잘못된 ID/분류/속성이나 누락된 시트는 오류로 표시됩니다.

## 시트별 코드

- event_catalog의 category: `permanent` 상시 / `limited` 한정
- event_catalog의 enabled: `1` 표시 / `0` 숨김
- event_catalog의 reward_ids, achievement_ids: ID를 세미콜론으로 구분
- stage_list의 종류: `1` 워밍업 / `2` 예선전 / `3` 본선 / `4` 결승전
- stage_list의 npc 속성: `1` 물리 / `2` 불 / `3` 얼음 / `4` 전기 / `5` 에테르
- stage_list의 이벤트 아이디: 현재 이벤트는 `1`

적 레벨·경험치·전투 파티·보상 수량은 기존 `Assets/BeastBeat/Resources/Image/game-data.json` 설정을 계속 사용합니다. 신규 스테이지 ID는 같은 종류의 기존 설정을 기본값으로 사용합니다.

## Unity 연결

XLSX 파일을 Unity의 GameWorkbook 에셋으로 임포트합니다. 두 씬의 Workbook 필드와 Resources 경로 `Data/game_data`가 이 파일을 가리킵니다. 별도 CSV 파일을 생성하거나 읽지 않습니다. UI 오브젝트와 Button의 On Click 연결은 유지했습니다.

기존 `event_catalog.csv`와 `stage_list.csv` 및 각 meta 파일은 프로젝트에서 제거하여 휴지통으로 이동했습니다.

## 확인한 내용

원본 두 CSV와 XLSX의 모든 셀을 대조했습니다. Unity Play에서 이벤트 상시/한정 목록, 스테이지 네 종류, XLSX 저장 변경에 따른 이벤트 제목과 NPC 이름 갱신을 확인한 뒤 검증용 값을 원본으로 복원했습니다. 실행 파일 빌드는 하지 않았습니다.
