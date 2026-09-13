> XLSX 전환 완료: 현재 데이터는 Assets/Resources/Data/game_data.xlsx의 event_catalog / stage_list 시트에서 수정합니다. 기존 CSV는 삭제했습니다. 아래 CSV 관련 안내는 이전 방식이며, 최신 안내는 Assets/Resources/Data/game_data-사용안내.md를 확인하세요.

# Event List Scene 편집 안내

- 진입 씬: `Assets/Scenes/New/Event List Scene.unity`
- 이벤트 메인: `Assets/Scenes/New/Main Scene.unity`
- CSV: `Assets/BeastBeat/Data/event_catalog.csv`
- 목록 버튼 프리팹: `Assets/BeastBeat/Prefabs/EventCatalogRow.prefab`

## 버튼 연결

기존 Canvas의 **Event Entry Actions** 컴포넌트에 UI 참조와 CSV가 연결되어 있습니다. 각 버튼의 Inspector > Button > On Click()에서 연결을 직접 확인하고 수정할 수 있습니다.

| 버튼 | On Click 함수 | 동작 |
| --- | --- | --- |
| Btn_permanent | ShowPermanent | 상시 목록 |
| Btn_limited | ShowLimited | 한정 목록 |
| Btn_Goevent | OpenMainScene | Main Scene |
| Btn_Reward | OpenLevelRewards | rw_level (뒤로 가면 Event List Scene) |
| Btn_Goback / Btn_Gocity | 없음 | Interactable 해제 |

기존 버튼·이미지·텍스트 오브젝트는 유지했습니다. 버튼 스프라이트는 변경하지 않았고, 새 목록 Button의 Image에는 스프라이트가 없습니다. 기존 main/eventlist 씬 파일은 남겨두되 사용 씬 등록을 해제했고 실제 이동 경로는 새 씬으로 바꿨습니다. Event List에 복사되어 있던 Main Scene Actions 컴포넌트는 비활성화했습니다.

## CSV 데이터 추가

Excel 등에서 **CSV UTF-8** 형식으로 저장하세요. 헤더를 유지하고 한 행씩 추가하면 됩니다. 상시·한정 각 12개는 목록/스크롤 확인용 샘플이며 모든 이동 버튼은 요청대로 동일한 Main Scene으로 연결됩니다.

| 열 | 입력 |
| --- | --- |
| id | 중복되지 않는 양수 |
| category | permanent = 상시 / limited = 한정 |
| updated_at | yyyy-MM-dd. 날짜 오름차순, 같은 날짜는 id 오름차순 정렬 |
| title | 목록과 상세 제목 |
| description | 상세 설명. 셀 내 줄바꿈 또는 `\n` 사용 가능 |
| enabled | 1 = 표시 / 0 = 숨김 |
| background | 선택 사항. Resources 아래 Sprite 경로, 확장자 제외. 빈칸은 기존 배경 |
| reward_ids | 기존 game-data.json의 items ID를 세미콜론으로 구분. 최대 6개 |
| achievement_ids | 기존 game-data.json의 achievement ID. 세미콜론 구분, 최대 3개 |

쉼표나 줄바꿈을 포함한 셀은 큰따옴표로 감싸고, 셀 안의 큰따옴표는 두 번 적습니다. Excel에서 CSV로 저장하면 자동 처리됩니다. 잘못된 ID·분류·날짜·참조 ID는 Console과 상세 설명에 오류를 표시합니다.

Unity가 파일 변경을 임포트하면 **Play 중 0.5초 이내에 갱신**됩니다. 편집 모드에서 미리 반영하려면 Canvas의 Event Entry Actions 컴포넌트 메뉴에서 **Reload CSV / Update Scene Preview**를 실행하세요. Unity 자동 새로고침이 꺼져 있으면 CSV를 우클릭하여 Reimport 하세요.

## 오브젝트로 디자인 수정

`Scrview_Eventlist > Viewport > Content` 아래에 실제 Button 오브젝트 12개가 저장되어 있어 Play 전에도 보입니다. 각 행은 `EventCatalogRow.prefab`을 사용합니다. 프리팹 안의 Image·Button·Txt_EventTitle·Img_Selected를 수정하면 공통 디자인을 바꿀 수 있습니다. 행 높이는 Layout Element, 간격은 Content의 Vertical Layout Group에서 수정합니다.

CSV 로직은 이 프리팹을 복제하고 데이터·선택 상태를 갱신합니다. 새 Canvas나 전체 화면을 코드로 생성하지 않습니다. 기존 Content의 다른 사용자 오브젝트는 건드리지 않습니다. 데이터 행이 줄면 여분의 목록 버튼을 비활성화해 재사용합니다.

보상 칸은 기존 6개 Image를 유지하고 아이템 이름 Text를 추가한 미리보기입니다. 현재 아이템 아이콘 자산이 없어 이름과 등급 색으로 구분합니다. 기존 Image에 아이콘을 직접 넣을 수 있으며 런타임에서 보상 Image의 스프라이트를 덮어쓰지 않습니다. 업적 수치는 기존 게임 진행 데이터와 연결되어 있습니다.

한글을 추가해도 표시되도록 기존 NanumSquareNeo SDF의 Multi Atlas Textures를 켰습니다. 기존 폰트와 Outline/Underlay 재질 설정은 유지합니다.

## 검증

실행 파일 빌드 없이 Unity Play Mode에서 상시/한정 각 12개, 목록 선택, 스크롤, 버튼 클릭 대상, CSV 행 추가·복원 자동 반영, Event List ↔ Main Scene, Event List ↔ rw_level을 확인했습니다.
