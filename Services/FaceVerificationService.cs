using System.Net.Http.Headers;
using System.Text.Json;

namespace KuwagoAPI.Services
{
    public class FaceVerificationService
    {
        private readonly HttpClient _httpClient;

        public FaceVerificationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> VerifyFaceMatchAsync(string linkFile1, string linkFile2)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://face-verification2.p.rapidapi.com/faceverification"),
                Headers =
            {
                { "x-rapidapi-key", "2fabcce341mshc71b04007818aabp1f236fjsn2fb4067385ae" },
                { "x-rapidapi-host", "face-verification2.p.rapidapi.com" },
            },
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "linkFile1", linkFile1 },
                { "linkFile2", linkFile2 },
            })
            };

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}
