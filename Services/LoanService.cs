using Google.Cloud.Firestore;
using KuwagoAPI.DTO;
using KuwagoAPI.Helper;
using KuwagoAPI.Models;

namespace KuwagoAPI.Services
{
    public class LoanService
    {
        private readonly FirestoreDb _firestoreDb;

        public LoanService(FirestoreDb firestoreDb)
        {
            _firestoreDb = firestoreDb;
        }

        public async Task<StatusResponse> CreateLoanRequestAsync(string uid, LoanDTO dto)
        {
            try
            {
                // Validate fields manually (if needed beyond data annotations)
                if (string.IsNullOrWhiteSpace(dto.EmploymentInformation) ||
                    string.IsNullOrWhiteSpace(dto.DetailedAddress) ||
                    string.IsNullOrWhiteSpace(dto.LoanPurpose))
                {
                    return new StatusResponse
                    {
                        Success = false,
                        Message = "One or more required fields are missing or invalid.",
                        StatusCode = 400
                    };
                }

                // Get user info from Firestore
                var userSnapshot = await _firestoreDb.Collection("Users").Document(uid).GetSnapshotAsync();
                if (!userSnapshot.Exists)
                {
                    return new StatusResponse
                    {
                        Success = false,
                        Message = "User not found.",
                        StatusCode = 404
                    };
                }

                var user = userSnapshot.ConvertTo<mUser>();

                // Prepare loan request
                var loan = new mLoans
                {
                    UID = uid,
                    MaritalStatus = dto.MaritalStatus.ToString(),
                    HighestEducation = dto.HighestEducation.ToString(),
                    EmploymentInformation = dto.EmploymentInformation,
                    DetailedAddress = dto.DetailedAddress,
                    ResidentType = dto.ResidentType.ToString(),
                    LoanType = dto.LoanType.ToString(),
                    LoanAmount = (int)dto.LoanAmount,
                    LoanPurpose = dto.LoanPurpose,
                    CreatedAt = Timestamp.FromDateTime(DateTime.UtcNow)
                };

                var docRef = _firestoreDb.Collection("LoanRequests").Document();
                await docRef.SetAsync(loan);

                return new StatusResponse
                {
                    Success = true,
                    Message = "Loan request submitted successfully.",
                    StatusCode = 201,
                    Data = new
                    {
                        UserInfo = new
                        {
                            user.UID,
                            user.FirstName,
                            user.LastName,
                            user.Email,
                            user.Username,
                            user.PhoneNumber
                        },
                        LoanInfo = new
                        {
                            loan.UID,
                            loan.MaritalStatus,
                            loan.HighestEducation,
                            loan.EmploymentInformation,
                            loan.DetailedAddress,
                            loan.ResidentType,
                            loan.LoanType,
                            loan.LoanAmount,
                            loan.LoanPurpose,
                            CreatedAt = loan.CreatedAt.ToDateTime().ToString("yyyy-MM-dd HH:mm:ss")
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                return new StatusResponse
                {
                    Success = false,
                    Message = $"An unexpected error occurred: {ex.Message}",
                    StatusCode = 500
                };
            }
        }
    }
}
