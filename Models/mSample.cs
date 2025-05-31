using Google.Cloud.Firestore;

namespace KuwagoAPI.Models
{

    [FirestoreData]
    public class mSample
    {
        [FirestoreProperty]
        public string Name { get; set; }
        [FirestoreProperty]
        public string Role { get; set; }
    }
}
