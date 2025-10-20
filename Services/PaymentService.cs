using Google.Cloud.Firestore;
using KuwagoAPI.DTO;
using KuwagoAPI.Helper;
using KuwagoAPI.Models;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Crmf;
using RestSharp;
using static KuwagoAPI.Helper.LoanEnums;

namespace KuwagoAPI.Services
{
    public class PaymentService
    {
        private readonly FirestoreDb _firestoreDb;
        private readonly CreditScoreService _creditScoreService;
        private readonly IConfiguration _configuration;

        public PaymentService(FirestoreDb firestoreDb, CreditScoreService creditScoreService, IConfiguration configuration)
        {
            _firestoreDb = firestoreDb;
            _creditScoreService = creditScoreService;
            _configuration = configuration;
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
                //if (dto.AmountPaid < requiredInstallment)
                //{
                //    return new StatusResponse
                //    {
                //        Success = false,
                //        Message = $"Minimum payment required per installment is {requiredInstallment}. You submitted {dto.AmountPaid}.",
                //        StatusCode = 400
                //    };
                //}

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

                bool isOnTime = nextDueDate.HasValue && DateTime.UtcNow <= nextDueDate.Value;

                // Handle payment based on PaymentType
                if (string.IsNullOrWhiteSpace(dto.PaymentType) || dto.PaymentType.Equals("Cash", StringComparison.OrdinalIgnoreCase))
                {
                    // Default Cash payment flow
                    return await ProcessCashPayment(dto, requiredInstallment, nextDueDate, isOnTime, loanRequestId, lenderUid);
                }
                else if (dto.PaymentType.Equals("ECash", StringComparison.OrdinalIgnoreCase))
                {
                    // PayMongo ECash payment flow
                    return await ProcessECashPayment(dto, requiredInstallment, nextDueDate, isOnTime, loanRequestId, lenderUid);
                }
                else
                {
                    return new StatusResponse
                    {
                        Success = false,
                        Message = "Invalid PaymentType. Supported types: Cash, ECash",
                        StatusCode = 400
                    };
                }
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

        private async Task<StatusResponse> ProcessCashPayment(PaymentRequestDTO dto, double requiredInstallment,
            DateTime? nextDueDate, bool isOnTime, string loanRequestId, string lenderUid)
        {
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
                PaymentType = "Cash",
                PaymentDate = Timestamp.FromDateTime(DateTime.UtcNow),
                Notes = dto.Notes ?? "",
                Status = "Completed"
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
                    DueDate = nextDueDate?.ToString("yyyy-MM-dd"),
                    PaymentType = "Cash"
                }
            };
        }

        private async Task<StatusResponse> ProcessECashPayment(PaymentRequestDTO dto, double requiredInstallment,
            DateTime? nextDueDate, bool isOnTime, string loanRequestId, string lenderUid)
        {
            try
            {
                // Create pending payment record first
                var paymentDoc = _firestoreDb.Collection("Payments").Document();
                await paymentDoc.SetAsync(new
                {
                    PaymentID = paymentDoc.Id,
                    PayableID = dto.PayableID,
                    BorrowerUID = dto.BorrowerUID,
                    AmountPaid = dto.AmountPaid,
                    PaymentType = "ECash",
                    PaymentDate = Timestamp.FromDateTime(DateTime.UtcNow),
                    Notes = dto.Notes ?? "",
                    Status = "Pending"
                });

                // Create PayMongo checkout session
                var checkoutUrl = await CreatePayMongoCheckout(dto, paymentDoc.Id, requiredInstallment, nextDueDate, isOnTime);

                if (string.IsNullOrWhiteSpace(checkoutUrl))
                {
                    // Rollback - delete the pending payment
                    await paymentDoc.DeleteAsync();
                    return new StatusResponse
                    {
                        Success = false,
                        Message = "Failed to create payment checkout session.",
                        StatusCode = 500
                    };
                }

                return new StatusResponse
                {
                    Success = true,
                    Message = "Payment checkout session created. Please complete the payment.",
                    StatusCode = 200,
                    Data = new
                    {
                        PaymentID = paymentDoc.Id,
                        AmountPaid = dto.AmountPaid,
                        RequiredPerInstallment = requiredInstallment,
                        CheckoutUrl = checkoutUrl,
                        PaymentType = "ECash",
                        Status = "Pending"
                    }
                };
            }
            catch (Exception ex)
            {
                return new StatusResponse
                {
                    Success = false,
                    Message = $"ECash payment error: {ex.Message}",
                    StatusCode = 500
                };
            }
        }

        private async Task<string> CreatePayMongoCheckout(PaymentRequestDTO dto, string paymentId,
            double requiredInstallment, DateTime? nextDueDate, bool isOnTime)
        {
            try
            {
                var options = new RestClientOptions("https://api.paymongo.com/v1/checkout_sessions");
                var client = new RestClient(options);

                // Get URLs from configuration
                string successUrl = _configuration["PayMongo:SuccessUrl"] ?? "https://localhost:7074/api/Payment/Success";
                string failedUrl = _configuration["PayMongo:FailedUrl"] ?? "https://localhost:7074/api/Payment/Failed";

                // Append payment ID to URLs for tracking
                successUrl = $"{successUrl}?paymentId={paymentId}";
                failedUrl = $"{failedUrl}?paymentId={paymentId}";

                // Convert amount to centavos (PayMongo expects amount in smallest currency unit)
                int amountInCentavos = Convert.ToInt32(dto.AmountPaid * 100);

                string description = $"Loan Payment - Payable: {dto.PayableID}";
                if (!string.IsNullOrWhiteSpace(dto.Notes))
                    description += $" | {dto.Notes}";

                var requestBodyJson = JsonConvert.SerializeObject(new
                {
                    data = new
                    {
                        attributes = new
                        {
                            send_email_receipt = true,
                            show_description = true,
                            show_line_items = true,
                            description = description,
                            line_items = new[]
                            {
                                new
                                {
                                    currency = "PHP",
                                    amount = amountInCentavos,
                                    description = $"Installment Payment (Required: PHP {requiredInstallment})",
                                    quantity = 1,
                                    name = "Loan Payment"
                                }
                            },
                            payment_method_types = new[] { "gcash", "paymaya", "grab_pay" },
                            success_url = successUrl,
                            cancel_url = failedUrl,
                            metadata = new
                            {
                                payment_id = paymentId,
                                payable_id = dto.PayableID,
                                borrower_uid = dto.BorrowerUID
                            }
                        }
                    }
                });

                var request = new RestRequest("");
                request.AddHeader("accept", "application/json");

                // Get API key from configuration (should be stored securely)
                string apiKey = _configuration["PayMongo:SecretKey"] ?? "sk_test_your_key_here";
                string authHeader = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{apiKey}:"));
                request.AddHeader("authorization", $"Basic {authHeader}");
                request.AddJsonBody(requestBodyJson, false);

                var response = await client.PostAsync(request);

                if (response.IsSuccessful)
                {
                    dynamic responseObject = JsonConvert.DeserializeObject(response.Content);
                    string checkoutUrl = responseObject.data.attributes.checkout_url;

                    // Store checkout session ID for later verification
                    string checkoutSessionId = responseObject.data.id;
                    await UpdatePaymentCheckoutSession(paymentId, checkoutSessionId, checkoutUrl);

                    return checkoutUrl;
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PayMongo checkout creation failed: {ex.Message}");
                return null;
            }
        }

        private async Task UpdatePaymentCheckoutSession(string paymentId, string checkoutSessionId, string checkoutUrl)
        {
            var paymentRef = _firestoreDb.Collection("Payments").Document(paymentId);
            await paymentRef.UpdateAsync(new Dictionary<string, object>
            {
                { "CheckoutSessionId", checkoutSessionId },
                { "CheckoutUrl", checkoutUrl }
            });
        }

        public async Task<StatusResponse> CompleteECashPayment(string paymentId)
        {
            try
            {
                var paymentSnapshot = await _firestoreDb.Collection("Payments").Document(paymentId).GetSnapshotAsync();
                if (!paymentSnapshot.Exists)
                    return new StatusResponse { Success = false, Message = "Payment record not found.", StatusCode = 404 };

                var payment = paymentSnapshot.ToDictionary();

                // Check if already completed
                if (payment["Status"].ToString() == "Completed")
                    return new StatusResponse { Success = true, Message = "Payment already completed.", StatusCode = 200 };

                // Update payment status
                await _firestoreDb.Collection("Payments").Document(paymentId).UpdateAsync(new Dictionary<string, object>
                {
                    { "Status", "Completed" },
                    { "CompletedAt", Timestamp.FromDateTime(DateTime.UtcNow) }
                });

                // Get payable information for credit score update
                string payableId = payment["PayableID"].ToString();
                string borrowerUid = payment["BorrowerUID"].ToString();
                double amountPaid = Convert.ToDouble(payment["AmountPaid"]);

                var payableSnapshot = await _firestoreDb.Collection("Payables").Document(payableId).GetSnapshotAsync();
                var payable = payableSnapshot.ToDictionary();
                string loanRequestId = payable.ContainsKey("LoanRequestID") ? payable["LoanRequestID"].ToString() : null;
                string lenderUid = payable.ContainsKey("LenderUID") ? payable["LenderUID"].ToString() : null;
                var paymentSchedule = payable.ContainsKey("PaymentSchedule") ? (List<object>)payable["PaymentSchedule"] : null;

                // Calculate if on time
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

                bool isOnTime = nextDueDate.HasValue && DateTime.UtcNow <= nextDueDate.Value;

                // Update credit score
                await _creditScoreService.UpdateCreditScoreAsync(borrowerUid, isOnTime);

                // Create payment tracking record
                var paymentTrackingDoc = _firestoreDb.Collection("PaymentTracking").Document();
                await paymentTrackingDoc.SetAsync(new
                {
                    PaymentTrackingID = paymentTrackingDoc.Id,
                    PaymentID = paymentId,
                    PayableID = payableId,
                    LoanRequestID = loanRequestId,
                    BorrowerUID = borrowerUid,
                    LenderUID = lenderUid,
                    AmountPaid = amountPaid,
                    PaymentDate = Timestamp.FromDateTime(DateTime.UtcNow),
                    DueDate = Timestamp.FromDateTime(nextDueDate.HasValue ? nextDueDate.Value : DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)),
                    IsOnTime = isOnTime
                });

                return new StatusResponse
                {
                    Success = true,
                    Message = "ECash payment completed successfully.",
                    StatusCode = 200,
                    Data = new
                    {
                        PaymentID = paymentId,
                        Status = "Completed",
                        IsOnTime = isOnTime
                    }
                };
            }
            catch (Exception ex)
            {
                return new StatusResponse
                {
                    Success = false,
                    Message = $"Error completing payment: {ex.Message}",
                    StatusCode = 500
                };
            }
        }

        public async Task<StatusResponse> CancelECashPayment(string paymentId)
        {
            try
            {
                var paymentSnapshot = await _firestoreDb.Collection("Payments").Document(paymentId).GetSnapshotAsync();
                if (!paymentSnapshot.Exists)
                    return new StatusResponse { Success = false, Message = "Payment record not found.", StatusCode = 404 };

                // Update payment status to cancelled
                await _firestoreDb.Collection("Payments").Document(paymentId).UpdateAsync(new Dictionary<string, object>
                {
                    { "Status", "Cancelled" },
                    { "CancelledAt", Timestamp.FromDateTime(DateTime.UtcNow) }
                });

                return new StatusResponse
                {
                    Success = true,
                    Message = "Payment cancelled.",
                    StatusCode = 200
                };
            }
            catch (Exception ex)
            {
                return new StatusResponse
                {
                    Success = false,
                    Message = $"Error cancelling payment: {ex.Message}",
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
                    PaymentType = data.ContainsKey("PaymentType") ? data["PaymentType"] : "Cash",
                    Status = data.ContainsKey("Status") ? data["Status"] : "Completed",
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
                    PaymentType = data.ContainsKey("PaymentType") ? data["PaymentType"] : "Cash",
                    Status = data.ContainsKey("Status") ? data["Status"] : "Completed",
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

                // Verify ownership
                if (payable["BorrowerUID"].ToString() != borrowerUid)
                    return new StatusResponse { Success = false, Message = "Unauthorized access", StatusCode = 403 };

                double totalPayableAmount = Convert.ToDouble(payable["TotalPayableAmount"]);
                int terms = Enum.TryParse<TermsOfMonths>(payable["TermsOfMonths"].ToString(), out var parsedTerms) ? (int)parsedTerms : 0;
                var paymentSchedule = payable.ContainsKey("PaymentSchedule") ? (List<object>)payable["PaymentSchedule"] : null;
                var monthlyPayment = Math.Round(totalPayableAmount / terms, 2);

                // 🔹 Convert payment schedule timestamps to ordered due dates
                var scheduleDates = paymentSchedule?
                    .OfType<Timestamp>()
                    .Select(ts => ts.ToDateTime().Date)
                    .OrderBy(d => d)
                    .ToList() ?? new List<DateTime>();

                // 🔹 Fetch actual payments
                var paymentsSnapshot = await _firestoreDb.Collection("Payments")
                    .WhereEqualTo("PayableID", payableId)
                    .WhereEqualTo("BorrowerUID", borrowerUid)
                    .OrderBy("PaymentDate")
                    .GetSnapshotAsync();

                var paymentRecords = paymentsSnapshot.Documents
                    .Select(doc => new
                    {
                        PaymentDate = ((Timestamp)doc.GetValue<Timestamp>("PaymentDate")).ToDateTime().Date,
                        AmountPaid = doc.ContainsField("AmountPaid") ? Convert.ToDouble(doc.GetValue<double>("AmountPaid")) : 0.0
                    })
                    .OrderBy(x => x.PaymentDate)
                    .ToList();

                var scheduleList = new List<PaymentScheduleItem>();
                var unpaidDates = new List<string>();

                // 🔹 If no payments exist → mark all as unpaid
                if (!paymentRecords.Any())
                {
                    foreach (var dueDate in scheduleDates)
                    {
                        scheduleList.Add(new PaymentScheduleItem
                        {
                            DueDate = dueDate.ToString("yyyy-MM-dd"),
                            PaymentDate = null,
                            AmountPaid = 0,
                            RequiredToPayEveryMonth = monthlyPayment,
                            ActualPayment = 0,
                            Status = "Unpaid"
                        });
                        unpaidDates.Add(dueDate.ToString("yyyy-MM-dd"));
                    }
                }
                else
                {
                    var remainingPayments = new Queue<(DateTime date, double amount)>(
                        paymentRecords.Select(x => (x.PaymentDate, x.AmountPaid))
                    );

                    double advanceBalance = 0; // carry forward any overpayment

                    foreach (var dueDate in scheduleDates)
                    {
                        string status;
                        DateTime? paymentDate = null;
                        double amountPaid = 0;
                        double actualPayment = 0;
                        double requiredPayment = monthlyPayment;

                        // 🔹 Adjust required payment if previous advance exists
                        if (advanceBalance > 0)
                        {
                            requiredPayment = Math.Max(0, monthlyPayment - advanceBalance);
                        }

                        if (remainingPayments.Count == 0)
                        {
                            if (advanceBalance > 0)
                            {
                                double appliedAdvance = Math.Min(advanceBalance, monthlyPayment);
                                actualPayment = Math.Round(monthlyPayment - appliedAdvance, 2);
                                advanceBalance = Math.Max(0, advanceBalance - (monthlyPayment - appliedAdvance));
                                status = "Advance Applied";
                            }
                            else
                            {
                                status = "Unpaid";
                                unpaidDates.Add(dueDate.ToString("yyyy-MM-dd"));
                            }
                        }
                        else
                        {
                            var nextPayment = remainingPayments.Peek();

                            if (nextPayment.date <= dueDate)
                            {
                                paymentDate = nextPayment.date;
                                amountPaid = nextPayment.amount;
                                remainingPayments.Dequeue();

                                double totalAvailable = amountPaid + advanceBalance;

                                if (totalAvailable >= monthlyPayment)
                                {
                                    actualPayment = monthlyPayment;
                                    advanceBalance = totalAvailable - monthlyPayment;
                                    status = nextPayment.date < dueDate ? "Advance" : "Paid";
                                }
                                else
                                {
                                    actualPayment = totalAvailable;
                                    advanceBalance = 0;
                                    status = "Partial";
                                }
                            }
                            else
                            {
                                if (advanceBalance > 0)
                                {
                                    double appliedAdvance = Math.Min(advanceBalance, monthlyPayment);
                                    actualPayment = Math.Round(monthlyPayment - appliedAdvance, 2);
                                    advanceBalance = Math.Max(0, advanceBalance - (monthlyPayment - appliedAdvance));
                                    status = "Advance Applied";
                                }
                                else
                                {
                                    status = "Unpaid";
                                    unpaidDates.Add(dueDate.ToString("yyyy-MM-dd"));
                                }
                            }
                        }

                        scheduleList.Add(new PaymentScheduleItem
                        {
                            DueDate = dueDate.ToString("yyyy-MM-dd"),
                            PaymentDate = paymentDate?.ToString("yyyy-MM-dd"),
                            AmountPaid = amountPaid,
                            RequiredToPayEveryMonth = monthlyPayment,
                            ActualPayment = Math.Round(actualPayment, 2),
                            Status = status
                        });
                    }

                    // 🔹 Handle extra advance payments (beyond all due dates)
                    while (remainingPayments.Count > 0)
                    {
                        var extra = remainingPayments.Dequeue();
                        scheduleList.Add(new PaymentScheduleItem
                        {
                            DueDate = scheduleDates.LastOrDefault().AddMonths(1).ToString("yyyy-MM-dd"),
                            PaymentDate = extra.date.ToString("yyyy-MM-dd"),
                            AmountPaid = extra.amount,
                            RequiredToPayEveryMonth = monthlyPayment,
                            ActualPayment = extra.amount,
                            Status = "Advance"
                        });
                    }
                }

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
                        Schedule = scheduleList
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