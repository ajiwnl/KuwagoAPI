using Google.Cloud.Firestore;

namespace KuwagoAPI.Models
{
    [FirestoreData]
    public class mUser
    {
        [FirestoreProperty]
        public string FirstName { get; set; }

        [FirestoreProperty]
        public string LastName { get; set; }

        [FirestoreProperty]
        public string PhoneNumber { get; set; }

        [FirestoreProperty]
        public string Email { get; set; }

        [FirestoreProperty]
        public string ProfilePicture { get; set; }

        [FirestoreProperty]
        public string Username { get; set; }

        [FirestoreProperty]
        public string Password { get; set; }

        [FirestoreProperty]
        public string UID { get; set; }

        [FirestoreProperty]
        public Timestamp createdAt { get; set; }

        [FirestoreProperty]
        public string Role { get; set; }
    }
}
