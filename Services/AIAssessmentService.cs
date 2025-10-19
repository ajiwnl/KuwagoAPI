using Google.Cloud.Firestore;
using KuwagoAPI.DTO;
using KuwagoAPI.Helper;
using KuwagoAPI.Models;
using Newtonsoft.Json;
using RestSharp;

namespace KuwagoAPI.Services
{
    public class AIAssessmentService
    {
        private readonly FirestoreDb _firestoreDb;

        public AIAssessmentService(FirestoreDb firestoreDb)
        {
            _firestoreDb = firestoreDb;
        }

        // ✅ For Borrowers: AI improvement suggestions
        public async Task<StatusResponse> GetImprovedScoreAsync(string uid)
        {
            try
            {
                var creditDoc = await _firestoreDb.Collection("CreditScores").Document(uid).GetSnapshotAsync();
                if (!creditDoc.Exists)
                    return new StatusResponse { Success = false, Message = "Credit score record not found.", StatusCode = 404 };

                var creditData = creditDoc.ToDictionary();

                var payablesQuery = await _firestoreDb.Collection("Payables")
                    .WhereEqualTo("BorrowerUID", uid)
                    .GetSnapshotAsync();

                var payablesList = payablesQuery.Documents.Select(doc => doc.ToDictionary()).ToList();

                var combinedData = new
                {
                    CreditScore = creditData.GetValueOrDefault("Score", 0),
                    TotalLoans = creditData.GetValueOrDefault("TotalLoans", 0),
                    SuccessfulRepayments = creditData.GetValueOrDefault("SuccessfulRepayments", 0),
                    MissedRepayments = creditData.GetValueOrDefault("MissedRepayments", 0),
                    Payables = payablesList.Select(p => new
                    {
                        PayableID = p.GetValueOrDefault("PayableID", ""),
                        TotalPayableAmount = p.GetValueOrDefault("TotalPayableAmount", 0),
                        TermsOfMonths = p.GetValueOrDefault("TermsOfMonths", ""),
                        PaymentStatus = p.ContainsKey("IsFullyPaid") && (bool)p["IsFullyPaid"]
                            ? "Paid"
                            : (p.ContainsKey("RemainingBalance") && Convert.ToDouble(p["RemainingBalance"]) > 0 ? "Ongoing" : "Pending")
                    })
                };

                string creditDataJson = JsonConvert.SerializeObject(combinedData, Formatting.Indented);

                var client = new RestClient("https://mimanu-faq.vercel.app/loan-assessment");
                var request = new RestRequest("", Method.Post);
                request.AddHeader("Content-Type", "application/json");
                request.AddJsonBody(new
                {
                    credit_data = $"Based on the following borrower financial record, analyze their credit performance and suggest specific, actionable improvements. " +
                                  $"Focus on habits and behaviors that would increase their credit score, reliability, and trustworthiness to lenders. " +
                                  $"Provide clear guidance in bullet points with reasoning for each suggestion:\n\n{creditDataJson}"
                });

                var response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    return new StatusResponse { Success = false, Message = $"AI error: {response.StatusCode}", StatusCode = (int)response.StatusCode };

                dynamic aiResponse = JsonConvert.DeserializeObject(response.Content);
                string assessmentText = aiResponse?.assessment?.ToString() ?? "No suggestion returned by AI.";

                return new StatusResponse
                {
                    Success = true,
                    StatusCode = 200,
                    Message = "AI improvement suggestions retrieved successfully.",
                    Data = new
                    {
                        BorrowerUID = uid,
                        CreditData = combinedData,
                        AISuggestion = assessmentText
                    }
                };
            }
            catch (Exception ex)
            {
                return new StatusResponse
                {
                    Success = false,
                    Message = $"Error generating AI score improvement: {ex.Message}",
                    StatusCode = 500
                };
            }
        }

        // ✅ NEW: For Lenders/Admins - AI Loan Worthiness Assessment
        public async Task<StatusResponse> GetLoanApprovalAnalysisAsync(string uid)
        {
            try
            {
                // 1️⃣ Fetch Credit Score Data
                var creditDoc = await _firestoreDb.Collection("CreditScores").Document(uid).GetSnapshotAsync();
                if (!creditDoc.Exists)
                {
                    return new StatusResponse
                    {
                        Success = false,
                        Message = "Credit score record not found for borrower.",
                        StatusCode = 404
                    };
                }

                var creditData = creditDoc.ToDictionary();

                // 2️⃣ Fetch Payables Data
                var payablesQuery = await _firestoreDb.Collection("Payables")
                    .WhereEqualTo("BorrowerUID", uid)
                    .GetSnapshotAsync();

                var payablesList = payablesQuery.Documents.Select(doc => doc.ToDictionary()).ToList();

                // 3️⃣ Combine Data
                var combinedData = new
                {
                    CreditScore = creditData.GetValueOrDefault("Score", 0),
                    TotalLoans = creditData.GetValueOrDefault("TotalLoans", 0),
                    SuccessfulRepayments = creditData.GetValueOrDefault("SuccessfulRepayments", 0),
                    MissedRepayments = creditData.GetValueOrDefault("MissedRepayments", 0),
                    Payables = payablesList.Select(p => new
                    {
                        PayableID = p.GetValueOrDefault("PayableID", ""),
                        TotalPayableAmount = p.GetValueOrDefault("TotalPayableAmount", 0),
                        TermsOfMonths = p.GetValueOrDefault("TermsOfMonths", ""),
                        PaymentType = p.GetValueOrDefault("PaymentType", ""),
                        RemainingBalance = p.GetValueOrDefault("RemainingBalance", 0),
                        PaymentStatus = p.ContainsKey("IsFullyPaid") && (bool)p["IsFullyPaid"]
                            ? "Paid"
                            : (p.ContainsKey("RemainingBalance") && Convert.ToDouble(p["RemainingBalance"]) > 0 ? "Ongoing" : "Pending")
                    })
                };

                string creditDataJson = JsonConvert.SerializeObject(combinedData, Formatting.Indented);

                // 4️⃣ Call the AI for Lender Risk Assessment
                var client = new RestClient("https://mimanu-faq.vercel.app/loan-assessment");
                var request = new RestRequest("", Method.Post);
                request.AddHeader("Content-Type", "application/json");
                request.AddJsonBody(new
                {
                    credit_data = $"Perform a loan approval and risk analysis for the following borrower data. " +
                                  $"Assess if this borrower is trustworthy, likely to repay on time, or high-risk. " +
                                  $"Provide reasoning, risk level (Low/Medium/High), and a recommendation (Approve/Reject):\n\n{creditDataJson}"
                });

                var response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                {
                    return new StatusResponse
                    {
                        Success = false,
                        Message = $"AI endpoint error: {response.StatusCode} - {response.ErrorMessage}",
                        StatusCode = (int)response.StatusCode
                    };
                }

                dynamic aiResponse = JsonConvert.DeserializeObject(response.Content);
                string assessmentText = aiResponse?.assessment?.ToString() ?? "No assessment returned by AI.";

                return new StatusResponse
                {
                    Success = true,
                    StatusCode = 200,
                    Message = "AI loan approval assessment retrieved successfully.",
                    Data = new
                    {
                        BorrowerUID = uid,
                        CreditData = combinedData,
                        AILoanAssessment = assessmentText
                    }
                };
            }
            catch (Exception ex)
            {
                return new StatusResponse
                {
                    Success = false,
                    Message = $"Error generating loan approval analysis: {ex.Message}",
                    StatusCode = 500
                };
            }
        }
    }

    // Helper extension for cleaner GetValueOrDefault on dictionaries
    public static class DictionaryExtensions
    {
        public static dynamic GetValueOrDefault(this IDictionary<string, object> dict, string key, dynamic defaultValue)
        {
            return dict.ContainsKey(key) ? dict[key] : defaultValue;
        }
    }
}
