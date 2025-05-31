using Google.Cloud.Firestore;
using KuwagoAPI.Models;

namespace KuwagoAPI.Services
{
    public class FirestoreService
    {
        private readonly FirestoreDb _firestoreDb;

        public FirestoreService(FirestoreDb firestoreDb)
        {
            _firestoreDb = firestoreDb;
        }

        public async Task<mSample?> GetSampleByIdAsync(string id)
        {
            DocumentReference docRef = _firestoreDb.Collection("Sample").Document(id);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

            if (snapshot.Exists)
            {
                var sample = snapshot.ConvertTo<mSample>();
                return sample;
            }

            return null;
        }
    }
}
