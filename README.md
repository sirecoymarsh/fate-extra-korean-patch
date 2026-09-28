# Fate/EXTRA 한국어 패치

PSP 일본판 Fate/EXTRA(NPJH50247)용 비공식 한국어 패치입니다.

**[최신 배포와 다운로드](https://github.com/sirecoymarsh/fate-extra-korean-patch/releases/tag/v8h-hd-v54-ui-v4)** — 본편 v8h · HD v54 · 선택 UI v4 · 런처 1.6.0

**[런처 내려받기](https://github.com/sirecoymarsh/fate-extra-korean-patch/releases/latest/download/Fate-Extra-Korean-Launcher.zip)** — 받을 파일은 이것 하나입니다. 배포 페이지의 나머지 파일은 런처 없이 수동 설치할 때만 필요합니다.

## 설치

런처 1.6.0은 켜면 일본판 원본 ISO·PPSSPP·메모리스틱을 스스로 찾고, 「전체 설치」 또는 「본편만」을 고른 뒤 「설치」 한 번으로 끝납니다. 설치 방식은 **한국어 ISO 파일 만들기**가 기본이고, **외부 데이터 로딩 · ISO 생성 안 함**은 「고급」에 있습니다. 외부 방식은 실행할 때도 원본 ISO가 필요하며 게임은 런처에서 실행합니다. 방식을 바꾼 뒤에는 설치를 다시 눌러야 적용됩니다. [런처 1.6.0 릴리즈](https://github.com/sirecoymarsh/fate-extra-korean-patch/releases/tag/launcher-v1.6.0)

16MB 이상 파일은 **4개 구간을 동시에 다운로드**합니다. 각 구간을 이어받고 합친 파일을 검증하며, 서버가 구간 전송을 지원하지 않으면 일반 다운로드로 전환합니다.

런처 ZIP을 새 폴더에 풀고 실행합니다. 처음 실행할 때 「Windows의 PC 보호」 창이 뜨면 「추가 정보 → 실행」을 누르세요. ZIP 파일의 속성에서 「차단 해제」를 체크하고 풀면 이 창이 뜨지 않습니다. 찾아 놓은 경로를 확인하고 **한국어 패치 설치**를 누르면 됩니다. HD/UI/치트/클리어 세이브는 PPSSPP 메모리스틱 폴더가 필요합니다.

| 선택 | 화면 |
| --- | --- |
| 본편 | 한국어 대사·설명 + 원래 영문 UI |
| 본편 + HD | 고화질 그림·글꼴 + 원래 영문 UI |
| 본편 + HD + UI 한국어화 | 고화질 + 한국어 UI·아이콘·전투 메시지 |

시작할 때 최신 배포를 확인하며 다운로드·적용은 버튼을 눌러야 진행됩니다. 런처 실행 파일 자체는 자동 교체하지 않습니다. 이미 적용한 UI 한국어화는 HD 업데이트 시 유지되며, 원래 UI로 돌아가려면 **UI 한국어화 해제**를 사용합니다.

## 이번 배포

- 본편 v8h: 문자열 재배치 과정에서 손상된 아처 2곳·세이버 13곳의 연출 명령을 원본으로 복원했습니다. 기존 v8g의 번역·글꼴·이름·매트릭스 수정은 유지합니다.
- HD v54·선택 UI v4: v8h 호환성 정보 갱신이며 그림과 매핑은 HD v53·UI v3과 같습니다.
- 런처 1.6.0: 첫 화면을 다시 짰습니다. 켜면 경로를 찾고, 전체 설치 또는 본편만을 고른 뒤 설치 한 번입니다. 외부 데이터 로딩은 고급에 있습니다.

이전에 멈추던 아처 사례 1건과 새 부팅 세션의 전투 진입·표시는 사용자가 정상 진행을 확인했습니다. 모든 아처·세이버 장면을 개별 재현한 검증은 아닙니다.

## PPSSPP 설정과 저장

HD/UI 설치 시 Fate/EXTRA 전용 설정에 렌더링 8배, MSAA 4배, 수직동기화·텍스처 교체 켜기를 적용합니다. 기존 JIT 설정과 전역 설정은 보존합니다. MSAA 지원과 성능은 백엔드·기기에 따릅니다. 설치·업데이트 전에는 PPSSPP를 종료하세요.

치트는 선택 설치입니다. PPSSPP의 치트 사용 기능만 켜며 개별 코드 19개는 모두 꺼져 있습니다. 클리어 세이브도 선택 설치이고 선택하지 않은 저장 데이터는 변경하지 않습니다. 동봉 클리어 세이브는 캐스터 Lv.52 / 37시간51분이며 치트 사용 이력이 있을 수 있습니다. 기존 같은 슬롯은 보관하세요.

새 ISO를 시작한 뒤 게임 타이틀에서 일반 저장을 불러오세요. 이전 PPSSPP 상태 저장에는 옛 실행 코드가 들어 있습니다.

## 파일과 지원 원본

- `Fate-Extra-Korean-Launcher-v1.6.0.zip` — 고정 이름 사본 `Fate-Extra-Korean-Launcher.zip`과 같은 파일
- `Fate-Extra-Korean-Base-v8h.zip`
- `Fate-Extra-Korean-HD-v54.z01` + `.zip` — 같은 폴더에 두고 ZIP에서 전체 압축 해제
- `Fate-Extra-Korean-UI-v4.zip` — 런처로 적용/해제

수동 본편 설치는 기본 팩의 `Apply-Patch.cmd`를 실행해 일본판 원본 ISO를 선택합니다. 원본을 보존하며 같은 폴더에 `Fate-Extra-Korean-v8h.iso`를 생성합니다. 수동 HD 설치는 기존 폴더를 백업하고 압축을 푼 `NPJH50247` 폴더를 메모리스틱의 `PSP/TEXTURES`에 넣은 뒤 PPSSPP의 텍스처 교체를 켭니다. 수동 UI 설치는 UI ZIP의 `files/KoreanHD`를 해당 폴더에 합치고 `korean-textures.ini`를 `textures.ini`로 이름을 바꿔 덮어씁니다.

지원 원본: NPJH50247, DISC_VERSION 1.01, 1,280,933,888바이트.

```text
SHA-256: 60399D610CBCDA96601374A2E621A22BB58505C403C6FA4221EEC5E87235667B
```

다른 판본·수정된 ISO·CSO·CHD는 직접 적용하지 않습니다. 게임 ISO와 에뮬레이터는 제공하지 않습니다.

검증 범위와 제한은 [KNOWN_ISSUES.md](KNOWN_ISSUES.md), 제작·참고 자료는 [CREDITS.txt](CREDITS.txt)를 확인하세요. 원작의 권리는 원저작권자에게 있습니다.
