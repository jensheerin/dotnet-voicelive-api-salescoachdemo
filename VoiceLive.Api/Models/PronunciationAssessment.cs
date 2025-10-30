using System.Text.Json.Serialization;

namespace VoiceLive.Api.Models;

public class PronunciationAssessment
{
    [JsonPropertyName("accuracy_score")]
    public double AccuracyScore { get; set; }

    [JsonPropertyName("fluency_score")]
    public double FluencyScore { get; set; }

    [JsonPropertyName("completeness_score")]
    public double CompletenessScore { get; set; }

    [JsonPropertyName("pron_score")]
    public double PronScore { get; set; }

    [JsonPropertyName("recognized_text")]
    public string RecognizedText { get; set; } = string.Empty;

    [JsonPropertyName("reference_text")]
    public string ReferenceText { get; set; } = string.Empty;

    [JsonPropertyName("words")]
    public List<WordAssessment>? Words { get; set; }
}

public class WordAssessment
{
    [JsonPropertyName("word")]
    public string Word { get; set; } = string.Empty;

    [JsonPropertyName("accuracy_score")]
    public double AccuracyScore { get; set; }

    [JsonPropertyName("error_type")]
    public string ErrorType { get; set; } = string.Empty;
}
