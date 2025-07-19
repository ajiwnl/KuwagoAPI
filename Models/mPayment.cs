using Google.Cloud.Firestore;

namespace KuwagoAPI.Models
{
    [FirestoreData]
    public class mPayment
    {
        [FirestoreProperty] public string PaymentID { get; set; }
        [FirestoreProperty] public string PayableID { get; set; }
        [FirestoreProperty] public string BorrowerUID { get; set; }
        [FirestoreProperty] public double AmountPaid { get; set; }
        [FirestoreProperty] public Timestamp PaymentDate { get; set; }
        [FirestoreProperty] public string Notes { get; set; }
    }

}
