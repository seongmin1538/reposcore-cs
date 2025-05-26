global using Octokit;
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;

// 현재 이름만 바꾼 거고 싹 다 재설계해야 함
namespace reposcore_cs.Services.GitHub;
    public class RepoDataCollector(string token) // 1단계: 저장소에서 필요한 데이터를 가져오는 역할
    {
        private readonly GitHubClient _client = CreateClient("reposcore-cs", token);

        private static GitHubClient CreateClient(string productName, string token)
        {
            var client = new GitHubClient(new ProductHeaderValue(productName));

            if (!string.IsNullOrEmpty(token))
            {
                client.Credentials = new Credentials(token);
            }

            return client;
        }

        private static void HandleError(Exception ex)
        {
            Console.WriteLine($"❗ 알 수 없는 오류가 발생했습니다: {ex.Message}");
            Environment.Exit(1);
        }

        [Obsolete]
        public Dictionary<string, int> Collect(string owner, string repo, string outputDir, List<string> formats)
        {
            try
            {
                Console.WriteLine("📥 Pull Requests 로딩 중...");
                var prs = _client.PullRequest.GetAllForRepository(owner, repo, new PullRequestRequest
                {
                    State = ItemStateFilter.Closed
                }).Result;

                Console.WriteLine("📥 Issues 로딩 중...");
                var issues = _client.Issue.GetAllForRepository(owner, repo, new RepositoryIssueRequest
                {
                    State = ItemStateFilter.All
                }).Result;

                Console.WriteLine("🔍 라벨 통계 분석 중...");
                var targetLabels = new[] { "bug", "documentation", "enhancement" };
                var labelCounts = targetLabels.ToDictionary(label => label, _ => 0);

                foreach (var pr in prs.Where(p => p.Merged == true))
                {
                    var labels = pr.Labels.Select(l => l.Name.ToLower()).ToList();
                    foreach (var label in targetLabels)
                    {
                        if (labels.Contains(label))
                            labelCounts[label]++;
                    }
                }

                foreach (var issue in issues)
                {
                    if (issue.PullRequest != null) continue;
                    var labels = issue.Labels.Select(l => l.Name.ToLower()).ToList();
                    foreach (var label in targetLabels)
                    {
                        if (labels.Contains(label))
                            labelCounts[label]++;
                    }
                }

                Console.WriteLine("\n📊 GitHub Label 통계 결과");

                Console.WriteLine("\n✅ Pull Requests (Merged)");
                foreach (var label in targetLabels)
                {
                    Console.WriteLine($"- {char.ToUpper(label[0]) + label[1..]} PRs: {labelCounts[label]}");
                }

                Console.WriteLine("\n✅ Issues");
                foreach (var label in targetLabels)
                {
                    Console.WriteLine($"- {char.ToUpper(label[0]) + label[1..]} Issues: {labelCounts[label]}");
                }

                return labelCounts;
            }
            catch (RateLimitExceededException)
            {
                try
                {
                    var client = new GitHubClient(new ProductHeaderValue("reposcore-cs"));
                    var rateLimits = _client.Miscellaneous.GetRateLimits().Result;
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
                HandleError(ex);
            }
            return [];
        }

        // 결과물 만들어내는 이거는 3단계에서 할 일이니까 이것도 다른 곳으로 옮겨야 함
        private void GenerateOutputFiles(string outputDir, List<string> formats)
        {
            try
            {
                Directory.CreateDirectory(outputDir);

                foreach (var format in formats)
                {
                    string fileName = $"result.{format.ToLower()}";
                    string filePath = Path.Combine(outputDir, fileName);

                    File.WriteAllText(filePath, string.Empty);
                    Console.WriteLine($"📁 생성된 파일: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❗ 출력 파일 생성 중 오류: {ex.Message}");
            }
        }
    }