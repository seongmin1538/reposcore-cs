using Octokit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DotNetEnv;
using System.Text.Json;

// GitHub 저장소 데이터를 수집하는 클래스입니다.
// 저장소의 PR 및 이슈 데이터를 분석하고, 사용자별 활동 정보를 정리합니다.
/// <summary>
/// GitHub 저장소에서 이슈 및 PR 데이터를 수집하고 사용자별 활동 내역을 생성하는 클래스입니다.
/// </summary>
/// <remarks>
/// 이 클래스는 Octokit 라이브러리를 사용하여 GitHub API로부터 데이터를 가져오며,
/// 사용자 활동을 분석해 <see cref="UserActivity"/> 형태로 정리합니다.
/// </remarks>
/// <param name="owner">GitHub 저장소 소유자 (예: oss2025hnu)</param>
/// <param name="repo">GitHub 저장소 이름 (예: reposcore-cs)</param>
public class RepoDataCollector
{
    private static GitHubClient? _client; // GitHub API 요청에 사용할 클라이언트입니다.
    private readonly string _owner; // 분석 대상 저장소의 owner (예: oss2025hnu)
    private readonly string _repo; // 분석 대상 저장소의 이름 (예: reposcore-cs)

    //수정에 용이하도록 수집데이터종류 전역변수화
    private static readonly string[] FeatureLabels = { "bug", "enhancement" };
    private static readonly string[] DocsLabels = { "documentation" };
    private static readonly string TypoLabel = "typo";

    public RepoStateSummary StateSummary { get; private set; } =
        new RepoStateSummary(0, 0, 0, 0);

    // 생성자에는 저장소 하나의 정보를 넘김
    public RepoDataCollector(string owner, string repo)
    {
        _owner = owner;
        _repo = repo;
    }

    // GitHubClient 초기화 메소드
    public static void CreateClient(string? token = null)
    {
        _client = new GitHubClient(new ProductHeaderValue("reposcore-cs"));

        // 인증키 추가 (토큰이 있을경우)
        // 토큰이 직접 전달된 경우: .env 갱신 후 인증 설정
        if (!string.IsNullOrEmpty(token))
        {
            try
            {
                File.WriteAllText(".env", $"GITHUB_TOKEN={token}\n");
                Console.WriteLine(".env의 토큰을 갱신합니다.");
            }
            catch (IOException ioEx)
            {
                PrintHelper.PrintError($"❗ .env 파일 쓰기 중 IO 오류가 발생했습니다: {ioEx.Message}");
            }
            catch (UnauthorizedAccessException uaEx)
            {
                PrintHelper.PrintError($"❗ .env 파일 쓰기 권한이 없습니다: {uaEx.Message}");
            }
            catch (Exception ex)
            {
                PrintHelper.PrintError($"❗ .env 파일 쓰기 중 알 수 없는 오류가 발생했습니다: {ex.Message}");
            }

            _client.Credentials = new Credentials(token);
        }
        else if (File.Exists(".env"))
        {
            try
            {
                Console.WriteLine(".env의 토큰으로 인증을 진행합니다.");

                Env.Load(); // .env 로드

                token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

                if (string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("❗ .env 파일에는 GITHUB_TOKEN이 포함되어 있지 않습니다.");
                }
                else
                {
                    _client.Credentials = new Credentials(token);
                    Console.WriteLine("✅ GITHUB_TOKEN을 이용해 인증 정보를 설정했습니다.");
                }
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("❗ .env 파일이 존재하지 않습니다. 가이드에 따라 .env 파일을 생성해 주세요.");
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine("❗ .env 파일에 접근할 권한이 없습니다.");
            }
            catch (IOException ioEx)
            {
                Console.WriteLine($"❗ .env 파일 읽기 중 입출력 오류가 발생했습니다: {ioEx.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❗ .env 로딩 중 예기치 못한 오류가 발생했습니다: {ex.Message}");
            }
        }
        else
        {
            PrintHelper.PrintError("❗ 인증 토큰이 제공되지 않았고 .env 파일도 존재하지 않습니다. 인증이 실패할 수 있습니다.");
        }
    }

    public static void ValidateRepositoryExists(string owner, string repo)
    {
        try
        {
            var repository = _client!.Repository.Get(owner, repo).Result;
        }
        catch (AggregateException exception)
        {
            foreach (var innerException in exception.InnerExceptions)
            {
                if (innerException is not NotFoundException) continue;
                
                PrintHelper.PrintError($"❗ 저장소 ‘{owner}/{repo}’를 찾을 수 없습니다.");
                Environment.Exit(1);
            }
        }
    }

    
    // 캐시 파일 경로를 반환하는 메서드
    private string GetCacheFilePath()
    {
        return Path.Combine("cache", $"{_owner}_{_repo}.json");
    }

    // 캐시에서 데이터를 로드하는 메서드
    private Dictionary<string, UserActivity>? LoadFromCache()
    {
        var cachePath = GetCacheFilePath();
        if (!File.Exists(cachePath))
            return null;

        try
        {
            var json = File.ReadAllText(cachePath);
            return JsonSerializer.Deserialize<Dictionary<string, UserActivity>>(json);
        }
        catch (Exception ex)
        {
            PrintHelper.PrintError($"❗ 캐시 파일 로드 중 오류 발생: {ex.Message}");
            return null;
        }
    }

    // 데이터를 캐시에 저장하는 메서드
    private void SaveToCache(Dictionary<string, UserActivity> data)
    {
        var cachePath = GetCacheFilePath();
        try
        {
            var json = JsonSerializer.Serialize(data);
            File.WriteAllText(cachePath, json);
        }
        catch (Exception ex)
        {
            PrintHelper.PrintError($"❗ 캐시 파일 저장 중 오류 발생: {ex.Message}");
        }
    }

    /// <summary>
    /// 지정된 저장소의 이슈 및 PR 데이터를 수집하여 사용자별 활동 내역을 반환합니다.
    /// </summary>
    /// <param name="returnDummyData">더미 데이터를 사용할지 여부 (테스트 용도)</param>
    /// <param name="since">이 날짜 이후의 PR 및 이슈만 분석 (YYYY-MM-DD 형식)</param>
    /// <param name="until">이 날짜까지의 PR 및 이슈만 분석 (YYYY-MM-DD 형식)</param>
    /// <param name="useCache">캐시를 사용할지 여부</param>
    /// <returns>
    /// 사용자 로그인명을 키로 하고 활동 내역(UserActivity)을 값으로 갖는 Dictionary
    /// </returns>
    /// <exception cref="RateLimitExceededException">API 호출 한도 초과 시</exception>
    /// <exception cref="AuthorizationException">인증 실패 시</exception>
    /// <exception cref="NotFoundException">저장소를 찾을 수 없을 경우</exception>
    /// <exception cref="Exception">기타 알 수 없는 예외 발생 시</exception>
    public Dictionary<string, UserActivity> Collect(bool returnDummyData = false, string? since = null, string? until = null, bool useCache = false)
    {
        if (returnDummyData)
        {
            return DummyData.repo1Activities;
        }

        if (useCache)
        {
            var cachedData = LoadFromCache();
            if (cachedData != null)
            {
                Console.WriteLine($"✅ 캐시에서 데이터를 로드했습니다: {_owner}/{_repo}");
                return cachedData;
            }
        }

        // --- Retry + Backoff 로직 추가 ---
        int maxRetries = 3;
        int[] backoffSeconds = { 1, 2, 4 };

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var request = new RepositoryIssueRequest { State = ItemStateFilter.All };

                if (!string.IsNullOrEmpty(since))
                {
                    if (DateTime.TryParse(since, out DateTime sinceDate))
                        request.Since = sinceDate;
                    else
                        throw new ArgumentException($"잘못된 시작 날짜 형식입니다: {since}. YYYY-MM-DD 형식으로 입력해주세요.");
                }

                var allIssuesAndPRs = _client!.Issue.GetAllForRepository(_owner, _repo, request).Result;

                if (!string.IsNullOrEmpty(until))
                {
                    if (!DateTime.TryParse(until, out DateTime untilDate))
                        throw new ArgumentException($"잘못된 종료 날짜 형식입니다: {until}. YYYY-MM-DD 형식으로 입력해주세요.");
                    allIssuesAndPRs = allIssuesAndPRs.Where(issue => issue.CreatedAt <= untilDate).ToList();
                }

                var rejectionLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "wontfix", "invalid", "duplicate" };
                var mutableActivities = new Dictionary<string, UserActivity>();
                int mergedPr = 0, unmergedPr = 0, openIssue = 0, closedIssue = 0;

                foreach (var item in allIssuesAndPRs)
                {
                    if (item.User?.Login == null) continue;
                    if (item.Labels.Any(l => rejectionLabels.Contains(l.Name))) continue;
                    var username = item.User.Login;

                    if (!mutableActivities.ContainsKey(username))
                        mutableActivities[username] = new UserActivity(0,0,0,0,0);

                    var labelName = item.Labels.Any() ? item.Labels[0].Name : null;
                    var activity = mutableActivities[username];

                    if (item.PullRequest != null)
                    {
                        if (item.PullRequest.Merged)
                        {
                            mergedPr++;
                            if (FeatureLabels.Contains(labelName)) activity.PR_fb++;
                            else if (DocsLabels.Contains(labelName)) activity.PR_doc++;
                            else if (labelName == TypoLabel) activity.PR_typo++;
                        }
                        else unmergedPr++;
                    }
                    else
                    {
                        if (item.State.Value.ToString() == "Open")
                        {
                            openIssue++;
                            if (FeatureLabels.Contains(labelName)) activity.IS_fb++;
                            else if (DocsLabels.Contains(labelName)) activity.IS_doc++;
                        }
                        else if (item.State.Value.ToString() == "Closed")
                        {
                            closedIssue++;
                            if (item.StateReason.ToString() == "completed")
                            {
                                if (FeatureLabels.Contains(labelName)) activity.IS_fb++;
                                else if (DocsLabels.Contains(labelName)) activity.IS_doc++;
                            }
                        }
                    }
                }

                var userActivities = new Dictionary<string, UserActivity>();
                foreach (var (key, value) in mutableActivities)
                {
                    userActivities[key] = new UserActivity(value.PR_fb, value.PR_doc, value.PR_typo, value.IS_fb, value.IS_doc);
                }

                StateSummary = new RepoStateSummary(mergedPr, unmergedPr, openIssue, closedIssue);
                SaveToCache(userActivities);
                return userActivities;
            }
            catch (Exception ex)
            {
                PrintHelper.PrintWarning($"⚠️ API 요청 실패, 재시도 시도 ({attempt + 1}/{maxRetries}) - {ex.Message}");

                if (attempt == maxRetries - 1)
                    throw;

                System.Threading.Thread.Sleep(backoffSeconds[attempt] * 1000);
            }
        }

        // 정상 실행 도달 불가 (논리상)
        throw new Exception("재시도 실패");
    }
}
