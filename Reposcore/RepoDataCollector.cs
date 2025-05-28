using Octokit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DotNetEnv;

public class RepoDataCollector
{
    private static GitHubClient? _client;
    private readonly string _owner;
    private readonly string _repo;

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
        if (!string.IsNullOrEmpty(token))
        {
            File.WriteAllText(".env", $"GITHUB_TOKEN={token}\n");
            Console.WriteLine(".env의 토큰을 갱신합니다.");
            _client.Credentials = new Credentials(token);
        }
        else if (File.Exists(".env"))
        {
            Console.WriteLine(".env의 토큰으로 인증을 진행합니다.");
            Env.Load();
            token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            _client.Credentials = new Credentials(token);
        }
    }

    // 수집용 mutable 클래스 (Collect 메소드에서만 사용)
    private class MutableUserActivity
    {
        public int PR_fb = 0;
        public int PR_doc = 0;
        public int PR_typo = 0;
        public int IS_fb = 0;
        public int IS_doc = 0;
    }

    // Collect 메소드
    public Dictionary<string, UserActivity> Collect(bool returnDummyData = false)
    {
        if (returnDummyData)
        {
            return DummyData.repo1Activities;
        }

        try
        {
            // Issues수집 (RP포함)
            var allIssuesAndPRs = _client!.Issue.GetAllForRepository(_owner, _repo, new RepositoryIssueRequest
            {
                State = ItemStateFilter.All
            }).Result;

            // 수집용 mutable 객체. 모든 데이터 수집 후 레코드로 변환하여 반환
            var mutableActivities = new Dictionary<string, MutableUserActivity>();

            // allIssuesAndPRs의 데이터를 유저,라벨별로 분류
            foreach (var item in allIssuesAndPRs)
            {
                if (item.User?.Login == null) continue;

                var username = item.User.Login;

                // 처음 기록하는 사용자 초기화
                if (!mutableActivities.ContainsKey(username))
                {
                    mutableActivities[username] = new MutableUserActivity();
                }

                var labelName = item.Labels.Any() ? item.Labels[0].Name : null; // 라벨 구분을 위한 labelName

                var activity = mutableActivities[username];

                if (item.PullRequest != null) // PR일 경우
                {
                    if (item.PullRequest.Merged) // 병합된 PR만 집계
                    {
                        if (labelName == "bug" || labelName == "enhancement")
                            activity.PR_fb++;
                        else if (labelName == "documentation")
                            activity.PR_doc++;
                        else if (labelName == "typo")
                            activity.PR_typo++;
                    }
                }
                else
                {
                    if (item.State.Value.ToString() == "Open" ||
                        item.StateReason.ToString() == "completed") // 열려있거나 정상적으로 닫힌 이슈들만 집계
                    {
                        if (labelName == "bug" || labelName == "enhancement")
                            activity.IS_fb++;
                        else if (labelName == "documentation")
                            activity.IS_doc++;

                    }
                }
            }

            // 레코드로 변환
            var userActivities = new Dictionary<string, UserActivity>();
            foreach (var (key, value) in mutableActivities)
            {
                userActivities[key] = new UserActivity(
                    PR_fb: value.PR_fb,
                    PR_doc: value.PR_doc,
                    PR_typo: value.PR_typo,
                    IS_fb: value.IS_fb,
                    IS_doc: value.IS_doc
                );
            }

            return userActivities;
        }
        catch (RateLimitExceededException)
        {
            try
            {
                var rateLimits = _client!.RateLimit.GetRateLimits().Result;
                var coreRateLimit = rateLimits.Rate;
                var resetTime = coreRateLimit.Reset; // UTC DateTime
                var secondsUntilReset = (int)(resetTime - DateTimeOffset.UtcNow).TotalSeconds;

                Console.WriteLine($"❗ API 호출 한도(Rate Limit)를 초과했습니다. {secondsUntilReset}초 후 재시도 가능합니다 (약 {resetTime.LocalDateTime} 기준).");
            }
            catch (Exception innerEx)
            {
                Console.WriteLine($"❗ API 호출 한도 초과, 재시도 시간을 가져오는 데 실패했습니다: {innerEx.Message}");
            }

            Environment.Exit(1);
        }
        catch (AuthorizationException)
        {
            Console.WriteLine("❗ 인증 실패: 올바른 토큰을 사용했는지 확인하세요.");
            Environment.Exit(1);
        }
        catch (NotFoundException)
        {
            Console.WriteLine("❗ 저장소를 찾을 수 없습니다. owner/repo 이름을 확인하세요.");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❗ 알 수 없는 오류가 발생했습니다: {ex.Message}");
            Environment.Exit(1);
        }
        return null!;
    }
}