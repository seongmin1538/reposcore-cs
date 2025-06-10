using Cocona;
using System.Text.Json;
using System.IO;
using System.Linq;
using System.Collections.Generic;

// ───────────────────────────────────────────────────────
// ① 캐시 시뮬레이션 상수 (현재는 항상 Disabled)
// ───────────────────────────────────────────────────────
const bool CACHE_ENABLED = false;

CoconaApp.Run((
    [Argument(Description = "분석할 저장소. \"owner/repo\" 형식으로 공백을 구분자로 하여 여러 개 입력")] string[] repos,
    [Option('v', Description = "자세한 로그 출력을 활성화합니다.")] bool verbose,
    [Option('o', Description = "출력 디렉토리 경로를 지정합니다. (default : \"result\")", ValueName = "Output directory")] string? output,
    [Option('f', Description = "출력 형식 지정 (\"text\", \"csv\", \"chart\", \"html\", \"all\", default : \"all\")", ValueName = "Output format")] string[]? format,
    [Option('t', Description = "GitHub 액세스 토큰 입력", ValueName = "Github token")] string? token,
    [Option("include-user", Description = "결과에 포함할 사용자 ID 목록", ValueName = "Include user's id")] string[]? includeUsers,
    [Option("dry-run", Description = "실제 작업 없이 시뮬레이션 로그만 출력")] bool dryRun
) =>
{
    if (dryRun)
    {
        Console.WriteLine("===== Dry-Run 시뮬레이션 =====");
        foreach (var repoPath in repos) Console.WriteLine($"  - {repoPath}");
        Console.WriteLine("===== 시뮬레이션 종료 =====");
        return;
    }

    var summaries = new List<(string RepoName, Dictionary<string, int> LabelCounts)>();
    var failedRepos = new List<string>();

    RepoDataCollector.CreateClient(token);

    var totalScores = new Dictionary<string, UserScore>(); // 🆕 total score 집계용

    foreach (var repoPath in repos)
    {
        var parsed = TryParseRepoPath(repoPath);
        if (parsed == null) { failedRepos.Add(repoPath); continue; }
        var (owner, repo) = parsed.Value;
        var collector = new RepoDataCollector(owner, repo);
        var userActivities = collector.Collect();

        try
        {
            var labelCounts = new Dictionary<string, int> {
                { "bug", 0 }, { "documentation", 0 }, { "typo", 0 }
            };

            var userScores = userActivities.ToDictionary(
                pair => pair.Key,
                pair => ScoreAnalyzer.FromActivity(pair.Value)
            );

            // 🆕 total score에 누적
            foreach (var (user, score) in userScores)
            {
                if (!totalScores.ContainsKey(user))
                    totalScores[user] = score;
                else
                {
                    var prev = totalScores[user];
                    totalScores[user] = new UserScore(
                        prev.PR_fb + score.PR_fb,
                        prev.PR_doc + score.PR_doc,
                        prev.PR_typo + score.PR_typo,
                        prev.IS_fb + score.IS_fb,
                        prev.IS_doc + score.IS_doc,
                        prev.total + score.total
                    );
                }
            }

            // 포맷 처리
            var formats = (format == null || format.Length == 0)
                ? new List<string> { "text", "csv", "chart", "html" }
                : checkFormat(format);

            var outputDir = string.IsNullOrWhiteSpace(output) ? "output" : output;
            var generator = new FileGenerator(userScores, repo, outputDir);

            if (formats.Contains("csv")) generator.GenerateCsv();
            if (formats.Contains("text")) generator.GenerateTable();
            if (formats.Contains("chart")) generator.GenerateChart();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"! 오류 발생: {ex.Message}");
        }
    }

    // 🆕 totalChart 출력
    if (totalScores.Count > 0)
    {
        var outputDir = string.IsNullOrWhiteSpace(output) ? "output" : output;
        var totalGen = new FileGenerator(totalScores, "total", outputDir);
        totalGen.GenerateChart();
    }
});

static List<string> checkFormat(string[] format)
{
    var FormatList = new List<string> { "text", "csv", "chart", "html", "all" };
    var validFormats = new List<string> { };
    var unValidFormats = new List<string> { };
    char[] invalidChars = Path.GetInvalidFileNameChars();

    foreach (var fm in format)
    {
        var f = fm.Trim().ToLowerInvariant();
        if (f.IndexOfAny(invalidChars) >= 0)
        {
            Console.WriteLine($"포맷 '{f}'에는 사용할 수 없는 문자가 포함되어 있습니다.");
            Environment.Exit(1);
        }
        if (FormatList.Contains(f)) validFormats.Add(f);
        else unValidFormats.Add(f);
    }

    if (unValidFormats.Count != 0)
    {
        Console.WriteLine("유효하지 않은 포맷 존재: " + string.Join(", ", unValidFormats));
        Environment.Exit(1);
    }

    return validFormats.Contains("all")
        ? new List<string> { "text", "csv", "chart", "html" }
        : validFormats;
}

static (string, string)? TryParseRepoPath(string repoPath)
{
    var parts = repoPath.Split('/');
    if (parts.Length != 2)
    {
        Console.WriteLine($"⚠️ 저장소 인자 '{repoPath}'는 'owner/repo' 형식이어야 합니다.");
        return null;
    }
    return (parts[0], parts[1]);
}