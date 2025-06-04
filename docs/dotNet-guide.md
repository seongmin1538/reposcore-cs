# .NET Guide

이 가이드는 .NET 콘솔 애플리케이션을 생성, 빌드, 실행, 테스트하는 과정을 안내합니다.

---
# Useage

## 프로젝트 생성

```bash
dotnet new console -n {프로젝트_이름}
```

해당 명령어를 실행하면 다음과 같은 폴더 및 파일 구조가 생성됩니다:

```
{프로젝트_이름}/
├── {프로젝트_이름}.csproj
└── Program.cs
```

---

## 프로젝트 빌드

프로젝트의 C# 코드를 컴파일하여 실행 가능한 `.dll` 파일을 생성합니다.
컴파일 오류를 사전에 확인하거나, 실행 파일을 따로 만들고자 할 때 사용됩니다.

```bash
dotnet build
```

예시 출력:
```bash
Build succeeded.
    0 Warning(s)
    0 Error(s)
```
빌드 결과물은 `bin/` 폴더에 생성됩니다.

---

## 프로젝트 실행

프로젝트를 자동으로 빌드한 뒤 `Program.cs`의 `Main` 메서드를 실행합니다.
실행 방법과 예시 명령어들은 README.md에 있습니다.
만약 입력 인자가 필요한 프로그램이라면 `--` 뒤에 인자를 넣어줘야 합니다.

예시 출력:
```bash
📊 GitHub Label 통계 결과

✅ Pull Requests (Merged)
- Bug PRs: 8
- Documentation PRs: 62
- Enhancement PRs: 57

✅ Issues
- Bug Issues: 8
- Documentation Issues: 62
- Enhancement Issues: 57

`Program.cs`의 `Main` 메서드가 실행됩니다.
```

---

## 테스트

`xUnit`, `NUnit`, `MSTest` 등으로 작성된 테스트 코드를 실행합니다.
현재 이 저장소는 테스트 프로젝트가 없어 실행 가능한 테스트가 없습니다.

```bash
dotnet test
```

예시 출력: (현재 테스트 프로젝트 없음)
```bash
Determining projects to restore...
All projects are up-to-date for restore.
```

예시 출력: (테스트 프로젝트 1개 존재 시)
```bash
Test run for /reposcore-cs.Tests/bin/Debug/net8.0/reposcore-cs.Tests.dll (.NETCoreApp,Version=v8.0)
Microsoft (R) Test Execution Command Line Tool Version 17.8.0

Starting test execution, please wait...
Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: 23 ms
```

> 테스트 프로젝트(`xUnit`, `NUnit` 등)가 존재할 경우 실행됩니다.  
> 예시: `dotnet new xunit -n MyApp.Tests`

---

## 참고 자료

- [.NET CLI 공식 문서 (GitHub)](https://github.com/dotnet/docs/tree/main/docs/core/tools)  
- [Microsoft Learn: .NET CLI 개요](https://learn.microsoft.com/dotnet/core/tools/)
