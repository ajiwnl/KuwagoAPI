using Google.Cloud.Firestore;
using KuwagoAPI.DTO;
using KuwagoAPI.Helper;
using KuwagoAPI.Models;
using static KuwagoAPI.Helper.LoanEnums;

namespace KuwagoAPI.Services
{
    public class LoanService
    {
        private readonly FirestoreDb _firestoreDb;
        private readonly CreditScoreService _creditScoreService;


        public LoanService(FirestoreDb firestoreDb, CreditScoreService creditScoreService)
        {
            _firestoreDb = firestoreDb;
            _creditScoreService = creditScoreService;
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
                    CreatedAt = Timestamp.FromDateTime(DateTime.UtcNow),
                    LoanStatus = LoanStatus.Pending.ToString()

                };

                var docRef = _firestoreDb.Collection("LoanRequests").Document();
                loan.LoanRequestID = docRef.Id; 
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
                            loan.LoanRequestID,
                            loan.UID,
                            loan.MaritalStatus,
                            loan.HighestEducation,
                            loan.EmploymentInformation,
                            loan.DetailedAddress,
                            loan.ResidentType,
                            loan.LoanType,
                            loan.LoanAmount,
                            loan.LoanPurpose,
                            loan.LoanStatus,
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

        public async Task<StatusResponse> GetLoanRequestsByUIDAsync(string uid)
        {
            try
            {
                // Fetch user document
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

                // Query loan requests for this UID
                var loanQuery = _firestoreDb.Collection("LoanRequests").WhereEqualTo("UID", uid);
                var loanSnapshots = await loanQuery.GetSnapshotAsync();

                var loans = loanSnapshots.Documents.Select(doc =>
                {
                    var loan = doc.ConvertTo<mLoans>();
                    return new
                    {
                        loan.LoanRequestID,
                        loan.UID,
                        loan.MaritalStatus,
                        loan.HighestEducation,
                        loan.EmploymentInformation,
                        loan.DetailedAddress,
                        loan.ResidentType,
                        loan.LoanType,
                        loan.LoanAmount,
                        loan.LoanPurpose,
                        loan.LoanStatus,
                        CreatedAt = loan.CreatedAt.ToDateTime().ToString("yyyy-MM-dd HH:mm:ss")
                    };
                }).ToList();

                return new StatusResponse
                {
                    Success = true,
                    Message = "Loan requests retrieved successfully.",
                    StatusCode = 200,
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
                        Loans = loans
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

        public async Task<StatusResponse> FilterLoanRequestsAsync(string uid, LoanFilterDTO filter)
        {
            try
            {
                var userDoc = await _firestoreDb.Collection("Users").Document(uid).GetSnapshotAsync();
                if (!userDoc.Exists)
                {
                    return new StatusResponse
                    {
                        Success = false,
                        Message = "User not found.",
                        StatusCode = 404
                    };
                }

                var user = userDoc.ConvertTo<mUser>();

                Query query = _firestoreDb.Collection("LoanRequests").WhereEqualTo("UID", uid);

                if (filter.LoanStatus.HasValue)
                    query = query.WhereEqualTo("LoanStatus", filter.LoanStatus.Value.ToString());

                if (filter.LoanType.HasValue)
                    query = query.WhereEqualTo("LoanType", filter.LoanType.Value.ToString());

                var snapshot = await query.GetSnapshotAsync();

                var loans = snapshot.Documents.Select(doc =>
                {
                    var loan = doc.ConvertTo<mLoans>();
                    return new
                    {
                        loan.LoanRequestID,
                        loan.UID,
                        loan.MaritalStatus,
                        loan.HighestEducation,
                        loan.EmploymentInformation,
                        loan.DetailedAddress,
                        loan.ResidentType,
                        loan.LoanType,
                        loan.LoanAmount,
                        loan.LoanPurpose,
                        loan.LoanStatus,
                        CreatedAt = loan.CreatedAt.ToDateTime().ToString("yyyy-MM-dd HH:mm:ss")
                    };
                }).ToList();

                return new StatusResponse
                {
                    Success = true,
                    Message = "Filtered loan requests retrieved successfully.",
                    StatusCode = 200,
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
                        Loans = loans
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

        public async Task<StatusResponse> GetAllLoanRequestsAsync()
        {
            try
            {
                var snapshot = await _firestoreDb.Collection("LoanRequests").GetSnapshotAsync();

                if (snapshot.Count == 0)
                {
                    return new StatusResponse
                    {
                        Success = true,
                        Message = "No loan requests found.",
                        StatusCode = 200,
                        Data = new List<object>()
                    };
                }

                var loanList = new List<object>();

                foreach (var doc in snapshot.Documents)
                {
                    var loan = doc.ConvertTo<mLoans>();

                    // Get user info
                    var userSnapshot = await _firestoreDb.Collection("Users").Document(loan.UID).GetSnapshotAsync();
                    var user = userSnapshot.Exists ? userSnapshot.ConvertTo<mUser>() : null;

                    loanList.Add(new
                    {
                        LoanInfo = new
                        {
                            loan.LoanRequestID,
                            loan.UID,
                            loan.MaritalStatus,
                            loan.HighestEducation,
                            loan.EmploymentInformation,
                            loan.DetailedAddress,
                            loan.ResidentType,
                            loan.LoanType,
                            loan.LoanAmount,
                            loan.LoanPurpose,
                            loan.LoanStatus,
                            CreatedAt = loan.CreatedAt.ToDateTime().ToString("yyyy-MM-dd HH:mm:ss")
                        },
                        UserInfo = user == null ? null : new
                        {
                            user.UID,
                            user.FirstName,
                            user.LastName,
                            user.Email,
                            user.Username,
                            user.PhoneNumber
                        }
                    });
                }

                return new StatusResponse
                {
                    Success = true,
                    Message = "All loan requests retrieved successfully.",
                    StatusCode = 200,
                    Data = loanList
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

        public async Task<StatusResponse> FilterLoanRequestsAsync(LoanFilterDTOv2 filter)
        {
            try
            {
                var loanSnapshots = await _firestoreDb.Collection("LoanRequests").GetSnapshotAsync();
                var loanDocs = loanSnapshots.Documents;

                var result = new List<object>();

                foreach (var loanDoc in loanDocs)
                {
                    var loan = loanDoc.ConvertTo<mLoans>();

                    // Load related user
                    var userSnapshot = await _firestoreDb.Collection("Users").Document(loan.UID).GetSnapshotAsync();
                    if (!userSnapshot.Exists) continue;

                    var user = userSnapshot.ConvertTo<mUser>();

                    // Filters
                    if (!string.IsNullOrWhiteSpace(filter.FirstName) &&
                        !user.FirstName.Contains(filter.FirstName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!string.IsNullOrWhiteSpace(filter.LastName) &&
                        !user.LastName.Contains(filter.LastName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!string.IsNullOrWhiteSpace(filter.Email) &&
                        !user.Email.Equals(filter.Email, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!string.IsNullOrWhiteSpace(filter.LoanStatus) &&
                        !loan.LoanStatus.Equals(filter.LoanStatus, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (filter.CreatedAfter.HasValue &&
                        loan.CreatedAt.ToDateTime() < filter.CreatedAfter.Value)
                        continue;

                    if (filter.CreatedBefore.HasValue &&
                        loan.CreatedAt.ToDateTime() > filter.CreatedBefore.Value)
                        continue;

                    result.Add(new
                    {
                        LoanInfo = new
                        {
                            loan.LoanRequestID,
                            loan.UID,
                            loan.MaritalStatus,
                            loan.HighestEducation,
                            loan.EmploymentInformation,
                            loan.DetailedAddress,
                            loan.ResidentType,
                            loan.LoanType,
                            loan.LoanAmount,
                            loan.LoanPurpose,
                            loan.LoanStatus,
                            CreatedAt = loan.CreatedAt.ToDateTime().ToString("yyyy-MM-dd HH:mm:ss")
                        },
                        UserInfo = new
                        {
                            user.UID,
                            user.FirstName,
                            user.LastName,
                            user.Email,
                            user.Username,
                            user.PhoneNumber
                        }
                    });
                }

                return new StatusResponse
                {
                    Success = true,
                    Message = "Filtered loan requests retrieved successfully.",
                    StatusCode = 200,
                    Data = result
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

        public async Task<StatusResponse> ProcessLoanAgreementAsync(LoanAgreementDTO dto, string lenderUid)
        {
            try
            {
                // Basic validations
                if (string.IsNullOrWhiteSpace(dto.LoanRequestID))
                    return new StatusResponse { Success = false, Message = "LoanRequestID is required.", StatusCode = 400 };

                if (string.IsNullOrWhiteSpace(dto.UpdatedLoanStatus))
                    return new StatusResponse { Success = false, Message = "UpdatedLoanStatus is required.", StatusCode = 400 };

                if (!Enum.TryParse<LoanStatus>(dto.UpdatedLoanStatus, true, out var parsedStatus))
                    return new StatusResponse
                    {
                        Success = false,
                        Message = "Invalid loan status provided. Allowed values: Pending, Active, Denied, InProgress, Completed.",
                        StatusCode = 400
                    };

                if (dto.UpdatedLoanAmount < 0)
                    return new StatusResponse { Success = false, Message = "Loan amount cannot be negative.", StatusCode = 400 };

                if (dto.InterestRate < 0 || dto.InterestRate > 100)
                    return new StatusResponse { Success = false, Message = "Interest rate must be between 0 and 100.", StatusCode = 400 };

                if (string.IsNullOrWhiteSpace(lenderUid))
                    return new StatusResponse { Success = false, Message = "Lender UID is missing or invalid.", StatusCode = 401 };

                if (parsedStatus == LoanStatus.Approved)
                {
                    if (!Enum.IsDefined(typeof(TermsOfMonths), dto.TermsOfMonths))
                        return new StatusResponse { Success = false, Message = "Invalid TermsOfMonths provided.", StatusCode = 400 };

                    if (!Enum.IsDefined(typeof(PaymentType), dto.PaymentType))
                        return new StatusResponse { Success = false, Message = "Invalid PaymentType provided.", StatusCode = 400 };
                }

                // Get loan request
                var loanDocRef = _firestoreDb.Collection("LoanRequests").Document(dto.LoanRequestID);
                var loanSnapshot = await loanDocRef.GetSnapshotAsync();
                if (!loanSnapshot.Exists)
                    return new StatusResponse { Success = false, Message = "Loan request not found.", StatusCode = 404 };

                var loan = loanSnapshot.ConvertTo<mLoans>();

                // Get borrower
                var borrowerSnapshot = await _firestoreDb.Collection("Users").Document(loan.UID).GetSnapshotAsync();
                if (!borrowerSnapshot.Exists)
                    return new StatusResponse { Success = false, Message = "Borrower not found.", StatusCode = 404 };
                var borrower = borrowerSnapshot.ConvertTo<mUser>();

                // Get lender
                var lenderSnapshot = await _firestoreDb.Collection("Users").Document(lenderUid).GetSnapshotAsync();
                var lender = lenderSnapshot.Exists ? lenderSnapshot.ConvertTo<mUser>() : null;

                // Create AgreedLoan
                var agreedLoanDocRef = _firestoreDb.Collection("AgreedLoans").Document();
                var agreedLoanId = agreedLoanDocRef.Id;
                await agreedLoanDocRef.SetAsync(new
                {
                    AgreedLoanID = agreedLoanId,
                    LenderUID = lenderUid,
                    BorrowerUID = loan.UID,
                    InterestRate = dto.InterestRate,
                    LoanRequestID = dto.LoanRequestID,
                    AgreementDate = Timestamp.FromDateTime(DateTime.UtcNow),
                    TermsOfMonths = parsedStatus == LoanStatus.Approved ? dto.TermsOfMonths.ToString() : null,
                    PaymentType = parsedStatus == LoanStatus.Approved ? dto.PaymentType.ToString() : null
                });

                if (!loanSnapshot.ContainsField("AgreementDate") || loanSnapshot.GetValue<Timestamp>("AgreementDate") == null)
                {
                    Dictionary<string, object> loanUpdates = new()
    {
        { "LoanStatus", parsedStatus.ToString() },
        { "LoanAmount", dto.UpdatedLoanAmount },
        { "AgreementDate", Timestamp.FromDateTime(DateTime.UtcNow) }
    };
                    await loanDocRef.UpdateAsync(loanUpdates);
                }
                else
                {
                    Console.WriteLine("AgreementDate already exists, skipping update on LoanRequests.");
                }


                // Create Payables if approved
                List<Timestamp> paymentScheduleDates = new();
                DocumentReference payableDocRef = null;
                double totalPayable = 0;

                if (parsedStatus == LoanStatus.Approved)
                {
                    var existingPayables = await _firestoreDb.Collection("Payables")
                        .WhereEqualTo("LoanRequestID", dto.LoanRequestID)
                        .GetSnapshotAsync();

                    if (existingPayables.Count == 0)
                    {
                        var startDate = DateTime.UtcNow.Date.AddMonths(1);
                        for (int i = 0; i < (int)dto.TermsOfMonths; i++)
                            paymentScheduleDates.Add(Timestamp.FromDateTime(startDate.AddMonths(i)));

                        totalPayable = dto.UpdatedLoanAmount + (dto.UpdatedLoanAmount * (dto.InterestRate / 100.0));

                        payableDocRef = _firestoreDb.Collection("Payables").Document();
                        await payableDocRef.SetAsync(new
                        {
                            PayableID = payableDocRef.Id,
                            LoanRequestID = dto.LoanRequestID,
                            BorrowerUID = loan.UID,
                            LenderUID = lenderUid,
                            PaymentType = dto.PaymentType.ToString(),
                            TermsOfMonths = dto.TermsOfMonths.ToString(),
                            TotalPayableAmount = totalPayable,
                            PaymentSchedule = paymentScheduleDates,
                            CreatedAt = Timestamp.FromDateTime(DateTime.UtcNow)
                        });
                    }
                }

                // Return
                return new StatusResponse
                {
                    Success = true,
                    Message = parsedStatus == LoanStatus.Approved
                        ? "Loan approved and agreement created successfully."
                        : "Loan status updated successfully.",
                    StatusCode = 200,
                    Data = parsedStatus == LoanStatus.Approved ? new
                    {
                        AgreedLoanID = agreedLoanId,
                        Terms = dto.TermsOfMonths.ToString(),
                        PaymentType = dto.PaymentType.ToString(),
                        TotalPayableAmount = totalPayable,
                        PayableID = payableDocRef?.Id,
                        PaymentSchedule = paymentScheduleDates.Select(ts => ts.ToDateTime().ToString("yyyy-MM-dd")).ToList(),
                        BorrowerUID = loan.UID,
                        LenderUID = lenderUid
                    } : null
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


        public async Task<StatusResponse> FilterAgreedLoansAsync(AgreedLoanFilterDTO filter)
        {
            try
            {
                var snapshot = await _firestoreDb.Collection("AgreedLoans").GetSnapshotAsync();
                var result = new List<object>();

                foreach (var doc in snapshot.Documents)
                {
                    var agreedLoan = doc.ToDictionary();

                    // Extract base fields
                    string agreedLoanId = agreedLoan["AgreedLoanID"].ToString();
                    string borrowerUid = agreedLoan["BorrowerUID"].ToString();
                    string lenderUid = agreedLoan["LenderUID"].ToString();
                    double interestRate = Convert.ToDouble(agreedLoan["InterestRate"]);
                    DateTime agreementDate = ((Timestamp)agreedLoan["AgreementDate"]).ToDateTime();

                    // Apply UID & AgreedLoanID filters first
                    if (!string.IsNullOrWhiteSpace(filter.AgreedLoanID) &&
                        !string.Equals(agreedLoanId, filter.AgreedLoanID, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!string.IsNullOrWhiteSpace(filter.BorrowerUID) &&
                        !string.Equals(borrowerUid, filter.BorrowerUID, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!string.IsNullOrWhiteSpace(filter.LenderUID) &&
                        !string.Equals(lenderUid, filter.LenderUID, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Retrieve borrower
                    var borrowerSnapshot = await _firestoreDb.Collection("Users").Document(borrowerUid).GetSnapshotAsync();
                    if (!borrowerSnapshot.Exists) continue;
                    var borrower = borrowerSnapshot.ConvertTo<mUser>();

                    // Retrieve lender
                    var lenderSnapshot = await _firestoreDb.Collection("Users").Document(lenderUid).GetSnapshotAsync();
                    if (!lenderSnapshot.Exists) continue;
                    var lender = lenderSnapshot.ConvertTo<mUser>();

                    // Apply name and date filters
                    if (!string.IsNullOrWhiteSpace(filter.BorrowerFirstName) &&
                        !borrower.FirstName.Contains(filter.BorrowerFirstName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!string.IsNullOrWhiteSpace(filter.BorrowerLastName) &&
                        !borrower.LastName.Contains(filter.BorrowerLastName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!string.IsNullOrWhiteSpace(filter.LenderFirstName) &&
                        !lender.FirstName.Contains(filter.LenderFirstName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!string.IsNullOrWhiteSpace(filter.LenderLastName) &&
                        !lender.LastName.Contains(filter.LenderLastName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (filter.MinInterestRate.HasValue && interestRate < filter.MinInterestRate.Value) continue;
                    if (filter.MaxInterestRate.HasValue && interestRate > filter.MaxInterestRate.Value) continue;

                    if (filter.AgreementDateAfter.HasValue && agreementDate < filter.AgreementDateAfter.Value) continue;
                    if (filter.AgreementDateBefore.HasValue && agreementDate > filter.AgreementDateBefore.Value) continue;

                    // Retrieve LoanRequestID from AgreedLoan (must be saved there when created)
                    var loanRequestId = agreedLoan.ContainsKey("LoanRequestID") ? agreedLoan["LoanRequestID"].ToString() : null;
                    if (string.IsNullOrWhiteSpace(loanRequestId))
                        continue;

                    var loanSnapshot = await _firestoreDb.Collection("LoanRequests").Document(loanRequestId).GetSnapshotAsync();
                    if (!loanSnapshot.Exists)
                        continue;

                    var loan = loanSnapshot.ConvertTo<mLoans>();

                    result.Add(new
                    {
                        AgreedLoanID = agreedLoanId,
                        InterestRate = interestRate,
                        AgreementDate = agreementDate.ToString("yyyy-MM-dd HH:mm:ss"),
                        LenderInfo = new
                        {
                            lender.UID,
                            lender.FirstName,
                            lender.LastName,
                            lender.Email
                        },
                        BorrowerInfo = new
                        {
                            borrower.UID,
                            borrower.FirstName,
                            borrower.LastName,
                            borrower.Email
                        },
                        UpdatedLoanInfo = new
                        {
                            loan.LoanRequestID,
                            loan.UID,
                            loan.MaritalStatus,
                            loan.HighestEducation,
                            loan.EmploymentInformation,
                            loan.DetailedAddress,
                            loan.ResidentType,
                            loan.LoanType,
                            loan.LoanAmount,
                            loan.LoanPurpose,
                            loan.LoanStatus,
                            CreatedAt = loan.CreatedAt.ToDateTime().ToString("yyyy-MM-dd HH:mm:ss")
                        }
                    });

                }

                return new StatusResponse
                {
                    Success = true,
                    Message = "Filtered agreed loans retrieved successfully.",
                    StatusCode = 200,
                    Data = result
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
