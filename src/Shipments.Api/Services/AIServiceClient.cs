using System.ComponentModel;
namespace Shipments.Api.Services
{
    public record AskRequest(
        [property: DefaultValue("What does 'out for delivery' mean?")]
        string Question
    );
    public record AskResponse(string Answer);

    public class AiServiceClient(HttpClient http)
    {
        public async Task<string> AskAsync(string question, CancellationToken ct)
        {
            var response = await http.PostAsJsonAsync("/ask", new AskRequest(question), ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<AskResponse>(ct);
            return result?.Answer ?? throw new InvalidOperationException("Empty response from AI service");
        }
    }
}
