namespace BookSharingWebAPI.Models;

public class CoverDetectionResult
{
    public CoverDetectionStatus AnalysisStatus { get; set; } = new();
    public CoverDetectionOcrResult? OcrResult { get; set; }
    public CoverDetectionNlpAnalysis? NlpAnalysis { get; set; }

    public bool IsSuccess => AnalysisStatus.IsSuccess;
    public string? ErrorMessage => AnalysisStatus.ErrorMessage;
    public List<string> PotentialAuthors => NlpAnalysis?.PotentialAuthors ?? [];
    public List<string> PotentialTitles => NlpAnalysis?.PotentialTitles ?? [];
    public string FullOcrText => OcrResult?.Text ?? string.Empty;

    public static CoverDetectionResult Failure(string message) => new()
    {
        AnalysisStatus = new CoverDetectionStatus { IsSuccess = false, ErrorMessage = message }
    };
}

public class CoverDetectionStatus
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}

public class CoverDetectionOcrResult
{
    public string Text { get; set; } = string.Empty;
    public List<CoverDetectionRegion> Regions { get; set; } = [];
}

public class CoverDetectionRegion
{
    public string Text { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public List<List<float>> Coordinates { get; set; } = [];
}

public class CoverDetectionNlpAnalysis
{
    public List<string> PotentialAuthors { get; set; } = [];
    public List<string> PotentialTitles { get; set; } = [];
}
