using System.Text.Json.Serialization;

namespace VoiceLive.Api.Models;

public class ConversationAnalysisResult
{
    [JsonPropertyName("speaking_tone_style")]
    public SpeakingToneStyle SpeakingToneStyle { get; set; } = new();

    [JsonPropertyName("conversation_content")]
    public ConversationContent ConversationContent { get; set; } = new();

    [JsonPropertyName("overall_score")]
    public int OverallScore { get; set; }

    [JsonPropertyName("strengths")]
    public List<string> Strengths { get; set; } = new();

    [JsonPropertyName("improvements")]
    public List<string> Improvements { get; set; } = new();

    [JsonPropertyName("specific_feedback")]
    public string SpecificFeedback { get; set; } = string.Empty;
}

public class SpeakingToneStyle
{
    [JsonPropertyName("professional_tone")]
    public int ProfessionalTone { get; set; }

    [JsonPropertyName("active_listening")]
    public int ActiveListening { get; set; }

    [JsonPropertyName("engagement_quality")]
    public int EngagementQuality { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

public class ConversationContent
{
    [JsonPropertyName("needs_assessment")]
    public int NeedsAssessment { get; set; }

    [JsonPropertyName("value_proposition")]
    public int ValueProposition { get; set; }

    [JsonPropertyName("objection_handling")]
    public int ObjectionHandling { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

public class AnalyzeRequest
{
    public string ScenarioId { get; set; } = string.Empty;
    public string Transcript { get; set; } = string.Empty;
    public List<AudioChunk> AudioChunks { get; set; } = new();
}

public class AudioChunk
{
    public string Data { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class AnalysisResponse
{
    public ConversationAnalysisResult ConversationAnalysis { get; set; } = new();
    public PronunciationAssessmentResult? PronunciationAssessment { get; set; }
}

public class PronunciationAssessmentResult
{
    [JsonPropertyName("accuracy_score")]
    public double AccuracyScore { get; set; }

    [JsonPropertyName("fluency_score")]
    public double FluencyScore { get; set; }

    [JsonPropertyName("completeness_score")]
    public double CompletenessScore { get; set; }

    [JsonPropertyName("prosody_score")]
    public double ProsodyScore { get; set; }

    [JsonPropertyName("pronunciation_score")]
    public double PronunciationScore { get; set; }

    [JsonPropertyName("words")]
    public List<WordAssessment> Words { get; set; } = new();
}
