> XLSX 전환 완료: 현재 데이터는 Assets/Resources/Data/game_data.xlsx의 event_catalog / stage_list 시트에서 수정합니다. 기존 CSV는 삭제했습니다. 아래 CSV 관련 안내는 이전 방식이며, 최신 안내는 Assets/Resources/Data/game_data-사용안내.md를 확인하세요.

# Stage List Scene 편집 안내

- 사용 씬: `Assets/Scenes/New/Stage List Scene.unity`
- 목록 CSV: `Assets/BeastBeat/Resources/Data/stage_list.csv`
- 목록 버튼 프리팹: `Assets/BeastBeat/Prefabs/StageCatalogRow.prefab`
- 연결 컴포넌트: Canvas의 **Stage List Actions**

기존 버튼·이미지·텍스트는 유지했습니다. 버튼에 스프라이트를 새로 지정하지 않았고, 새 목록 Button의 Image도 Sprite가 비어 있습니다. 기존 `stage_list.unity`는 파일을 삭제하지 않고 사용 씬 등록을 해제했습니다. Main Scene의 Btn_League와 전투·결과 화면의 스테이지 목록 복귀는 새 Stage List Scene을 사용합니다.

## 버튼 On Click()

| 버튼 | 함수 | 역할 |
| --- | --- | --- |
| Btn_Goback | GoBack | 진입 전 화면. 현재 Main Scene |
| Btn_Warmingup | ShowWarmingup | 종류 1 목록 |
| Btn_Pre | ShowPre | 종류 2 목록 |
| Btn_Tournament | ShowTournament | 종류 3 목록 |
| Btn_Final | ShowFinal | 종류 4 목록 |
| Btn_Battlestart | StartBattle | 선택한 스테이지로 battle 씬 진입 |

배틀 시작 버튼의 기존 실제 이름은 `Btn_Battlestart`여서 이름을 바꾸지 않고 사용했습니다. 목록 행도 Button의 On Click → StageCatalogRow.SelectStage로 연결됩니다. 이전 화면 기본 경로는 Stage List Actions의 Previous Scene에서 수정할 수 있습니다.

## CSV 컬럼: 정확히 6개

첫 줄과 컬럼 순서를 유지하고 **CSV UTF-8**로 저장하세요.

```csv
아이디,이벤트 아이디,종류,등장 npc,npc 대사,npc 속성
1,1,1,점원,환영해요! 파트너와 호흡을 맞춰볼까요?,2
```

| 컬럼 | 의미 |
| --- | --- |
| 아이디 | 중복 없는 양수. 오름차순 정렬 및 전투/보상 참조 ID |
| 이벤트 아이디 | 현재 이벤트는 1. 컴포넌트의 Event Id와 일치하는 행만 표시 |
| 종류 | 1 워밍업 / 2 예선전 / 3 본선 / 4 결승전 |
| 등장 npc | 목록에 `배틀! ‘NPC’`로 표시 |
| npc 대사 | NPC 대사. 화면에서는 최대 30자, 한 줄로 표시 |
| npc 속성 | 1 물리 / 2 불 / 3 얼음 / 4 전기 / 5 에테르 |

이름 컬럼을 추가하지 않았습니다. 상세 제목은 종류별 ID 순서에 따라 `예선전 배틀 01`처럼 자동 생성됩니다. 목록 NPC 이름은 최대 10자 범위로 표시합니다. 쉼표를 포함한 셀은 큰따옴표로 감싸고, 셀 안의 큰따옴표는 두 번 적습니다. Excel에서 저장하면 자동 처리됩니다.

CSV는 기존 전투 데이터 11개를 옮겼습니다: 워밍업 1 / 예선 5 / 본선 4 / 결승 1. 플레이 기록과 연결되므로 기존 아이디는 가능하면 유지하고, 새 행에는 새 아이디를 사용하세요.

## 갱신과 전투 데이터

Unity가 CSV 변경을 임포트하면 Play 중 0.5초 이내에 목록·상세 정보가 갱신됩니다. 자동 새로고침이 꺼져 있으면 CSV 우클릭 → Reimport 하세요. 편집 모드 미리보기는 Stage List Actions 컴포넌트 메뉴의 **Reload CSV / Update Scene Preview**를 실행합니다.

CSV에는 요청한 6개 컬럼만 있습니다. 적 레벨·경험치·전투 파티·보상 수량은 `Assets/BeastBeat/Resources/Image/game-data.json`의 기존 ID 데이터를 사용합니다. 신규 ID는 같은 종류의 첫 기존 스테이지에서 기본 레벨·경험치·보상·적 인원수를 가져오고, 적 방부는 CSV 속성으로 배정합니다. 특정 새 스테이지의 전투 균형을 따로 설정하려면 JSON에 해당 ID의 stage_list와 stage_rewards를 추가하세요.

기존 잠금 규칙을 유지합니다. 같은 이벤트에서 앞선 ID의 스테이지를 클리어해야 도전할 수 있고, 파티 편성 및 참여 조건도 확인합니다. 잠긴 스테이지도 상세 정보를 볼 수 있으며, 배틀 버튼 아래가 아닌 상세 영역에 잠금 이유가 표시됩니다. 배틀 시작 시 마지막 플레이 스테이지를 저장하고, 재진입 시 해당 종류·행을 선택합니다.

## Hierarchy에서 디자인 수정

`Scrview_StageList > Viewport > Content` 아래에 실제 목록 Button이 저장되어 있습니다. StageCatalogRow 프리팹의 Image·Text·Layout Element를 편집하면 공통 디자인을 수정할 수 있습니다. 행 높이는 Layout Element, 간격은 Content의 Vertical Layout Group에서 조절합니다. CSV 연결 코드는 이 프리팹을 복제하고 데이터/선택 상태를 갱신합니다. 새 Canvas나 전체 화면을 코드로 그리지 않습니다.

NPC 초상화 및 속성/보상 이미지 원본은 유지했습니다. 현재 초상화·아이템 아이콘 자산이 지정되어 있지 않아 NPC 이름, 속성 이름과 색, 보상 이름과 수량을 네이티브 Text 오브젝트로 추가해 표시합니다. NPC Image에 Sprite를 지정하면 이름 대체 표시는 숨겨집니다. 속성 및 보상 Image에는 직접 아이콘을 넣을 수 있고, 연결 코드가 Sprite를 교체하지 않습니다.

## 검증

실행 파일 빌드 없이 Unity Play에서 네 종류 목록, NPC 선택·상성 표시, 클릭 대상, 빈 목록, CSV 추가 자동 반영, 7개 목록 스크롤, 추가 ID의 실제 배틀 진입, 배틀 복귀와 선택 위치 복원, Main Scene 왕복을 확인했습니다. 전투 진입 검증은 별도의 QA 저장 경로를 사용했고, 기존 사용자 저장 파일이 동일한 것을 확인했습니다.
