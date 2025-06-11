# reposcore-cs
A CLI for scoring student participation in an open-source class repo, implemented in C#.

**주의**: 처음 만들 때 dotnet 7.0으로 환경을 설정했다 8.0으로 설정을 바꿨기 때문에 그 이전에 Codespace를 생성한 경우 코드스페이스 vscode 에디터에서 `Ctrl-Shift-P`를 누르고 rebuild로 검색해서 Codespace rebuild를 선택하면 8.0 환경으로 다시 가상머신이 만들어집니다. (단, 기존에 컨테이너 안에 있는 파일들은 깃헙에 push하지 않은 내용들 중에 필요한 게 있으면 혹시 모르니 백업해 놓아야 ...)

## Usage
아래는 `dotnet run -- -h` 또는 `dotnet run -- -help`실행 결과를 붙여넣은 것이므로 명령줄 관련 코드가 변경되면 아래 내용도 그에 맞게 수정해야 함.

```bash
Usage: reposcore-cs [--verbose] [--output <Output directory>] [--format <Output format>...] [--token <Github token>] [--include-user <Include user's id>...] [--progress] [--help] [--version] repos0 ... reposN

reposcore-cs

Arguments:
  0: repos    분석할 저장소. "owner/repo" 형식으로 공백을 구분자로 하여 여러 개 입력 (Required)

Options:
  -v, --verbose                            자세한 로그 출력을 활성화합니다.
  -o, --output <Output directory>          출력 디렉토리 경로를 지정합니다. (default : "result")
  -f, --format <Output format>...          출력 형식 지정 ("text", "csv", "chart", "html", "all", default : "all")
  -t, --token <Github token>               GitHub 액세스 토큰 입력
  --include-user <Include user's id>...    결과에 포함할 사용자 ID 목록
  --progress                               API 호출 진행률을 표시합니다.
  --show-state-summary                     PR/Issue 상태 요약을 표시합니다.
  -h, --help                               Show help message
  --version                                Show version
```

## CLI 유틸리티 실행 예시

기본적인 명령행 인자 처리와 옵션을 다음과 같은 명령어로 시험삼아 실행해 볼 수 있습니다.
 
```bash
dotnet run -- owner1/repo1 owner2/repo2 owner3/repo3
dotnet run -- owner/repo --verbose
dotnet run -- owner1/repo1 owner2/repo2 owner3/repo3 --verbose
dotnet run -- owner/repo -f csv -f text
dotnet run -- --version
dotnet run -- --help
```

* 옵션 등을 정리해 나가는 단계이므로 실행 예시는 출력 결과가 계속해서 변경할 것이므로 일단 이전에 실행 예시 출력 결과들은 삭제하였음
* 위 명령어들은 `reposcore-cs.csproj` 파일이 위치한 최상위 디렉토리(`reposcore-cs`)에서 실행해야 정상적으로 동작합니다.
* 다양한 저장소를 실행하는 명령어는 해당 프로젝트의 저장소가 3개이기 때문에 3개로 반영하였습니다.

---

## Score Formula
아래는 PR 개수와 이슈 개수의 비율에 따라 점수로 인정가능한 최대 개수를 구하고 각 배점에 따라 최종 점수를 산출하는 공식이다.

- $P_{fb}$ : 기능 또는 버그 관련 Merged PR 개수 (**3점**) ($P_{fb} = P_f + P_b$)  
- $P_d$ : 문서 관련 Merged PR 개수 (**2점**)  
- $P_t$ : 오타 수정 Merged PR 개수 (**1점**)  
- $I_{fb}$ : 기능 또는 버그 관련 Open 또는 해결된 이슈 개수 (**2점**) ($I_{fb} = I_f + I_b$)  
- $I_d$ : 문서 관련 Open 또는 해결된 이슈 개수 (**1점**)

$P_{\text{valid}} = P_{fb} + \min(P_d + P_t, 3 \times \max(P_{fb}, 1)) \quad$ 점수 인정 가능 PR 개수  
$I_{\text{valid}} = \min(I_{fb} + I_d, 4 \times P_{\text{valid}}) \quad$ 점수 인정 가능 이슈 개수

PR의 점수를 최대로 하기 위해 기능/버그 PR을 먼저 계산한 후 문서 PR과 오타 PR을 계산합니다.  
($P_{fb}$이 0일 경우에도 문서 PR과 오타 PR 합산으로 최대 3개까지 인정됩니다.)

$P_{fb}^* = \min(P_{fb}, P_{\text{valid}}) \quad$ 기능/버그 PR 최대 포함  

$P_d^* = \min(P_d, P_{\text{valid}} - P_{fb}^*)$  문서 PR 포함

$P_t^* = P_{\text{valid}} - P_{fb}^* - P_d^*$  남은 개수에서 오타 PR 포함

이슈의 점수를 최대로 하기 위해 기능/버그 이슈를 먼저 계산한 후 문서 이슈를 계산합니다.

$I_{fb}^* = \min(I_{fb}, I_{\text{valid}}) \quad$ 기능/버그 이슈 최대 포함  
$I_d^* = I_{\text{valid}} - I_{fb}^* \quad$ 남은 개수에서 문서 이슈 포함

최종 점수 계산 공식:  
$S = 3P_{fb}^* + 2P_d^* + 1P_t^* + 2I_{fb}^* + 1I_d^*$

##  C# Dev Kit 설치 및 활용
Visual Studio Code용 C# Dev Kit 확장 프로그램은 C# 개발을 보다 편리하게 도와주는 도구입니다.  
테스트 실행, 디버깅, IntelliSense 자동완성 기능을 지원합니다.
이 확장은 .NET 기반 C# 콘솔 애플리케이션, Blazor, MAUI 등 다양한 .NET 프로젝트;
xUnit, NUnit, MSTest 등 테스트 프레임워크; Windows, macOS, Linux 플랫폼을 지원합니다.

### 설치 방법
    1. Visual Studio Code 실행
    2. 좌측 사이드바에서 Extensions(확장 프로그램) 탭 클릭
    3. 검색창에 "C# Dev Kit" 입력
    4. 설치(Install) 버튼 클릭

또는 터미널에서 명령어로 설치할 수도 있습니다:
`bash code --install-extension ms-dotnettools.csdevkit`

참고로, 현재는 devcontainer 설정으로 새로 코드스페이스를 만들 때 자동으로 설치가 되도록 설정이 되어 있습니다.

### 활용 방법
명령 팔레트(Command Palette) (Ctrl+Shift+P)에서 다음 명령어 사용:
- C# Dev Kit: Run Test
- C# Dev Kit: Debug Test
- IntelliSense 자동완성 기능 활성화
.csproj 파일 기반 프로젝트를 자동으로 인식 및 빌드

---
## 📚 가이드 문서 모음

### [.NET 가이드](docs/dotNet-guide.md)
- .NET 콘솔 애플리케이션 생성, 빌드, 실행, 테스트 과정 안내.

### [프로젝트 기여 및 작업 규칙](docs/project_guidelines.md)
- 프로젝트 규칙과 참여 방법.

### [Github Token 생성 방법](docs/github-token-guide.md)
- GitHub 토큰 생성 및 설정.

### [Octokit 가이드](docs/octokit_guide.md)
- GitHub API를 호출 또는 저장소 및 이슈를 관리하는 과정 안내.

### [포크 동기화 가이드](docs/fork_sync_guide.md)
- 포크 저장소 동기화 가이드.

### [소스 파일 추가 가이드](docs/add_cs_files_guide.md)
- 프로그램의 시작점을 포함하는 `Program.cs`외에 다른 확장자 `.cs`인 C# 소스코드 파일을 추가하는 방법 안내.

