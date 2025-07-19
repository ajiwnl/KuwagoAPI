using Google.Cloud.Firestore;
using KuwagoAPI.DTO;
using KuwagoAPI.Helper;
using KuwagoAPI.Models;

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
                // Validate amount
                if (dto.AmountPaid <= 0)
                    return new StatusResponse
                    {
                        Success = false,
                        Message = "AmountPaid must be greater than 0.",
                        StatusCode = 400
                    };

                // Validate Payable exists
                var payableRef = _firestoreDb.Collection("Payables").Document(dto.PayableID);
                var payableSnapshot = await payableRef.GetSnapshotAsync();

                if (!payableSnapshot.Exists)
                    return new StatusResponse { Success = false, Message = "Payable not found.", StatusCode = 404 };

                var payable = payableSnapshot.ToDictionary();
                string borrowerUID = payable["BorrowerUID"]?.ToString();

                // Validate borrower UID matches
                if (dto.BorrowerUID != borrowerUID)
                    return new StatusResponse
                    {
                        Success = false,
                        Message = "Borrower UID does not match the payable record.",
                        StatusCode = 403
                    };

                // Get total payable amount
                double totalPayable = Convert.ToDouble(payable["TotalPayableAmount"]);

                // Get existing payments for this payable
                var existingPaymentsSnapshot = await _firestoreDb.Collection("Payments")
                    .WhereEqualTo("PayableID", dto.PayableID)
                    .GetSnapshotAsync();

                double totalPaid = existingPaymentsSnapshot.Documents
                    .Sum(p => Convert.ToDouble(p.GetValue<double>("AmountPaid")));

                double remainingBalance = totalPayable - totalPaid;

                // Validate if payment exceeds remaining
                if (dto.AmountPaid > remainingBalance)
                    return new StatusResponse
                    {
                        Success = false,
                        Message = $"Payment exceeds the remaining balance. Remaining: {remainingBalance:F2}",
                        StatusCode = 400
                    };

                // Submit Payment
                var paymentRef = _firestoreDb.Collection("Payments").Document();
                var payment = new
                {
                    PaymentID = paymentRef.Id,
                    PayableID = dto.PayableID,
                    BorrowerUID = dto.BorrowerUID,
                    AmountPaid = dto.AmountPaid,
                    PaymentDate = Timestamp.FromDateTime(dto.PaymentDate),
                    Notes = dto.Notes ?? string.Empty
                };

                await paymentRef.SetAsync(payment);

                return new StatusResponse
                {
                    Success = true,
                    Message = "Payment submitted successfully.",
                    StatusCode = 200,
                    Data = payment
                };
            }
            catch (Exception ex)
            {
                return new StatusResponse
                {
                    Success = false,
                    Message = $"Error submitting payment: {ex.Message}",
                    StatusCode = 500
                };
            }
        }


        public async Task<List<mPayment>> GetPaymentsByBorrower(string borrowerUid)
        {
            var query = await _firestoreDb.Collection("Payments")
                .WhereEqualTo("BorrowerUID", borrowerUid)
                .OrderBy("PaymentDate")
                .GetSnapshotAsync();

            return query.Documents.Select(doc => doc.ConvertTo<mPayment>()).ToList();
        }

        public async Task<List<mPayment>> GetPaymentsByPayable(string payableId)
        {
            var query = await _firestoreDb.Collection("Payments")
                .WhereEqualTo("PayableID", payableId)
                .OrderBy("PaymentDate")
                .GetSnapshotAsync();

            return query.Documents.Select(doc => doc.ConvertTo<mPayment>()).ToList();
        }

        public async Task<StatusResponse> GetPaymentSummary(string payableId)
        {
            var payments = await GetPaymentsByPayable(payableId);
            var totalPaid = payments.Sum(p => p.AmountPaid);

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
