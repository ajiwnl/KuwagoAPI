using Google.Cloud.Firestore;
using KuwagoAPI.Models;

namespace KuwagoAPI.Services
{
    public class IdentityVerificationService
    {
        private readonly FirestoreDb _firestoreDb;

        public IdentityVerificationService(FirestoreDb firestoreDb)
        {
            _firestoreDb = firestoreDb;
        }
        public async Task<bool> IDAlreadyUploadedAsync(string uid)
        {
            var docRef = _firestoreDb.Collection("ID Photos").Document(uid);
            var snapshot = await docRef.GetSnapshotAsync();
            return snapshot.Exists;
        }

        public async Task UploadIDAsync(string uid, string imageUrl)
        {
            var docRef = _firestoreDb.Collection("ID Photos").Document(uid);
            var data = new mIdentityVerification
            {
                UID = uid,
                IDUrl = imageUrl,
                UploadedAt = Timestamp.FromDateTime(DateTime.UtcNow)
            };

            await docRef.SetAsync(data);
        }
    }
}
