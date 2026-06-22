namespace AIStudyHub.Business.Configuration;

public class GuardrailsOptions
{
    public double FaithfulnessThreshold { get; set; } = 0.7;
    public double GroundingThreshold { get; set; } = 0.5;
    public double MinConfidenceScore { get; set; } = 0.4;
}
