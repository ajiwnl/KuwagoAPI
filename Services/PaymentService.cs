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
            try
            {
                // Fetch the payable record
                var payableSnapshot = await _firestoreDb.Collection("Payables")
                    .Document(payableId)
                    .GetSnapshotAsync();

                if (!payableSnapshot.Exists)
                {
                    return new StatusResponse
                    {
                        Success = false,
                        Message = "Payable not found.",
                        StatusCode = 404
                    };
                }

                var payable = payableSnapshot.ToDictionary();
                double totalPayableAmount = 0;

                if (payable.ContainsKey("TotalPayableAmount"))
                {
                    var value = payable["TotalPayableAmount"];
                    if (value is double d)
                        totalPayableAmount = d;
                    else if (value is long l)
                        totalPayableAmount = Convert.ToDouble(l);
                    else if (value is int i)
                        totalPayableAmount = Convert.ToDouble(i);
                    else
                        totalPayableAmount = Convert.ToDouble(value);
                }

                // Get all payments for this payable
                var payments = await GetPaymentsByPayable(payableId);

                double totalPaid = 0;
                foreach (var p in payments)
                {
                    var amountPaidObj = ((dynamic)p).AmountPaid;

                    if (amountPaidObj is double d)
                        totalPaid += d;
                    else if (amountPaidObj is long l)
                        totalPaid += Convert.ToDouble(l);
                    else if (amountPaidObj is int i)
                        totalPaid += Convert.ToDouble(i);
                    else
                        totalPaid += Convert.ToDouble(amountPaidObj);
                }

                // Compute remaining balance and status
                double remainingBalance = Math.Round(totalPayableAmount - totalPaid, 2);
                bool isFullyPaid = remainingBalance <= 0;

                var summary = new
                {
                    PayableID = payableId,
                    TotalPayableAmount = totalPayableAmount,
                    TotalPaid = totalPaid,
                    RemainingBalance = remainingBalance,
                    IsFullyPaid = isFullyPaid,
                    PaymentCount = payments.Count,
                    Payments = payments
                };

                return new StatusResponse
                {
                    Success = true,
                    StatusCode = 200,
                    Message = "Payment summary retrieved successfully.",
                    Data = summary
                };
            }
            catch (Exception ex)
            {
                return new StatusResponse
                {
                    Success = false,
                    Message = $"An error occurred while getting summary: {ex.Message}",
                    StatusCode = 500
                };
            }
        }


        public async Task<StatusResponse> GetPaymentScheduleDetails(string borrowerUid, string payableId)
        {
            try
            {
                var payableSnapshot = await _firestoreDb.Collection("Payables")
                    .Document(payableId)
                    .GetSnapshotAsync();

                if (!payableSnapshot.Exists)
                    return new StatusResponse { Success = false, Message = "Payable not found", StatusCode = 404 };

                var payable = payableSnapshot.ToDictionary();
                
                // Verify the borrower owns this payable
                if (payable["BorrowerUID"].ToString() != borrowerUid)
                    return new StatusResponse { Success = false, Message = "Unauthorized access", StatusCode = 403 };

                double totalPayableAmount = Convert.ToDouble(payable["TotalPayableAmount"]);
                int terms = Enum.TryParse<TermsOfMonths>(payable["TermsOfMonths"].ToString(), out var parsedTerms) ? (int)parsedTerms : 0;
                var paymentSchedule = payable.ContainsKey("PaymentSchedule") ? (List<object>)payable["PaymentSchedule"] : null;

                var monthlyPayment = Math.Round(totalPayableAmount / terms, 2);

                var scheduleDates = paymentSchedule?
                    .OfType<Timestamp>()
                    .Select(ts => ts.ToDateTime().ToString("yyyy-MM-dd"))
                    .ToList() ?? new List<string>();

                // Get actual paid dates
                var paymentsQuery = await _firestoreDb.Collection("Payments")
                    .WhereEqualTo("PayableID", payableId)
                    .WhereEqualTo("BorrowerUID", borrowerUid)
                    .OrderBy("PaymentDate")
                    .GetSnapshotAsync();

                var paidDates = paymentsQuery.Documents
                    .Select(doc => ((Timestamp)doc.GetValue<Timestamp>("PaymentDate")).ToDateTime().ToString("yyyy-MM-dd"))
                    .ToList();

                return new StatusResponse
                {
                    Success = true,
                    StatusCode = 200,
                    Data = new
                    {
                        PayableID = payableId,
                        BorrowerUID = borrowerUid,
                        MonthlyPayment = monthlyPayment,
                        TotalAmount = totalPayableAmount,
                        Terms = terms,
                        ScheduledDates = scheduleDates,
                        PaidDates = paidDates
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


    }

}
