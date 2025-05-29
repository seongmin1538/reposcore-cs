using System;
using System.Collections.Generic;
using System.IO;

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
}