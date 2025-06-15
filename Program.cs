using Cocona;
using System.Text.Json;          // JSON 파싱
using System.IO;                 // File, Path
using System.Linq;
using System.Collections.Generic;

CoconaApp.Run((
    [Argument(Description = "분석할 저장소. \"owner/repo\" 형식으로 공백을 구분자로 하여 여러 개 입력")] string[] repos,
    [Option('v', Description = "자세한 로그 출력을 활성화합니다.")] bool verbose,
    [Option('o', Description = "출력 디렉토리 경로를 지정합니다. (default : \"result\")", ValueName = "Output directory")] string? output,
    [Option('f', Description = "출력 형식 지정 (\"text\", \"csv\", \"chart\", \"html\", \"all\", default : \"all\")", ValueName = "Output format")] string[]? format,
    [Option('t', Description = "GitHub 액세스 토큰 입력", ValueName = "Github token")] string? token,
    [Option("include-user", Description = "결과에 포함할 사용자 ID 목록", ValueName = "Include user's id")] string[]? includeUsers,
    [Option("user", Description = "특정 사용자 한 명의 점수와 순위만 출력합니다.", ValueName = "Username")] string? singleUser,
    [Option("since", Description = "이 날짜 이후의 PR 및 이슈만 분석 (YYYY-MM-DD)", ValueName = "Start date")] string? since,
    [Option("until", Description = "이 날짜까지의 PR 및 이슈만 분석 (YYYY-MM-DD)", ValueName = "End date")] string? until,
    [Option("user-info", Description = "ID→이름 매핑 JSON/CSV 파일 경로")] string? userInfoPath,
    [Option("progress", Description = "API 호출 진행률을 표시합니다.")] bool progress,
    [Option("use-cache", Description = "캐시된 데이터를 사용합니다.")] bool useCache = false,
    [Option("show-state-summary", Description = "PR/Issue 상태 요약을 표시합니다.")] bool showStateSummary = false
) =>
{
    // 캐시 디렉토리 생성
    const string CACHE_DIR = "cache";
    if (!Directory.Exists(CACHE_DIR))
    {
        Directory.CreateDirectory(CACHE_DIR);
    }

    // ───────────────────────────────────────────────────────
    // A) user-info 옵션으로 전달된 JSON/CSV 파일을 파싱해서 idToNameMap에 저장
    // ───────────────────────────────────────────────────────
    Dictionary<string,string>? idToNameMap = null;
    if (!string.IsNullOrWhiteSpace(userInfoPath))
    {
        var ext = Path.GetExtension(userInfoPath).ToLowerInvariant();
        try
        {
            if (ext == ".json")
            {
                var json = File.ReadAllText(userInfoPath);
                idToNameMap = JsonSerializer.Deserialize<Dictionary<string,string>>(json);
            }
            else if (ext == ".csv")
            {
                idToNameMap = File.ReadAllLines(userInfoPath)
                    .Skip(1) // 헤더(Id,Name) 스킵
                    .Select(line => line.Split(','))
                    .Where(parts => parts.Length == 2)
                    .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                PrintHelper.PrintError("올바르지 못한 포멧입니다.");
                return;
            }
            if (idToNameMap == null || idToNameMap.Count == 0)
                throw new Exception();
        }
        catch
        {
            PrintHelper.PrintError("올바르지 못한 포멧입니다.");
            return;
        }
    }

    // ───────────────────────────────────────────────────────
    // 1) output 옵션 누락 시 기본값 안내
    // ───────────────────────────────────────────────────────
    if (string.IsNullOrWhiteSpace(output))
    {
        // 실제 디폴트 값은 코드에서 "output"으로 설정되어 있음
        PrintHelper.PrintWarning("출력 디렉토리가 지정되지 않아 기본 경로 'output/'이 사용됩니다.");
    }

    // ───────────────────────────────────────────────────────
    // 2) format 옵션 누락 시 기본값 안내
    // ───────────────────────────────────────────────────────
    if (format == null || format.Length == 0)
    {
        // 여기서 기본값 배열은 {"text", "csv", "chart", "html"}으로 설정됨
        PrintHelper.PrintWarning("출력 형식이 지정되지 않아 기본값 'all'이 사용됩니다.");
    }

    var failedRepos = new List<string>();

    RepoDataCollector.CreateClient(token);

    var totalScores = new Dictionary<string, UserScore>(); // 🆕 total score 집계용
    int totalRepos = repos.Length;
    int repoIndex = 0;

    foreach (var repoPath in repos)
    {
        repoIndex++;
        var parsed = TryParseRepoPath(repoPath);
        if (parsed == null) { failedRepos.Add(repoPath); continue; }
        var (owner, repo) = parsed.Value;
        var collector = new RepoDataCollector(owner, repo);

        if (progress)
        {
            Console.Write($"\r▶ 처리 중 ({repoIndex}/{totalRepos}): {owner}/{repo}...\n");
            Console.Out.Flush();
        }

        Dictionary<string, UserActivity> userActivities;
        try
        {
            if (progress)
            {
                Console.Write($"\r▶ 전체({repoIndex}/{totalRepos}) PR 및 Issue 불러오는 중...");
                Console.Out.Flush();
            }
            userActivities = collector.Collect(since: since, until: until, useCache: useCache);
            if (progress)
            {
                PrintHelper.PrintSuccess(" OK");
            }
        }
        catch (Exception ex)
        {
            if (progress)
            {
                Console.WriteLine(" 실패");
            }
            PrintHelper.PrintError($"! 오류 발생: {ex.Message}");
            continue;
        }

        if (!progress)
            Console.WriteLine($"\n🔍 처리 중: {owner}/{repo}\n");

        try
        {
            var analyzer = new ScoreAnalyzer(userActivities, idToNameMap);
            var scores = analyzer.Analyze();
            totalScores = analyzer.TotalAnalyze(scores);

            if (string.IsNullOrEmpty(singleUser))
            {
                List<string> formats = (format == null || format.Length == 0)
                    ? new List<string> { "text", "csv", "chart", "html" }
                    : checkFormat(format);

                string outputDir = string.IsNullOrWhiteSpace(output) ? "output" : output;
                var generator = new FileGenerator(scores, repo, outputDir);

                if (formats.Contains("csv")) generator.GenerateCsv();
                if (formats.Contains("text")) generator.GenerateTable();
                if (formats.Contains("chart")) generator.GenerateChart();
                if (formats.Contains("html")) generator.GenerateHtml();
                if (showStateSummary) generator.GenerateStateSummary(collector.StateSummary);
            }
        }
        catch (Exception ex)
        {
            PrintHelper.PrintError($"! 오류 발생: {ex.Message}");
        }

        if (progress)
            PrintHelper.PrintInfo($"▶ 처리 중 ({repoIndex}/{totalRepos}): {owner}/{repo} 완료");
    }

    if (string.IsNullOrEmpty(singleUser) && totalScores.Count > 0)
    {
        string outputDir = string.IsNullOrWhiteSpace(output) ? "output" : output;
        var totalGen = new FileGenerator(totalScores, "total", outputDir);
        totalGen.GenerateChart();
    }
    // --user 옵션이 지정된 경우, 해당 사용자의 점수와 순위만 출력
    else if (!string.IsNullOrEmpty(singleUser) && totalScores.Count > 0)
    {
        var sortedScores = totalScores.OrderByDescending(x => x.Value.total).ToList();
        int rank = 1;
        int prevScore = -1;
        int actualRank = 1;

        UserScore? targetUserScore = null;
        int targetUserRank = 0;

        foreach (var entry in sortedScores)
        {
            if (entry.Value.total != prevScore)
            {
                rank = actualRank;
            }

            if (string.Equals(entry.Key, singleUser, StringComparison.OrdinalIgnoreCase))
            {
                targetUserScore = entry.Value;
                targetUserRank = rank;
                break;
            }

            prevScore = entry.Value.total;
            actualRank++;
        }

        if (targetUserScore != null)
        {
            Console.WriteLine($"{singleUser} 사용자의 총점: {targetUserScore.total}점, 순위: {targetUserRank}위");
        }
        else
        {
            Console.WriteLine($"'{singleUser}' 사용자를 찾을 수 없습니다.");
        }
    }


    if (failedRepos.Count > 0)
    {
        PrintHelper.PrintError("\n❌ 처리되지 않은 저장소 목록:");
        foreach (var r in failedRepos) Console.WriteLine($"- {r} (올바른 형식: owner/repo)");
    }

    if (progress)
    {
        PrintHelper.PrintSuccess("완료");
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
            PrintHelper.PrintError($"포맷 '{f}'에는 사용할 수 없는 문자가 포함되어 있습니다.");
            Environment.Exit(1);
        }
        if (FormatList.Contains(f)) validFormats.Add(f);
        else unValidFormats.Add(f);
    }

    if (unValidFormats.Count != 0)
    {
        PrintHelper.PrintError("유효하지 않은 포맷 존재: " + string.Join(", ", unValidFormats));
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
        PrintHelper.PrintError($"⚠️ 저장소 인자 '{repoPath}'는 'owner/repo' 형식이어야 합니다.");
        return null;
    }
    return (parts[0], parts[1]);
}
