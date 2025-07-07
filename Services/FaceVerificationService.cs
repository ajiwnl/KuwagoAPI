using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using CloudinaryDotNet;
using HttpMethod = System.Net.Http.HttpMethod;

namespace KuwagoAPI.Services
{
    public class FaceVerificationService
    {
        private const string ApiKey = "q24S37lDQlT4LQpK1fGlwi57SuGhH32O";
        private const string ApiSecret = "cseJ7Jfl91a3RmKD5nbs312vuM-mNPPh";
        private const string FaceppUrl = "https://api-us.faceplusplus.com/facepp/v3/compare";

        private readonly HttpClient _httpClient;

        public FaceVerificationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<object> VerifyFaceMatchAsync(string linkFile1, string linkFile2)
        {
            Console.WriteLine($"Face++ Compare: image_url1={linkFile1}, image_url2={linkFile2}");

            var formData = new Dictionary<string, string>
            {
                 { "api_key", ApiKey },
                 { "api_secret", ApiSecret },
                 { "image_url1", linkFile1 },
                 { "image_url2", linkFile2 }
            };
            var request = new HttpRequestMessage(HttpMethod.Post, FaceppUrl)
            {
                 Content = new FormUrlEncodedContent(formData)
            };
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*"));

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"Face++ response: {response.StatusCode} | Body: {responseBody}");

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Face++ API error {response.StatusCode}: {responseBody}");
            }

            using var jsonDoc = JsonDocument.Parse(responseBody);
            var confidence = jsonDoc.RootElement.GetProperty("confidence").GetDouble();

            return new
            {
                confidence,
                isMatch = confidence >= 80.0
            };
        }
    }
}
