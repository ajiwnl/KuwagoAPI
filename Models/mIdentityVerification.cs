using Google.Cloud.Firestore;

namespace KuwagoAPI.Models
{
   [FirestoreData]
public class mIdentityVerification
{
    [FirestoreProperty]
    public string UID { get; set; }

    [FirestoreProperty]
    public string IDUrl { get; set; }

    [FirestoreProperty]
    public string SelfieUrl { get; set; }  // <- Add this

    [FirestoreProperty]
    public Timestamp UploadedAt { get; set; }
}

}
