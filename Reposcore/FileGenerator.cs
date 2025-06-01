using System;
using System.Collections.Generic;
using System.IO;
using ConsoleTables;

public class FileGenerator
{
    private readonly Dictionary<string, UserScore> _scores;
    private readonly string _repoName;
    private readonly string _folderPath;

    public FileGenerator(Dictionary<string, UserScore> repoScores, string repoName, string folderPath)
    {
        _scores = repoScores;
        _repoName = repoName;
        _folderPath = folderPath;

        // 폴더생성
        Directory.CreateDirectory(folderPath);
    }

    public void GenerateCsv()
    {
        // 경로 설정
        string filePath = Path.Combine(_folderPath, $"{_repoName}.csv");
        using StreamWriter writer = new StreamWriter(filePath);

        // CSV 헤더
        writer.WriteLine("UserId,f/b_PR,doc_PR,typo,f/b_issue,doc_issue,total");

        // 내용 작성
        foreach (var (id, socres) in _scores)
        {
            string line = $"{id},{socres.PR_fb},{socres.PR_doc},{socres.PR_typo},{socres.IS_fb},{socres.IS_doc},{socres.total}";
            writer.WriteLine(line);
        }

        Console.WriteLine($"{filePath} 생성됨");
    }
    public void GenerateTable()
    {
        // 출력할 파일 경로
        string filePath = Path.Combine(_folderPath, $"{_repoName}.txt");

        // 테이블 생성
        var headers = "UserId,f/b_PR,doc_PR,typo,f/b_issue,doc_issue,total".Split(',');

        // 각 칸의 너비 계산 (오른쪽 정렬을 위해 사용)
        int[] colWidths = headers.Select(h => h.Length).ToArray();

        var table = new ConsoleTable(headers);

        // 내용 작성
        foreach (var (id, scores) in _scores.OrderByDescending(x => x.Value.total))
        {
            table.AddRow(
                id.PadRight(colWidths[0]), // 글자는 왼쪽 정렬                   
                scores.PR_fb.ToString().PadLeft(colWidths[1]), // 숫자는 오른쪽 정렬
                scores.PR_doc.ToString().PadLeft(colWidths[2]),
                scores.PR_typo.ToString().PadLeft(colWidths[3]),
                scores.IS_fb.ToString().PadLeft(colWidths[4]),
                scores.IS_doc.ToString().PadLeft(colWidths[5]),
                scores.total.ToString().PadLeft(colWidths[6])
            );
        }

        // 파일 출력
        File.WriteAllText(filePath, table.ToMinimalString());
        Console.WriteLine($"{filePath} 생성됨");
    }
}
