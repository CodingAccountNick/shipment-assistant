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
        // What we send to the Python service (separate from the public API's AskRequest)
        private record AiAskRequest(string Question, string? Context);

        // Existing callers (like the demo workers) keep working unchanged
        public Task<string> AskAsync(string question, CancellationToken ct) =>
            AskAsync(question, null, ct);

        public async Task<string> AskAsync(string question, string? context, CancellationToken ct)
        {
            var response = await http.PostAsJsonAsync("/ask", new AiAskRequest(question, context), ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AskResponse>(ct);
            return result?.Answer ?? throw new InvalidOperationException("Empty response from AI service");
        }
    }
}
