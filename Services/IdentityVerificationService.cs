﻿using Google.Cloud.Firestore;
using KuwagoAPI.Helper;
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

        public async Task UploadIDPhotoAsync(string uid, string idUrl, string selfieUrl)
        {
            var docRef = _firestoreDb.Collection("ID Photos").Document(uid);
            var data = new mIdentityVerification
            {
                UID = uid,
                IDUrl = idUrl,
                SelfieUrl = selfieUrl,
                UploadedAt = Timestamp.FromDateTime(DateTime.UtcNow)
            };

            await docRef.SetAsync(data);
        }

        public async Task<mIdentityVerification> GetIdentityVerificationAsync(string uid)
        {
            var docRef = _firestoreDb.Collection("ID Photos").Document(uid);
            var snapshot = await docRef.GetSnapshotAsync();

            if (!snapshot.Exists) return null;

            return snapshot.ConvertTo<mIdentityVerification>();
        }

        public async Task UpdateVerificationResultAsync(string uid, double confidence, VerificationStatus verifyStatus)
        {
            var docRef = _firestoreDb.Collection("ID Photos").Document(uid);

            var updates = new Dictionary<string, object>
    {
        { "ConfidenceLevel", confidence },
        { "VerifyStatus", (int)verifyStatus }, // save as int to Firestore
        { "VerifiedAt", Timestamp.FromDateTime(DateTime.UtcNow) }
    };

            await docRef.UpdateAsync(updates);
        }



    }
}
