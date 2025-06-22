using Google.Cloud.Firestore;
using static KuwagoAPI.Helper.LoanEnums;

namespace KuwagoAPI.Models
{
    [FirestoreData]
    public class mLoans
    {
        [FirestoreProperty]
        public string LoanRequestID { get; set; }

        [FirestoreProperty]
        public string UID { get; set; }

        [FirestoreProperty]
        public string MaritalStatus { get; set; }

        [FirestoreProperty]
        public string HighestEducation { get; set; }

        [FirestoreProperty]
        public string EmploymentInformation { get; set; }

        [FirestoreProperty]
        public string DetailedAddress { get; set; }

        [FirestoreProperty]
        public string ResidentType { get; set; }

        [FirestoreProperty]
        public string LoanType { get; set; }

        [FirestoreProperty]
        public int LoanAmount { get; set; }

        [FirestoreProperty]
        public string LoanPurpose { get; set; }

        [FirestoreProperty]
        public string LoanStatus { get; set; }

        [FirestoreProperty]
        public Timestamp CreatedAt { get; set; } = Timestamp.FromDateTime(DateTime.UtcNow);

        [FirestoreProperty]
        public Timestamp? AgreementDate { get; set; }

    }

}
