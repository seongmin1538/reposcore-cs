using Cocona;
using System;
using System.Collections.Generic;
using Octokit;
using DotNetEnv;


CoconaApp.Run(static(
    
    [Argument] string[] repos,
    [Option('v', Description = "자세한 로그 출력을 활성화합니다.")] bool verbose,
    [Option('o', Description = "출력 디렉토리 경로를 지정합니다.")] string? output,
    [Option('t', Description = "GitHub Personal Access Token 입력")] string? token,
    [Option('f', Description = "출력 형식을 지정합니다. (text, csv, chart, html, all)")] string[]? format
) =>
{
    // 더미 데이타가 실제로 불러와 지는지 기본적으로 확인하기 위한 코드
    var repo1Activities = DummyData.repo1Activities;
    Console.WriteLine("repo1Activities:" + repo1Activities.Count);
    var repo2Activities = DummyData.repo2Activities;
    Console.WriteLine("repo2Activities:" + repo2Activities.Count);

    foreach (var repoPath in repos)
    {
        if (!repoPath.Contains('/'))
        {
            Console.WriteLine($"! 저장소 인자 '{repoPath}'는 'owner/repo' 형식이어야 합니다.");
            continue;
        }

        var parts = repoPath.Split('/');
        if (parts.Length != 2)
        {
            Console.WriteLine($"! 저장소 인자 '{repoPath}'는 'owner/repo' 형식이어야 합니다.");
            continue;
        }

        string owner = parts[0];
        string repo = parts[1];

        Console.WriteLine($"\n🔍 처리 중: {owner}/{repo}");

        try
        {
            var client = new GitHubClient(new ProductHeaderValue("CoconaApp"));

            if (!string.IsNullOrEmpty(token))
            {
                File.WriteAllText(".env", $"GITHUB_TOKEN={token}\n");
                Console.WriteLine(".env의 토큰을 갱신합니다.");
                client.Credentials = new Credentials(token);
            }
            else if (File.Exists(".env"))
            {
                Console.WriteLine(".env의 토큰으로 인증을 진행합니다.");
                Env.Load();
                token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
                client.Credentials = new Credentials(token);
            }

            var repository = client.Repository.Get(owner, repo).GetAwaiter().GetResult();

            Console.WriteLine($"[INFO] Repository Name: {repository.Name}");
            Console.WriteLine($"[INFO] Full Name: {repository.FullName}");
            Console.WriteLine($"[INFO] Description: {repository.Description}");
            Console.WriteLine($"[INFO] Stars: {repository.StargazersCount}");
            Console.WriteLine($"[INFO] Forks: {repository.ForksCount}");
            Console.WriteLine($"[INFO] Open Issues: {repository.OpenIssuesCount}");
            Console.WriteLine($"[INFO] Language: {repository.Language}");
            Console.WriteLine($"[INFO] URL: {repository.HtmlUrl}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"! 오류 발생: {e.Message}");
            Environment.Exit(1);
        }

        try
        {
            var formats = format == null ?
                new List<string> { "text", "csv", "chart", "html" } 
                : checkFormat(format);

            var outputDir = string.IsNullOrWhiteSpace(output) ? "output" : output;

            var dataCollector = new RepoDataCollector(token!); // ✅ null-forgiving 연산자 적용

            // ===== 파일 생성 기능 구현 후 제거 =====
            Console.WriteLine("\n===생성되는 포맷===");
            foreach (var fm in formats)
            {
                Console.WriteLine($"-{fm}");
            }
            Console.WriteLine("\n파일 생성 기능이 아직 구현되지 않았습니다.");
            // ===== 파일 생성 기능 구현 후 제거 =====
        }
        catch (Exception ex)
        {
            Console.WriteLine($"! 오류 발생: {ex.Message}");
            Environment.Exit(1);
        }

        Environment.Exit(0);
    }
});

static List<string> checkFormat(string[] format) 
{
    var FormatList = new List<string> {"text", "csv", "chart", "html", "all"}; // 유효한 format

    var validFormats = new List<string> { };
    var unValidFormats = new List<string> { };
    char[] invalidChars = Path.GetInvalidFileNameChars();

    foreach (var fm in format)
    {
        var f = fm.Trim().ToLowerInvariant(); // 대소문자 구분 없이 유효성 검사
        if (f.IndexOfAny(invalidChars) >= 0)
        {
            Console.WriteLine($"포맷 '{f}'에는 파일명으로 사용할 수 없는 문자가 포함되어 있습니다.");
            Console.WriteLine("포맷 이름에서 다음 문자를 사용하지 마세요: " +
                string.Join(" ", invalidChars.Select(c => $"'{c}'")));
            Environment.Exit(1);
        }

        if (FormatList.Contains(f))
            validFormats.Add(f);
        else
            unValidFormats.Add(f);
    }

    // 유효하지 않은 포맷이 존재
    if (unValidFormats.Count != 0)
    {
        Console.WriteLine("유효하지 않은 포맷이 존재합니다.");
        Console.Write("유효하지 않은 포맷: ");
        foreach (var unValidFormat in unValidFormats)
        {
            Console.Write($"{unValidFormat} ");
        }
        Console.Write("\n");
        Environment.Exit(1);
    }

    // 추출한 리스트에 "all"이 존재할 경우 모든 포맷 리스트 반환
    if (validFormats.Contains("all"))
    {
        return new List<string> { "text", "csv", "chart", "html" };
    }

    return validFormats;
}
