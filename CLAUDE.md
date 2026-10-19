# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 개요

산나비(Sanabi) 스타일 2D 픽셀아트 플랫포머. Unity 6 (6000.0.34f1) + URP 17 **2D Renderer**. 캐릭터는 Sprite, 맵은 Tilemap.
사용자의 주 관심사는 **셰이더 개발**이다 (손으로 쓴 HLSL). 사용자와는 한국어로 대화하고, 코드 주석도 한국어로 쓴다.

- Unity 프로젝트는 한 단계 안쪽 폴더에 있다: `2DPlatformer/` (여기에 `Assets`, `ProjectSettings`, `Packages`).
- 작업 코드는 전부 `2DPlatformer/Assets/_Project/` 아래. `Assets/ThirdParty/` 는 원본 에셋(Craftpix Cyberpunk 캐릭터 48x48, Warped City)이며 수정하지 않는다.
- **깃허브에 올리지 않는 프로젝트다.** git 초기화, .gitignore, LFS, 원격 저장소 관련 제안을 하지 않는다.

## 빌드 · 실행 · 검증

빌드/린트/테스트 스크립트는 없다. 씬과 에셋은 대부분 **에디터 메뉴 `Tools > Project Setup > ...` 의 셋업 코드**(`_Project/Editor/*.cs`)가 만든다. 단계별 메뉴:

| 메뉴 | 내용 |
|---|---|
| Step 1 (Run All) | 레이어, 픽셀아트 임포트 규칙, Sandbox 씬 |
| Step 2 (Test Map) | TestMap 씬 + 타일 팔레트 (지형 타일은 사용자가 직접 칠함) |
| Step 3~7 | 플레이어, 디버그 오버레이, 벽 슬라이드, 카메라 추적, 카메라 구역 |
| **Step 8~26 (Run All)** | 셰이더·노멀맵·조명·포스트FX·갈고리·전투·적·FX·폴리시를 한 번에 (`GameplaySetup.RunAll`) |
| Step 27 (Build Levels) | `Levels/Level*.txt` → `Scenes/LevelXX.unity` 전부 생성 + `Resources/LevelCatalog` + 월드 FX 머티리얼. 플레이어에 GrappleHook 이 없으면 Run All 을 먼저 실행 |
| Step 28 (Title & UI) | UI 머티리얼(`Resources/UI`), `Levels/Title.txt` → `Scenes/Title.unity`, Build Settings 순서(Title → Level01 → …). 카탈로그가 없으면 Step 27 을 먼저 실행 |

셋업 메뉴는 **멱등**하게 작성한다 (다시 실행해도 중복 생성되지 않게 찾고 → 없으면 만들기). 새 기능도 같은 방식으로 셋업 메뉴를 추가하는 것이 이 프로젝트의 관례다.

**레벨 수정:** `Levels/LevelXX.txt` (기호·지시어 설명은 파일 맨 위 주석)를 고치고 Step 27 을 다시 실행. 레벨 씬은 통째로 다시 만들어지므로 씬에서 직접 고친 내용은 사라진다. 섹터 순서는 파일 이름 순서다. TestMap 은 사용자가 손으로 칠한 맵이므로 덮어쓰지 않는다.
- 섹터 4개(약 10분): 01 네온 옥상(튜토리얼, `@hint` 안내) / 02 하층 배관 지구 / 03 타워 외벽(세로) / 04 코어 첨탑(최종).
- 레벨 설계 기준(실측 물리): 점프 높이 3.25칸이라 단차는 3칸 이하, 같은 높이 틈은 4칸 이하. 벽 하나로는 못 오르고 벽 점프 통로는 폭 4칸. 갈고리 사거리 11 — 고정점은 발판보다 6~8칸 위. 바닥 높이의 전기 띠는 몸이 닿아 사실상 더 넓은 틈이 되므로 얕은 구덩이 안에 둔다. 턱 위로 올리려면 턱 모서리를 `H` 로 두고 감기(Shift)로 튀어 오르게 한다.

**배치 모드 검증:** 사용자가 에디터를 열어 두면 프로젝트가 잠겨서 배치 모드를 직접 돌릴 수 없다. 그래서 `Assets`/`ProjectSettings`/`Packages` 를 임시 폴더로 robocopy 한 복사본에서 실행한다:

```
"C:/Program Files/Unity/Hub/Editor/6000.0.34f1/Editor/Unity.exe" -batchmode -projectPath <복사본> -logFile <log> -executeMethod <정적 메서드>
```

로그에서 `error CS`, `Shader error`, `Exception` 을 grep 해 컴파일·셰이더 오류를 확인한다. 이전 세션에서는 복사본에만 넣은 하네스(메뉴 실행 → 플레이 모드 진입 → 시나리오 단계 실행 → `RenderPipeline.StandardRequest` 로 PNG 캡처)로 동작을 확인했다. 하네스는 프로젝트에 포함되어 있지 않으니 필요하면 복사본에 다시 만든다. 검증 전용 코드를 실제 프로젝트에 넣지 않는다.

## 아키텍처

### 플레이어 (`Scripts/Player`)
- `PlayerController` 가 직접 만든 FSM(`PlayerState` 상속: Grounded / Airborne / WallSlide / Hooked / Attack / Hurt / Execution)을 돌린다. 물리는 상태의 `FixedTick` 에서 처리한다. Rigidbody2D 속도를 직접 설정하며 물리 엔진 마찰에 의존하지 않는다.
- 튜닝 값은 ScriptableObject(`MovementConfig`, `HookConfig`, `CombatConfig`, `CameraConfig`)에 있다. 코요테 타임, 점프 버퍼, 조준 보정 같은 플레이 보정도 여기서 조정한다.
- `PlayerInputReader` 는 Input System 액션을 읽어 "누른 시각"을 기록한다(버퍼링). 상태는 `Consume*()` 로 입력을 소비한다. `SimulatePress` / `SimulatedHookHeld` / `SimulatedJumpHeld` / `SimulatedMoveX` 는 검증·AI 용이다. `InputEnabled` 로 메뉴 동안 게임 입력을 끈다.
- `GrappleHook`: 조준 방향 부채꼴 레이캐스트로 걸 곳을 찾고(적 우선 → 처형), 끝이 실제로 날아가 닿은 뒤 붙는다. 스윙의 로프 구속은 `HookedState` 가 직접 구현한다(Joint 미사용).

### 지형 · 레벨
- 충돌은 **단일 `Collision` 타일맵**(CompositeCollider). 갈고리 가능 여부는 레이어가 아니라 **타일별 `SurfaceTile.Hookable`** 로 정한다. 타일맵을 나누면 이음새에 캐릭터가 걸리기 때문이다. `Hazard` 타일맵에는 `HazardZone` 이 붙는다.
- `LevelBuilder` 는 TestMap 을 복사해 레벨 씬(과 타이틀 배경)을 만든다. 텍스트 레이아웃을 파싱해 지형 오토타일, 파사드 스탬프(`Visual_Detail`), 소품, 적, 라이트, 물웅덩이, `CameraRoom` 을 배치한다. Warped City 에 바닥 타일이 없어서 지형 타일 텍스처는 `TerrainTileGenerator` 가 코드로 그린다.
- 카메라: 직접 만든 `CameraController`(데드존, 룩어헤드, 트라우마 기반 흔들림 `CameraController.Shake`). `CameraRoom` 은 영역 제한, `CameraLockZone` 은 고정·추적 시작 트리거다. PixelPerfectCamera(PPU 16, 640x360, PixelSnapping)가 뷰를 스냅하므로 카메라 코드는 float 위치를 유지한다.

### 게임 흐름 · UI (`Scripts/Game`, `Scripts/UI`)
- `GameFlow`(자동 생성, DontDestroyOnLoad): 레벨 씬이 열리면 기록(시간·사망·처치·데이터 칩)을 시작하고 `GameUI` 를 만든다. 일시정지(`TimeManager.Paused` + `PlayerInputReader.InputEnabled`), 섹터 클리어·해금·최고 기록(PlayerPrefs), `ScreenWipe` 씬 전환. 섹터 순서는 `LevelCatalog`.
- 월드 오브젝트: `Checkpoint`(K, 체력 회복·재시작 위치), `LevelExit`(G), `ArenaGate`(|, 같은 `@room` 의 적이 다 쓰러지면 열림), `DataChip`(o), `HintZone`(@hint). 레벨의 적은 `respawnDelay = 0` 으로 배치된다.
- 위험 지대·낙사: 피해 후 `PlayerController.ReturnToSafeGround` 로 마지막 안전 발판에 돌아간다(깊은 가시 구덩이에서 갇히지 않게). 체력 0 이면 체크포인트에서 다시 시작.
- UI 는 프리팹 없이 런타임에 코드로 조립한다(`UIFactory`). 도형은 `CyberGraphic`(MaskableGraphic) → `Platformer/UI/Cyber` 셰이더(SDF 패널·버튼·칸 게이지·전환 화면, 아트 픽셀 3단위 격자), 글자는 legacy `Text` + `TextGlitch`(잔상 사본·uv1) → `Platformer/UI/CyberText`. 캔버스는 TexCoord1~3 채널을 켜야 한다. 글꼴은 OS 동적 글꼴(Bahnschrift/맑은 고딕/Consolas). 효과음은 `UISound` 가 코드로 합성한다.
- `GameSettings`: 볼륨·화면 모드·해상도·수직 동기화·화면 흔들림·화면 효과 강도·타이머 (PlayerPrefs). 흔들림·효과 강도는 `CameraController.Shake` / `PostFX` / `FXPostFeature` 가 곱해 쓴다.

### 렌더링 · 셰이더 (`Shaders/`, `Scripts/FX`, `Scripts/Rendering`)
- 거의 모든 스프라이트·타일은 **`Platformer/SpriteLitFX`** 셰이더를 쓴다. `Universal2D` 패스에서 2D 라이트 텍스처를 직접 샘플링하고(`Include/ShapeLights2D.hlsl`), 한 셰이더 안에서 다음을 처리한다:
  - 피격 플래시, 림 라이트, 외곽선, 글리치, 디졸브
  - 슬로우 모션 채도 감소, 자체 발광, 갈고리 하이라이트, 깊이 안개
- 공통 코드는 `Include/` 에 있다: `SpriteLitFXInput`(머티리얼 프로퍼티), `SpriteFXCore`(합성), `FXCommon`(전역).
- 오브젝트별 효과는 `SpriteFX` 가 MaterialPropertyBlock 으로 설정한다. 전역 효과(시간, 슬로우, 갈고리 대상 등)는 `FXGlobals` 가 `RenderPipelineManager.beginContextRendering` 에서 `Shader.SetGlobal*` 로 넣는다.
- 노멀 패스는 직접 구현했다. URP 기본 스프라이트 노멀 패스는 scale.x = -1 일 때 Y 가 뒤집히기 때문에 오브젝트 축으로 tangent/bitangent 를 만든다. 노멀맵 텍스처는 `NormalMapGenerator` 가 생성한다.
- 포스트 FX: `FXPostFeature`(RenderGraph `RecordRenderGraph` + `AddBlitPass`, BeforeRenderingPostProcessing) + 볼륨 컴포넌트 `FXPostSettings` + 게임플레이용 이벤트 API `PostFX`(글리치, 피격 비네트, 방사 블러 등).
- 이펙트 셰이더: Slash, HitSpark, Dust, Afterimage, Chain, Rain, Puddle, Billboard, Beacon(체크포인트·출구 빛기둥), EnergyGate(게이트), DataChip. UI 셰이더는 `Shaders/UI/`(UGUI 구조라 CG + UnityUI.cginc). 파티클은 커스텀 버텍스 스트림(UV, AgePercent, StableRandom → TEXCOORD0.xyzw)을 쓴다. 슬래시 왜곡과 물웅덩이 반사는 Camera Sorting Layer Texture 를 쓴다.
- 이펙트 생성은 `FXLibrary` + `FXPool` + `TimedFX` 가 맡는다. 시간 연출(히트스톱, 슬로우 모션; fixedDeltaTime 도 함께 조정)은 `TimeManager` 가 맡는다.

## 알려진 함정

- **2D 라이트:** Multiply 블렌드 라이트는 어두운 픽셀아트에서 거의 보이지 않는다. 네온 라이트는 blend style 1(Additive)을 쓰고, 림 라이트도 additive 라이트 텍스처의 기울기로만 계산한다.
- **Light2D 레이어 순서:** target sorting layer 를 `lightType = Global` 지정 **전에** 설정해야 "more than one global light" 오류가 나지 않는다.
- **HLSL 변수명:** `distance`, `dot`, `line` 같은 내장 함수 이름을 변수명으로 쓰지 않는다.
- **에디터 스크립트:**
  - `EditorSceneManager.NewScene` / `OpenScene(Single)` 은 쓰지 않는 에셋을 언로드한다. 씬을 연 뒤에는 에셋 참조를 경로로 다시 로드한다.
  - `GetComponent` 결과에 `?.` / `??` 를 쓰지 않는다 (Unity fake null). `== null` 로 검사한다.
  - `FindObjectsByType` 의 순서는 보장되지 않는다.
- **타일맵 콜라이더:** 코드로 타일을 칠한 직후 저장하면 `CompositeCollider2D` 에 이전 형상이 남는다(시작하자마자 땅을 뚫고 떨어지던 버그). 저장 전에 `TilemapCollider2D.ProcessTilemapChanges()` + `CompositeCollider2D.GenerateGeometry()` (LevelBuilder.RebuildColliders).
- **배치 모드:** `EditorUtility.DisplayDialog` 는 배치 모드에서 false 를 돌려준다 — 확인 창은 `!Application.isBatchMode` 일 때만 띄운다. 복사본은 ProductName 을 바꿔야 PlayerPrefs(기록·설정)가 실제 프로젝트와 섞이지 않는다.
- **UI 겹침:** 메뉴 패널은 반투명이라 뒤의 HUD·토스트가 비친다. 메뉴가 열리면(`UIScreen.Top != null`) 숨긴다.
- **PowerShell 5.1:** BOM 없는 UTF-8 `.ps1` 을 CP949 로 읽어 한글이 깨진다. `Invoke-Expression ([IO.File]::ReadAllText(path, [Text.Encoding]::UTF8))` 로 실행한다.
- **사용자 에디터 반영:** 셋업 메뉴는 Claude 가 사용자의 에디터에서 대신 누를 수 없다. 변경 후에는 어떤 메뉴를 실행해야 하는지 사용자에게 알려 준다.
