using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Drawing;
using ConsoleTables;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.TickGenerators;
using Alignment = ScottPlot.Alignment;
using Color = System.Drawing.Color;

public class FileGenerator
{
    private readonly Dictionary<string, UserScore> _scores;
    private readonly string _repoName;
    private readonly string _folderPath;
    private static List<(string RepoName, Dictionary<string, UserScore> Scores)> _allRepos = new();

    public FileGenerator(Dictionary<string, UserScore> repoScores, string repoName, string folderPath)
    {
        _scores = repoScores;
        _repoName = repoName;
        _folderPath = Path.Combine(folderPath, repoName);

        // 모든 저장소 데이터 저장
        _allRepos.Add((repoName, repoScores));

        try
        {
            Directory.CreateDirectory(_folderPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❗ 결과 디렉토리 생성에 실패했습니다. (경로: {_folderPath})");
            Console.WriteLine($"→ 디스크 권한이나 경로 오류를 확인하세요: {ex.Message}");
            Environment.Exit(1);
        }
    }

    double sumOfPR
    {
        get
        {
            return _scores.Sum(pair => pair.Value.PR_doc + pair.Value.PR_fb + pair.Value.PR_typo);
        }        
    }

    double sumOfIs
    {
        get { return _scores.Sum(pair => pair.Value.IS_doc + pair.Value.IS_fb); }
    }

    public void GenerateCsv()
    {
        // 경로 설정
        string filePath = Path.Combine(_folderPath, $"{_repoName}.csv");
        using StreamWriter writer = new StreamWriter(filePath);

        
        // 파일에 "# 점수 계산 기준…" 을 쓰면, 이 줄이 CSV 첫 줄로 나옵니다.
        writer.WriteLine("# 점수 계산 기준: PR_fb*3, PR_doc*2, PR_typo*1, IS_fb*2, IS_doc*1");
        // CSV 헤더
        writer.WriteLine("User,f/b_PR,doc_PR,typo,f/b_issue,doc_issue,PR_rate,IS_rate,total");

        string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        var totals = _scores.Values.Select(s => s.total).ToList();
        double avg = totals.Count > 0 ? totals.Average() : 0.0;
        double max = totals.Count > 0 ? totals.Max() : 0.0;
        double min = totals.Count > 0 ? totals.Min() : 0.0;
        writer.WriteLine($"# Repo: {_repoName}  Date: {now}  Avg: {avg:F1}  Max: {max:F1}  Min: {min:F1}");

        // 내용 작성
        foreach (var (id, scores) in _scores.OrderByDescending(x => x.Value.total))
        {
            double prRate = (sumOfPR > 0) ? (scores.PR_doc + scores.PR_fb + scores.PR_typo) / sumOfPR * 100 : 0.0;
    double isRate = (sumOfIs > 0) ? (scores.IS_doc + scores.IS_fb) / sumOfIs * 100 : 0.0;
            string line =
                $"{id},{scores.PR_fb},{scores.PR_doc},{scores.PR_typo},{scores.IS_fb},{scores.IS_doc},{prRate:F1},{isRate:F1},{scores.total}";
            writer.WriteLine(line);
        }

        Console.WriteLine($"{filePath} 생성됨");
    }
    public void GenerateTable()
    {
        // 출력할 파일 경로
        string filePath = Path.Combine(_folderPath, $"{_repoName}1.txt");

        // 테이블 생성
        var headers = "UserId,f/b_PR,doc_PR,typo,f/b_issue,doc_issue,PR_rate,IS_rate,total".Split(',');

        // 각 칸의 너비 계산 (오른쪽 정렬을 위해 사용)
        int[] colWidths = headers.Select(h => h.Length).ToArray();

        var table = new ConsoleTable(headers);

        // 내용 작성
        foreach (var (id, scores) in _scores.OrderByDescending(x => x.Value.total))
        {
            double prRate = (sumOfPR > 0) ? (scores.PR_doc + scores.PR_fb + scores.PR_typo) / sumOfPR * 100 : 0.0;
            double isRate = (sumOfIs > 0) ? (scores.IS_doc + scores.IS_fb) / sumOfIs * 100 : 0.0;
            table.AddRow(
                id.PadRight(colWidths[0]), // 글자는 왼쪽 정렬                   
                scores.PR_fb.ToString().PadLeft(colWidths[1]), // 숫자는 오른쪽 정렬
                scores.PR_doc.ToString().PadLeft(colWidths[2]),
                scores.PR_typo.ToString().PadLeft(colWidths[3]),
                scores.IS_fb.ToString().PadLeft(colWidths[4]),
                scores.IS_doc.ToString().PadLeft(colWidths[5]),
                $"{prRate:F1}".PadLeft(colWidths[6]),
                $"{isRate:F1}".PadLeft(colWidths[7]),
                scores.total.ToString().PadLeft(colWidths[8])
            );
        }
        
        // 점수 기준 주석과 테이블 같이 출력
        var tableText = table.ToMinimalString();

        // 생성 정보 로그 계산
        string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        var totals = _scores.Values.Select(s => s.total).ToList();
        double avg = totals.Count > 0 ? totals.Average() : 0.0;
        double max = totals.Count > 0 ? totals.Max() : 0.0;
        double min = totals.Count > 0 ? totals.Min() : 0.0;
        string metaLine = $"# Repo: {_repoName}  Date: {now}  Avg: {avg:F1}  Max: {max:F1}  Min: {min:F1}";

        var content = "# 점수 계산 기준: PR_fb*3, PR_doc*2, PR_typo*1, IS_fb*2, IS_doc*1"
                    + Environment.NewLine
                    + metaLine
                    + Environment.NewLine
                    + tableText;

        File.WriteAllText(filePath, content);
        Console.WriteLine($"{filePath} 생성됨");
    }

    public void GenerateChart()
    {
        var labels = new List<string>();
        var values = new List<double>();

        // total 점수 내림차순 정렬
        var sorted = _scores.OrderByDescending(x => x.Value.total).ToList();
        var rankList = new List<(int Rank, string User, double Score)>();
        int rank = 1;
        int count = 1;
        double? prevScore = null;

        foreach (var pair in sorted)
        {
            if (prevScore != null && pair.Value.total != prevScore)
            {
                rank = count;
            }
            rankList.Add((rank, pair.Key, pair.Value.total));
            prevScore = pair.Value.total;
            count++;
        }

        // 차트는 오름차순으로 표시
        foreach (var item in rankList.OrderBy(x => x.Score))
        {
            string suffix = item.Rank switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            };
            labels.Add($"{item.User} ({item.Rank}{suffix})");
            values.Add(item.Score);
        }

        string[] names = labels.ToArray();
        double[] scores = values.ToArray();
        
        // ✅ 간격 조절된 Position
        double spacing = 10; // 막대 간격
        double[] positions = Enumerable.Range(0, names.Length)
                                    .Select(i => i * spacing)
                                    .ToArray();

        // Bar 데이터 생성
        var plt = new ScottPlot.Plot();
        var bars = new List<Bar>();
        for (int i = 0; i < scores.Length; i++)
        {
            bars.Add(new Bar
            {
                Position = positions[i],
                Value = scores[i],
                FillColor = Colors.SteelBlue,
                Orientation = Orientation.Horizontal,
                Size = 5,
            });

            double textX = scores[i] + scores.Max() * 0.01;
            double textY = positions[i];

            // 사용자 이름 추출 (labels와 rankList는 같은 순서)
            var userLabel = labels[i];
            // userLabel은 "user (1st)" 형태이므로, rankList에서 사용자 이름을 가져옴
            var userName = rankList.OrderBy(x => x.Score).ElementAt(i).User;
            if (_scores.TryGetValue(userName, out var userScore))
            {
                string detailText = $"{userScore.total} (PR_fb {userScore.PR_fb} / PR_doc {userScore.PR_doc} / PR_typo {userScore.PR_typo} / IS_fb {userScore.IS_fb} / IS_doc {userScore.IS_doc})";
                var txt = plt.Add.Text(detailText, textX, textY);
                txt.Alignment = Alignment.MiddleLeft;
            }
            else
            {
                // 혹시라도 매칭이 안 될 경우 기존 총점만 표시
                var txt = plt.Add.Text($"{scores[i]:F1}", textX, textY);
                txt.Alignment = Alignment.MiddleLeft;
            }
        }

        var barPlot = plt.Add.Bars(bars);

        string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        double avg = scores.Average();
        double max = scores.Max();
        double min = scores.Min();

        string chartTitle = $"Repo: {_repoName}  Date: {now}  Avg: {avg:F1}  Max: {max:F1}  Min: {min:F1}";
        plt.Axes.Left.TickGenerator = new NumericManual(positions, names);
        plt.Title($"Scores - {_repoName}" + "\n" + chartTitle);
        plt.XLabel("Total Score");
        plt.YLabel("User");

        // x축 범위 설정
        plt.Axes.Bottom.Min = 0;
        plt.Axes.Bottom.Max = scores.Max() * 2.5; 

        // 통계 정보 추가
        double maxScore = scores.Max();
        double minScore = scores.Min();
        double avgScore = scores.Average();

        // 제목 근처에 통계 정보 표시
        double xRight = scores.Max() * 2.4; // x축 최대값의 90% 위치
        double yTop = positions[^3] + spacing * 2; // 마지막 막대 위에 표시
        double ySpacing = spacing * 0.8; // 텍스트 간격

        var maxText = plt.Add.Text($"max: {maxScore:F1}", xRight, yTop);
        maxText.Alignment = Alignment.UpperRight;
        maxText.Color = Colors.DarkGreen;

        var avgText = plt.Add.Text($"avg: {avgScore:F1}", xRight, yTop - ySpacing);
        avgText.Alignment = Alignment.UpperRight;
        avgText.Color = Colors.DarkBlue;

        var minText = plt.Add.Text($"min: {minScore:F1}", xRight, yTop - ySpacing * 2);
        minText.Alignment = Alignment.UpperRight;
        minText.Color = Colors.DarkRed;

        string outputPath = Path.Combine(_folderPath, $"{_repoName}_chart.png");
        plt.SavePng(outputPath, 1080, 1920);
        Console.WriteLine($"✅ 차트 생성 완료: {outputPath}");
    }

    public void GenerateStateSummary(RepoStateSummary summary)
    {
        string filePath = Path.Combine(_folderPath, $"{_repoName}_state.txt");
        using StreamWriter writer = new StreamWriter(filePath);
        writer.WriteLine($"Merged PR: {summary.MergedPR}");
        writer.WriteLine($"Unmerged PR: {summary.UnmergedPR}");
        writer.WriteLine($"Open Issue: {summary.OpenIssue}");
        writer.WriteLine($"Closed Issue: {summary.ClosedIssue}");
        Console.WriteLine($"{filePath} 생성됨");
    }

    public void GenerateHtml()
    {
        string filePath = Path.Combine(Path.GetDirectoryName(_folderPath)!, "index.html");
        using StreamWriter writer = new StreamWriter(filePath);

        // HTML 헤더 및 스타일
        writer.WriteLine("<!DOCTYPE html>");
        writer.WriteLine("<html lang='ko'>");
        writer.WriteLine("<head>");
        writer.WriteLine("    <meta charset='UTF-8'>");
        writer.WriteLine("    <meta name='viewport' content='width=device-width, initial-scale=1.0'>");
        writer.WriteLine("    <title>Reposcore Analysis</title>");
        writer.WriteLine("    <style>");
        writer.WriteLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
        writer.WriteLine("        .score-info { background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin-bottom: 20px; }");
        writer.WriteLine("        table { border-collapse: collapse; width: 100%; margin-bottom: 20px; }");
        writer.WriteLine("        th, td { border: 1px solid #ddd; padding: 8px; text-align: right; }");
        writer.WriteLine("        th { background-color: #f2f2f2; text-align: center; }");
        writer.WriteLine("        tr:nth-child(even) { background-color: #f9f9f9; }");
        writer.WriteLine("        tr:hover { background-color: #f5f5f5; }");
        writer.WriteLine("        .total { font-weight: bold; }");
        writer.WriteLine("        .tab { overflow: hidden; border: 1px solid #ccc; background-color: #f1f1f1; }");
        writer.WriteLine("        .tab button { background-color: inherit; float: left; border: none; outline: none; cursor: pointer; padding: 14px 16px; transition: 0.3s; }");
        writer.WriteLine("        .tab button:hover { background-color: #ddd; }");
        writer.WriteLine("        .tab button.active { background-color: #ccc; }");
        writer.WriteLine("        .tabcontent { display: none; padding: 6px 12px; border: 1px solid #ccc; border-top: none; }");
        writer.WriteLine("    </style>");
        writer.WriteLine("</head>");
        writer.WriteLine("<body>");

        // 점수 계산 기준 정보
        writer.WriteLine("    <div class='score-info'>");
        writer.WriteLine("        <h2>점수 계산 기준</h2>");
        writer.WriteLine("        <ul>");
        writer.WriteLine("            <li>PR_fb: 3점</li>");
        writer.WriteLine("            <li>PR_doc: 2점</li>");
        writer.WriteLine("            <li>PR_typo: 1점</li>");
        writer.WriteLine("            <li>IS_fb: 2점</li>");
        writer.WriteLine("            <li>IS_doc: 1점</li>");
        writer.WriteLine("        </ul>");
        writer.WriteLine("    </div>");

        // 탭 버튼
        writer.WriteLine("    <div class='tab'>");
        foreach (var (repoName, _) in _allRepos)
        {
            writer.WriteLine($"        <button class='tablinks' onclick=\"openTab(event, '{repoName}')\">{repoName}</button>");
        }
        writer.WriteLine("        <button class='tablinks' onclick=\"openTab(event, 'total')\">Total</button>");
        writer.WriteLine("    </div>");

        // 각 저장소별 탭 내용
        foreach (var (repoName, scores) in _allRepos)
        {
            writer.WriteLine($"    <div id='{repoName}' class='tabcontent'>");
            writer.WriteLine("        <table>");
            writer.WriteLine("            <thead>");
            writer.WriteLine("                <tr>");
            writer.WriteLine("                    <th>순위</th>");
            writer.WriteLine("                    <th>User</th>");
            writer.WriteLine("                    <th>f/b_PR</th>");
            writer.WriteLine("                    <th>doc_PR</th>");
            writer.WriteLine("                    <th>typo</th>");
            writer.WriteLine("                    <th>f/b_issue</th>");
            writer.WriteLine("                    <th>doc_issue</th>");
            writer.WriteLine("                    <th>PR_rate</th>");
            writer.WriteLine("                    <th>IS_rate</th>");
            writer.WriteLine("                    <th>total</th>");
            writer.WriteLine("                </tr>");
            writer.WriteLine("            </thead>");
            writer.WriteLine("            <tbody>");

            double repoSumOfPR = scores.Sum(pair => pair.Value.PR_doc + pair.Value.PR_fb + pair.Value.PR_typo);
            double repoSumOfIs = scores.Sum(pair => pair.Value.IS_doc + pair.Value.IS_fb);

            int currentRank = 1; // 순위
            double previousTotal = -1; // 이전 점수
            int position = 0; // 현재 위치

            foreach (var (id, score) in scores.OrderByDescending(x => x.Value.total))
            {
                position++;

                // 이전 점수와 다르면 현재 순위 업데이트
                if (score.total != previousTotal)
                {
                    currentRank = position;
                }

                double prRate = (repoSumOfPR > 0) ? (score.PR_doc + score.PR_fb + score.PR_typo) / repoSumOfPR * 100 : 0.0;
                double isRate = (repoSumOfIs > 0) ? (score.IS_doc + score.IS_fb) / repoSumOfIs * 100 : 0.0;

                writer.WriteLine("                <tr>");
                writer.WriteLine($"                    <td class='rank'>{currentRank}</td>");
                writer.WriteLine($"                    <td>{id}</td>");
                writer.WriteLine($"                    <td>{score.PR_fb}</td>");
                writer.WriteLine($"                    <td>{score.PR_doc}</td>");
                writer.WriteLine($"                    <td>{score.PR_typo}</td>");
                writer.WriteLine($"                    <td>{score.IS_fb}</td>");
                writer.WriteLine($"                    <td>{score.IS_doc}</td>");
                writer.WriteLine($"                    <td>{prRate:F1}%</td>");
                writer.WriteLine($"                    <td>{isRate:F1}%</td>");
                writer.WriteLine($"                    <td class='total'>{score.total}</td>");
                writer.WriteLine("                </tr>");

                // 이전 점수 업데이트
                previousTotal = score.total;
            }

            writer.WriteLine("            </tbody>");
            writer.WriteLine("        </table>");
            writer.WriteLine("    </div>");
        }

        // Total 탭 내용
        var totalScores = new Dictionary<string, UserScore>();
        foreach (var (_, scores) in _allRepos)
        {
            foreach (var (user, score) in scores)
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
        }

        writer.WriteLine("    <div id='total' class='tabcontent'>");
        writer.WriteLine("        <table>");
        writer.WriteLine("            <thead>");
        writer.WriteLine("                <tr>");
        writer.WriteLine("                    <th>순위</th>");
        writer.WriteLine("                    <th>User</th>");
        writer.WriteLine("                    <th>f/b_PR</th>");
        writer.WriteLine("                    <th>doc_PR</th>");
        writer.WriteLine("                    <th>typo</th>");
        writer.WriteLine("                    <th>f/b_issue</th>");
        writer.WriteLine("                    <th>doc_issue</th>");
        writer.WriteLine("                    <th>total</th>");
        writer.WriteLine("                </tr>");
        writer.WriteLine("            </thead>");
        writer.WriteLine("            <tbody>");

        int totalCurrentRank = 1;
        double totalPreviousTotal = -1;
        int totalPosition = 0;

        foreach (var (id, score) in totalScores.OrderByDescending(x => x.Value.total))
        {
            totalPosition++;

            // 이전 점수와 다르면 현재 순위 업데이트
            if (score.total != totalPreviousTotal)
            {
                totalCurrentRank = totalPosition;
            }

            writer.WriteLine("                <tr>");
            writer.WriteLine($"                    <td class='rank'>{totalCurrentRank}</td>");
            writer.WriteLine($"                    <td>{id}</td>");
            writer.WriteLine($"                    <td>{score.PR_fb}</td>");
            writer.WriteLine($"                    <td>{score.PR_doc}</td>");
            writer.WriteLine($"                    <td>{score.PR_typo}</td>");
            writer.WriteLine($"                    <td>{score.IS_fb}</td>");
            writer.WriteLine($"                    <td>{score.IS_doc}</td>");
            writer.WriteLine($"                    <td class='total'>{score.total}</td>");
            writer.WriteLine("                </tr>");

            // 이전 점수 업데이트
            totalPreviousTotal = score.total;
        }

        writer.WriteLine("            </tbody>");
        writer.WriteLine("        </table>");
        writer.WriteLine("    </div>");

        // JavaScript for tab functionality
        writer.WriteLine("    <script>");
        writer.WriteLine("        function openTab(evt, tabName) {");
        writer.WriteLine("            var i, tabcontent, tablinks;");
        writer.WriteLine("            tabcontent = document.getElementsByClassName('tabcontent');");
        writer.WriteLine("            for (i = 0; i < tabcontent.length; i++) {");
        writer.WriteLine("                tabcontent[i].style.display = 'none';");
        writer.WriteLine("            }");
        writer.WriteLine("            tablinks = document.getElementsByClassName('tablinks');");
        writer.WriteLine("            for (i = 0; i < tablinks.length; i++) {");
        writer.WriteLine("                tablinks[i].className = tablinks[i].className.replace(' active', '');");
        writer.WriteLine("            }");
        writer.WriteLine("            document.getElementById(tabName).style.display = 'block';");
        writer.WriteLine("            evt.currentTarget.className += ' active';");
        writer.WriteLine("        }");
        // 첫 번째 탭을 기본으로 열기
        writer.WriteLine("        document.getElementsByClassName('tablinks')[0].click();");
        writer.WriteLine("    </script>");

        writer.WriteLine("</body>");
        writer.WriteLine("</html>");

        Console.WriteLine($"✅ HTML 보고서 생성 완료: {filePath}");
    }
}
