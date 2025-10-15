using System;

namespace KuwagoAPI.DTO
{
    public class AIAssessmentDTO
    {
        public bool IsEligible { get; set; }
        public double EligibilityScore { get; set; }
        public string AssessmentMessage { get; set; }
        public List<CreditScoreHistoryDTO> CreditScoreHistory { get; set; }
        public int CurrentCreditScore { get; set; }
        public DateTime AssessmentDate { get; set; }
    }

    public class CreditScoreHistoryDTO
    {
        public int Score { get; set; }
        public int SuccessfulRepayments { get; set; }
        public int MissedRepayments { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}