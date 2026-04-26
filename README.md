# Unity Data Loader

데이터 로드 후 Container 관리까지 진행하는 프레임워크  
구글 스프레드시트를 Unity 게임 데이터 테이블로 가져오는 에디터 툴 제공  
**기획자가 시트를 수정 → 프로그래머가 에디터에서 "최신화" 한 번 → 런타임에서 타입 세이프하게 사용**.

## 프로젝트 의도

구글 스프레드시트를 게임 DB로 사용했을 때의 **장단점을 직접 검증하고, 시트 로드 방식을 학습**하기 위해 진행한 프로젝트.
실시간 갱신/협업이라는 매력에 비해 실제 워크플로우에 통합했을 때의 비용이 어느 정도인지 검토하려 했다.

## 사용 방법

### 1. Google Cloud Console 준비 (최초 1회)

1. [Google Cloud Console](https://console.cloud.google.com)에서 프로젝트 생성.
2. **APIs & Services → Library**: Google Sheets API + Google Drive API 사용 설정.
3. **OAuth consent screen**: User Type `External`, Test users에 본인 Google 계정 등록.
4. **Credentials → OAuth client ID**: Application type `Desktop app`으로 발급 → Client ID / Secret 확보.

### 2. Unity 에디터에서 인증

`Tools > Google Sheet Data Loader` →
**[구글 연동]** 탭에서 Client ID / Secret 입력 후 **인증(OAuth)** 클릭 → 브라우저 로그인 → 권한 동의.
한 번 인증되면 refresh token으로 자동 갱신되어 재로그인이 필요 없다.

### 3. 시트 URL 입력 & 동기화

**[DB 로드]** 탭에서 스프레드시트 URL 입력 후 **구글 시트 DB 최신화** 클릭.
→ 모든 시트가 `Assets/Resources/GoogleSheetData/*.json`으로 저장되고, enum 시트는 C# 코드로 자동 생성된다.

이후 시트가 변경될 때마다 **"최신화" 버튼만 누르면** 게임 데이터가 갱신된다.

### 4. 런타임 사용

`DataManager`는 두 가지 접근 방식을 제공한다.

**(A) 저수준 — `DataTable` 직접 조회**

```csharp
await DataManager.Instance.InitializeAsync();

DataTable table = DataManager.Instance.GetTable("Monster");

int hp        = table.GetInt(0, "Hp");
float speed   = table.GetFloat(0, "MoveSpeed");
MonsterType t = table.GetEnum<MonsterType>(0, "Type");
int[] drops   = table.GetIntArray(0, "DropItems");
```

**(B) 권장 — 타입 세이프 `DataContainer<TKey, TValue>`**

`InitializeAsync`가 끝나면 모든 `DataContainer` 구현체가 리플렉션으로 자동 등록·로드된다.

```csharp
await DataManager.Instance.InitializeAsync();

var stageContainer = DataManager.Instance.GetContainer<StageContainer>();

Stage stage = stageContainer.Get(1001);   // Dictionary 기반 키 조회
int spawn   = stage.SpawnTileCount;
int[] lv    = stage.StageLevel;

foreach (var kv in stageContainer.All) { /* ... */ }
```

## 설계 결정

### 인증 방식 — 왜 "웹에 게시"를 쓰지 않았나

가장 간단한 방법은 시트를 **웹에 게시(Publish to web)** 하는 것이지만,
이는 사실상 **DB 자체를 공개**하는 것과 같아 게임 데이터 유출 위험이 있다고 판단했다.

대신 **Google Cloud Console에서 OAuth 2.0 자격 증명을 발급**받고,
Sheets API에 Client ID / Secret으로 접근하는 구조를 선택했다.

- 시트는 **비공개 상태로 유지**, 본인 계정 권한으로만 접근.
- PKCE 기반 데스크톱 앱 플로우 → 한 번 인증하면 refresh token으로 자동 갱신.
- 스코프는 `drive.readonly` 최소 권한.

### Enum 파싱 — 왜 시트명을 직접 지정하게 했나

기본 타입(`int / float / bool / string` 및 배열) 파싱은 기본 제공하되,
**enum으로 파싱할 시트의 이름을 사용자가 직접 지정**할 수 있도록 분리했다.

이유: C#에는 `enum` 타입이 있고, 팀마다 enum 시트 네이밍 컨벤션이 제각각일 가능성이 크다.
(`_Enum`, `Game_Enum`, `Common_Enum` 등 팀 내부 협의에 따라 다양함)

이 부분을 하드코딩하지 않고 에디터 설정으로 빼두면 **팀 컨벤션을 그대로 따라가는 확장성**을 확보할 수 있다고 봤다.

### 데이터 컨테이너 — 왜 `DataTable` 위에 한 겹을 더 쌓았나

`DataTable.GetInt(row, "Hp")` 만으로도 데이터를 읽을 수 있지만,
**컬럼명을 문자열로 들고 다니는 코드는 시트 변경에 취약**하다 (오타·리네임 미감지·자동완성 불가).

그래서 시트 1장 = `IData` 1개 + `DataContainer<TKey, TValue>` 1개로 매핑하는 레이어를 한 겹 더 두었다.

- 시트 컬럼은 자동 생성된 **partial class**의 프로퍼티가 된다 → IDE 자동완성 + 컴파일 타임 검증.
- 컨테이너는 `Dictionary` / `List` 두 종류를 기본 제공 (`DictionaryContainer`, `ListContainer`).
- `IDataKey<T>`를 구현해 키 타입(int / string / enum…)을 자유롭게 선택.
- `DataManager`가 **리플렉션으로 모든 `DataContainer` 서브타입을 자동 등록**하므로,
  새 시트가 추가돼도 `DataManager` 코드는 손대지 않는다.
- 자동 생성 코드(`Generated/`)와 사용자 확장 코드(`PartialClass/`)를 폴더로 분리 →
  시트 재생성 시에도 **사용자 코드를 덮어쓰지 않는다**.

## 구조 개요

```
구글 스프레드시트
   │ (OAuth2 인증된 Sheets API)
   ▼
SheetJsonConverter  ──►  Assets/Resources/GoogleSheetData/*.json
EnumCodeGenerator   ──►  Assets/Scripts/GeneratedEnums/*.cs (+ asmdef)
   │
   ▼
DataManager (런타임)
   ├─ DataTable.GetInt / GetEnum / GetIntArray ...        (저수준)
   └─ DataContainer<TKey, TValue> 자동 등록 + 로드        (타입 세이프)
         └─ DictionaryContainer.Get(key) / ListContainer.GetByIndex(i)
```

| 모듈 | 역할 |
| --- | --- |
| `GoogleSheetDataLoaderWindow` | 에디터 UI, 인증/동기화 진입점 |
| `OAuth2Authenticator` / `OAuth2TokenStore` | PKCE 인증, access/refresh 토큰 분리 저장 및 자동 갱신 |
| `GoogleSheetsApi` | 시트 메타데이터 조회 + CSV 다운로드 |
| `SheetJsonConverter` | CSV → `SheetData` JSON 변환, `#` 접두사 시트/컬럼 제외 |
| `EnumCodeGenerator` | enum 시트 → C# 코드 생성, 별도 asmdef로 컴파일 격리 |
| `JsonParsing.DataTable` / `ValueParser` | JSON → 행/열 접근, 타입별 파서 |
| `GameData.DataManager` | 런타임 비동기 로드 + `DataTable` / `DataContainer` 통합 진입점 |
| `IData` / `IDataKey<T>` | 데이터 행/키 마커 인터페이스 |
| `DataContainer<TKey, TValue>` | 시트 1장을 묶어두는 베이스, `Parse(table, row)` 만 구현 |
| `DictionaryContainer` / `ListContainer` | 키 조회 / 순서 보존이 필요한 시트용 두 가지 표준 컨테이너 |

## 시트 작성 규칙

- **Row 1**: 컬럼명 (`#` 접두사 시 제외)
- **Row 2**: 타입 (`int`, `float`, `bool`, `string`, `enum`, `int[]` …)
- **Row 3+**: 데이터
- 배열 구분자: `|`  (예: `1|2|3` → `int[] { 1, 2, 3 }`)
- 시트명 `#` 접두사 → 변환 제외 (작업/임시 시트용)
- enum 시트는 컬럼 단위로 enum 정의 — Row 1: enum 타입명 / Row 2+: enum 멤버

## 폴더 구조

```
Assets/
├─ Scripts/
│  ├─ GoogleSheetDataLoader/
│  │  ├─ Editor/   UI, OAuth, API, 변환기, 코드 생성기
│  │  └─ Runtime/  CsvParser (에디터·런타임 공유)
│  ├─ JsonParsing/    DataTable, ValueParser, SheetData (JSON 스키마)
│  ├─ Data/           타입 세이프 컨테이너 레이어
│  │  ├─ Base/        IData, IDataKey<T>, DataContainer<TKey,TValue>,
│  │  │               DictionaryContainer, ListContainer
│  │  ├─ Generated/   시트 → 자동 생성 (partial 데이터 클래스 + abstract 컨테이너)
│  │  │  └─ Containers/
│  │  ├─ PartialClass/ 사용자 확장 (concrete 컨테이너, partial 데이터 메서드)
│  │  └─ DataManager.cs  런타임 진입점 (컨테이너 자동 등록·로드)
│  └─ GeneratedEnums/ 자동 생성된 enum + asmdef
└─ Resources/
   └─ GoogleSheetData/  자동 생성된 JSON 데이터 테이블
```

### `Data/` 폴더 세부

| 영역 | 내용 | 누가 작성 |
| --- | --- | --- |
| `Base/IData`, `IDataKey<T>` | 데이터 행을 식별하는 마커 인터페이스 | 프레임워크 |
| `Base/DataContainer<TKey,TValue>` | 컨테이너 추상 베이스 (`Load(table)` 흐름 정의) | 프레임워크 |
| `Base/DictionaryContainer<TKey,TValue>` | `Get(key)` / `TryGet` / `All` 제공 | 프레임워크 |
| `Base/ListContainer<TKey,TValue>` | `GetByIndex(i)` / `GetByKey` / 지연 인덱스 캐싱 | 프레임워크 |
| `Generated/Stage.cs`, `StageLevel.cs` … | 시트 컬럼 = `partial class` 프로퍼티 + `__Parse(table,row)` | **자동 생성** |
| `Generated/Containers/StageDictionaryContainer.cs` … | abstract 컨테이너 (시트명, key 컬럼, Parse 흐름) | **자동 생성** |
| `PartialClass/StageContainer.cs` 등 | `class StageContainer : StageDictionaryContainer { }` — 구체 클래스 1줄 | 사용자 |
| `PartialClass/Stage.cs` 등 | 파생 프로퍼티·헬퍼 메서드 등 도메인 로직 (선택) | 사용자 |
| `DataManager.cs` | 모든 컨테이너 인스턴스화 + `DataTable` 매핑 + 로드 | 프레임워크 |

> **포인트**: 시트를 다시 로드해도 `Generated/`만 덮어써지고 `PartialClass/`는 그대로다.
> 새 시트가 추가되면 `PartialClass/`에 **빈 concrete 클래스 한 줄**만 만들어주면 자동 등록된다.

## 요구 사항

- Unity 2020.1+
- Google Cloud Project + OAuth Client(Desktop app), Sheets/Drive API 활성화
- OAuth consent screen에 본인 Google 계정을 Test user로 등록

## 라이선스

[LICENSE](LICENSE) 참조.
