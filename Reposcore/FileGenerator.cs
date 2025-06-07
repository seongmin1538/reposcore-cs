using System;
using System.Collections.Generic;
using System.IO;
using ConsoleTables;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.TickGenerators;

public class FileGenerator
{
    private readonly Dictionary<string, UserScore> _scores;
    private readonly string _repoName;
    private readonly string _folderPath;

    public FileGenerator(Dictionary<string, UserScore> repoScores, string repoName, string folderPath)
    {
        _scores = repoScores;
        _repoName = repoName;
        _folderPath = Path.Combine(folderPath, repoName);
        Directory.CreateDirectory(_folderPath);
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

        // CSV 헤더
        writer.WriteLine("UserId,f/b_PR,doc_PR,typo,f/b_issue,doc_issue,PR_rate,IS_rate,total");

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

        // 파일 출력
        File.WriteAllText(filePath, table.ToMinimalString());
        Console.WriteLine($"{filePath} 생성됨");
    }

    public void GenerateChart()
    {
        var labels = new List<string>();
        var values = new List<double>();

        foreach (var (user, score) in _scores.OrderBy(x => x.Value.total)) // 오름차순
        {
            labels.Add(user);
            values.Add(score.total);
        }

        string[] names = labels.ToArray();
        double[] scores = values.ToArray();
        
        // ✅ 간격 조절된 Position
        double spacing = 10; // 막대 간격
        double[] positions = Enumerable.Range(0, names.Length)
                                    .Select(i => i * spacing)
                                    .ToArray();

        // Bar 데이터 생성
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
        }

        var plt = new ScottPlot.Plot();
        var barPlot = plt.Add.Bars(bars);

        plt.Axes.Left.TickGenerator = new NumericManual(positions, names);
        plt.Title($"Scores - {_repoName}");
        plt.XLabel("총 점수");
        plt.YLabel("사용자");

        string outputPath = Path.Combine(_folderPath, $"{_repoName}_chart.png");
        plt.SavePng(outputPath, 1920, 1080);
        Console.WriteLine($"✅ 차트 생성 완료: {outputPath}");
    }


}
