using Google.Cloud.Firestore;

namespace KuwagoAPI.Models
{

    [FirestoreData]
    public class mCreditScores
    {
        [FirestoreProperty]
        public string UID { get; set; }
        [FirestoreProperty]
        public int Score { get; set; }          
        [FirestoreProperty]
        public int TotalLoans { get; set; }     
        [FirestoreProperty]
        public int SuccessfulRepayments { get; set; }
        [FirestoreProperty]
        public int MissedRepayments { get; set; }
        [FirestoreProperty]
        public Timestamp LastUpdated { get; set; } 
    }
}
