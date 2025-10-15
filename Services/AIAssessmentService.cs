//using Google.Cloud.Firestore;
//using KuwagoAPI.DTO;
//using KuwagoAPI.Models;
//using System.Text.Json;

//namespace KuwagoAPI.Services
//{
//    public class AIAssessmentService
//    {
//        private readonly FirestoreDb _firestoreDb;
//        private readonly CreditScoreService _creditScoreService;
//        private readonly string _openAIApiKey;
//        private readonly string _modelId;

//        public AIAssessmentService(FirestoreDb firestoreDb, CreditScoreService creditScoreService, IConfiguration configuration)
//        {
//            _firestoreDb = firestoreDb;
//            _creditScoreService = creditScoreService;
//            _openAIApiKey = "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";
//            _modelId = "ft:gpt-4.1-mini-2025-04-14:personal::BTh1TBmd";
//        }

//        public async Task<AIAssessmentDTO> AssessLoanEligibility(string uid)
//        {
//            try
//            {
//                // Get current credit score
//                var currentCreditScore = await _creditScoreService.GetCreditScoreAsync(uid);
//                if (currentCreditScore == null)
//                {
//                    throw new Exception("No credit score found for the user.");
//                }

//                // Get credit score history from PaymentTracking
//                var paymentTrackingQuery = _firestoreDb.Collection("PaymentTracking")
//                    .WhereEqualTo("BorrowerUID", uid)
//                    .OrderBy("PaymentDate");

//                var paymentTrackingSnapshot = await paymentTrackingQuery.GetSnapshotAsync();

//                var creditScoreHistory = new List<CreditScoreHistoryDTO>();
//                creditScoreHistory.Add(new CreditScoreHistoryDTO
//                {
//                    Score = currentCreditScore.Score,
//                    SuccessfulRepayments = currentCreditScore.SuccessfulRepayments,
//                    MissedRepayments = currentCreditScore.MissedRepayments,
//                    LastUpdated = currentCreditScore.LastUpdated.ToDateTime()
//                });

//                // Prepare data for AI assessment
//                var assessmentData = new
//                {
//                    CurrentScore = currentCreditScore.Score,
//                    TotalLoans = currentCreditScore.TotalLoans,
//                    SuccessfulRepayments = currentCreditScore.SuccessfulRepayments,
//                    MissedRepayments = currentCreditScore.MissedRepayments,
//                    PaymentHistory = paymentTrackingSnapshot.Documents.Select(doc => new
//                    {
//                        PaymentDate = ((Timestamp)doc.GetValue<Timestamp>("PaymentDate")).ToDateTime(),
//                        IsOnTime = doc.GetValue<bool>("IsOnTime"),
//                        AmountPaid = doc.GetValue<double>("AmountPaid")
//                    }).ToList()
//                };

//                // Calculate base eligibility score (0-100)
//                double baseScore = CalculateBaseEligibilityScore(currentCreditScore);

//                // Use OpenAI for detailed assessment
//                var aiAssessment = await GetAIAssessment(assessmentData);

//                bool isEligible = baseScore >= 70;

//                return new AIAssessmentDTO
//                {
//                    IsEligible = isEligible,
//                    EligibilityScore = baseScore,
//                    AssessmentMessage = aiAssessment,
//                    CreditScoreHistory = creditScoreHistory,
//                    CurrentCreditScore = currentCreditScore.Score,
//                    AssessmentDate = DateTime.UtcNow
//                };
//            }
//            catch (Exception ex)
//            {
//                throw new Exception($"Error assessing loan eligibility: {ex.Message}");
//            }
//        }

//        private double CalculateBaseEligibilityScore(mCreditScores creditScore)
//        {
//            double score = 0;

//            // Credit score weight (50%)
//            double normalizedCreditScore = (creditScore.Score - 300) / (850.0 - 300.0) * 50;
//            score += normalizedCreditScore;

//            // Payment history weight (30%)
//            if (creditScore.TotalLoans > 0)
//            {
//                double paymentSuccessRate = creditScore.SuccessfulRepayments / (double)(creditScore.SuccessfulRepayments + creditScore.MissedRepayments) * 30;
//                score += paymentSuccessRate;
//            }

//            // Loan history weight (20%)
//            double loanHistoryScore = Math.Min(creditScore.TotalLoans, 5) / 5.0 * 20;
//            score += loanHistoryScore;

//            return Math.Round(score, 2);
//        }

//        private async Task<string> GetAIAssessment(object assessmentData)
//        {
//            try
//            {
//                var api = new OpenAIAPI(_openAIApiKey);
                
//                // Format the assessment data
//                string jsonData = JsonSerializer.Serialize(assessmentData, new JsonSerializerOptions
//                {
//                    WriteIndented = true
//                });

//                var result = await api.Completions.CreateCompletionAsync(new CompletionRequest(
//                    prompt: $"You are a loan assessment AI that evaluates loan eligibility based on credit scores and payment history. Provide a concise but detailed assessment.\n\nEvaluate this borrower's loan eligibility based on their credit data:\n{jsonData}",
//                    model: _modelId,
//                    max_tokens: 500,
//                    temperature: 0.7
//                ));

//                return result.Completions[0].Text.Trim();
//            }
//            catch (Exception ex)
//            {
//                return $"Unable to generate AI assessment. Using base eligibility calculation only. Error: {ex.Message}";
//            }
//        }
//    }
//}