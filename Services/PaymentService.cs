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

        public PaymentService(FirestoreDb firestoreDb)
        {
            _firestoreDb = firestoreDb;
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
                        PaymentDate = paymentDateString

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
