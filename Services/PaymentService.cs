using Google.Cloud.Firestore;
using KuwagoAPI.DTO;
using KuwagoAPI.Helper;
using KuwagoAPI.Models;
using static KuwagoAPI.Helper.LoanEnums;

namespace KuwagoAPI.Services
{
    public class PaymentService
    {
        private readonly FirestoreDb _firestoreDb;
        private readonly CreditScoreService _creditScoreService;

        public PaymentService(FirestoreDb firestoreDb, CreditScoreService creditScoreService)
        {
            _firestoreDb = firestoreDb;
            _creditScoreService = creditScoreService;
        }

        public async Task<StatusResponse> SubmitPaymentAsync(PaymentRequestDTO dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.PayableID))
                    return new StatusResponse { Success = false, Message = "PayableID is required.", StatusCode = 400 };

                if (string.IsNullOrWhiteSpace(dto.BorrowerUID))
                    return new StatusResponse { Success = false, Message = "BorrowerUID is required.", StatusCode = 400 };

                if (dto.AmountPaid <= 0)
                    return new StatusResponse { Success = false, Message = "AmountPaid must be greater than zero.", StatusCode = 400 };

                // Get Payable
                var payableSnapshot = await _firestoreDb.Collection("Payables").Document(dto.PayableID).GetSnapshotAsync();
                if (!payableSnapshot.Exists)
                    return new StatusResponse { Success = false, Message = "Payable record not found.", StatusCode = 404 };

                var payable = payableSnapshot.ToDictionary();
                double totalPayableAmount = Convert.ToDouble(payable["TotalPayableAmount"]);
                int terms = Enum.TryParse<TermsOfMonths>(payable["TermsOfMonths"].ToString(), out var parsedTerms) ? (int)parsedTerms : 0;
                string loanRequestId = payable.ContainsKey("LoanRequestID") ? payable["LoanRequestID"].ToString() : null;
                string lenderUid = payable.ContainsKey("LenderUID") ? payable["LenderUID"].ToString() : null;
                var paymentSchedule = payable.ContainsKey("PaymentSchedule") ? (List<object>)payable["PaymentSchedule"] : null;

                if (terms <= 0)
                    return new StatusResponse { Success = false, Message = "Invalid TermsOfMonths value in Payable record.", StatusCode = 500 };

                double requiredInstallment = Math.Round(totalPayableAmount / terms, 2);
                if (dto.AmountPaid < requiredInstallment)
                {
                    return new StatusResponse
                    {
                        Success = false,
                        Message = $"Minimum payment required per installment is {requiredInstallment}. You submitted {dto.AmountPaid}.",
                        StatusCode = 400
                    };
                }

                // Get total paid so far for this PayableID
                var paymentsQuery = await _firestoreDb.Collection("Payments")
                    .WhereEqualTo("PayableID", dto.PayableID)
                    .GetSnapshotAsync();
                double totalPaid = paymentsQuery.Documents.Sum(doc => Convert.ToDouble(doc.ToDictionary()["AmountPaid"]));

                // Reject payment if it would exceed TotalPayableAmount
                if (totalPaid + dto.AmountPaid > totalPayableAmount)
                {
                    return new StatusResponse
                    {
                        Success = false,
                        Message = $"Total payments would exceed the allowed amount ({totalPayableAmount}). Current paid: {totalPaid}, attempted: {dto.AmountPaid}",
                        StatusCode = 400
                    };
                }

                // Remove restriction on payment date: allow advance payments
                bool isOnTime = true;

                // Find next due date in PaymentSchedule
                DateTime? nextDueDate = null;
                if (paymentSchedule != null && paymentSchedule.Count > 0)
                {
                    var now = DateTime.UtcNow;
                    foreach (var tsObj in paymentSchedule)
                    {
                        if (tsObj is Timestamp ts)
                        {
                            var dueDate = ts.ToDateTime();
                            if (dueDate >= now)
                            {
                                nextDueDate = dueDate;
                                break;
                            }
                        }
                    }
                }

                isOnTime = nextDueDate.HasValue && DateTime.UtcNow <= nextDueDate.Value;

                // Update credit score based on payment timing
                await _creditScoreService.UpdateCreditScoreAsync(dto.BorrowerUID, isOnTime);

                // Save Payment
                var paymentDoc = _firestoreDb.Collection("Payments").Document();
                await paymentDoc.SetAsync(new
                {
                    PaymentID = paymentDoc.Id,
                    PayableID = dto.PayableID,
                    BorrowerUID = dto.BorrowerUID,
                    AmountPaid = dto.AmountPaid,
                    PaymentDate = Timestamp.FromDateTime(DateTime.UtcNow),
                    Notes = dto.Notes ?? ""
                });
                var paymentDateString = Timestamp.FromDateTime(DateTime.UtcNow)
                                  .ToDateTime()
                                  .ToString("yyyy-MM-dd HH:mm:ss");

                // Track payment in PaymentTracking collection
                var paymentTrackingDoc = _firestoreDb.Collection("PaymentTracking").Document();
                await paymentTrackingDoc.SetAsync(new
                {
                    PaymentTrackingID = paymentTrackingDoc.Id,
                    PaymentID = paymentDoc.Id,
                    PayableID = dto.PayableID,
                    LoanRequestID = loanRequestId,
                    BorrowerUID = dto.BorrowerUID,
                    LenderUID = lenderUid,
                    AmountPaid = dto.AmountPaid,
                    PaymentDate = Timestamp.FromDateTime(DateTime.UtcNow),
                    DueDate = Timestamp.FromDateTime(nextDueDate.HasValue ? nextDueDate.Value : DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)),
                    IsOnTime = isOnTime
                });

                return new StatusResponse
                {
                    Success = true,
                    Message = "Payment submitted successfully.",
                    StatusCode = 200,
                    Data = new
                    {
                        PaymentID = paymentDoc.Id,
                        AmountPaid = dto.AmountPaid,
                        RequiredPerInstallment = requiredInstallment,
                        PaymentDate = paymentDateString,
                        IsOnTime = isOnTime,
                        DueDate = nextDueDate?.ToString("yyyy-MM-dd")
                    }
                };
            }
            catch (Exception ex)
            {
                return new StatusResponse
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}",
                    StatusCode = 500
                };
            }
        }

        public async Task<List<object>> GetPaymentsByBorrower(string borrowerUid)
        {
            var query = await _firestoreDb.Collection("Payments")
                .WhereEqualTo("BorrowerUID", borrowerUid)
                .OrderBy("PaymentDate")
                .GetSnapshotAsync();

            return query.Documents.Select(doc =>
            {
                var data = doc.ToDictionary();
                return new
                {
                    PaymentID = doc.Id,
                    PayableID = data["PayableID"],
                    BorrowerUID = data["BorrowerUID"],
                    AmountPaid = data["AmountPaid"],
                    PaymentDate = ((Timestamp)data["PaymentDate"]).ToDateTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    Notes = data.ContainsKey("Notes") ? data["Notes"] : ""
                };
            }).ToList<object>();
        }

        public async Task<List<object>> GetPaymentsByPayable(string payableId)
        {
            var query = await _firestoreDb.Collection("Payments")
                .WhereEqualTo("PayableID", payableId)
                .OrderBy("PaymentDate")
                .GetSnapshotAsync();

            return query.Documents.Select(doc =>
            {
                var data = doc.ToDictionary();
                return new
                {
                    PaymentID = doc.Id,
                    PayableID = data["PayableID"],
                    BorrowerUID = data["BorrowerUID"],
                    AmountPaid = data["AmountPaid"],
                    PaymentDate = ((Timestamp)data["PaymentDate"]).ToDateTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    Notes = data.ContainsKey("Notes") ? data["Notes"] : ""
                };
            }).ToList<object>();
        }

        public async Task<StatusResponse> GetPaymentSummary(string payableId)
        {
            var payments = await GetPaymentsByPayable(payableId);

            var totalPaid = payments.Sum(p => Convert.ToDouble(((dynamic)p).AmountPaid));

            return new StatusResponse
            {
                Success = true,
                StatusCode = 200,
                Data = new
                {
                    PayableID = payableId,
                    TotalPaid = totalPaid,
                    PaymentCount = payments.Count,
                    Payments = payments
                }
            };
        }

    }

}
