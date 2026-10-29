using System.Collections.Concurrent;
using System.Diagnostics;

namespace Shipments.Api.Services
{
    public record DemoJob(
        int Id,
        string Question,
        int DelaySeconds,
        string Status,
        int StartThreadId,
        int? EndThreadId = null,
        double? AnsweredAtSeconds = null,
        string? Answer = null);

    // Fire-and-forget workers: each question runs on its own and nobody waits for the others.
    // Registered as a singleton so the workers keep running after the HTTP request that started them has returned.
    public class DemoWorker(IServiceScopeFactory scopes, IHostApplicationLifetime lifetime, ILogger<DemoWorker> logger)
    {
        private static readonly (string Question, int DelaySeconds)[] Questions =
        [
            ("Where is shipment SH-1001?", 20),
            ("When will shipment SH-1002 arrive?", 10),
            ("Why is shipment SH-1003 delayed?", 5),
        ];

        private readonly ConcurrentDictionary<int, DemoJob> jobs = new();
        private Stopwatch clock = new();

        public IReadOnlyList<DemoJob> Jobs => jobs.Values.OrderBy(j => j.Id).ToList();

        public IReadOnlyList<DemoJob> Start()
        {
            jobs.Clear();
            clock = Stopwatch.StartNew();

            for (int i = 0; i < Questions.Length; i++)
            {
                int id = i + 1; // capture a copy, not the loop variable
                var (question, delay) = Questions[i];
                _ = Task.Run(() => DoWork(id, question, delay));
            }

            return Jobs;
        }

        private async Task DoWork(int id, string question, int delaySeconds)
        {
            var ct = lifetime.ApplicationStopping;
            var startThread = Environment.CurrentManagedThreadId;
            jobs[id] = new DemoJob(id, question, delaySeconds, "Waiting", startThread);
            logger.LogInformation("[{Time,5:F1}s] Worker {Id} started, waiting {Delay}s (thread {Thread})",
                clock.Elapsed.TotalSeconds, id, delaySeconds, startThread);

            string status = "Answered";
            string answer;
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);

                // Typed HttpClients are transient, so each worker gets its own from a fresh scope
                using var scope = scopes.CreateScope();
                var ai = scope.ServiceProvider.GetRequiredService<AiServiceClient>();
                jobs[id] = jobs[id] with { Status = "Asking AI" };
                answer = await ai.AskAsync(question, ct);
            }
            catch (HttpRequestException)
            {
                status = "Failed";
                answer = "(AI service is not available)";
            }
            catch (Exception ex)
            {
                // Nothing awaits this task, so an escaped exception would be lost silently
                status = "Failed";
                answer = ex.Message;
            }

            var endThread = Environment.CurrentManagedThreadId;
            var answeredAt = Math.Round(clock.Elapsed.TotalSeconds, 2);
            jobs[id] = jobs[id] with { Status = status, EndThreadId = endThread, AnsweredAtSeconds = answeredAt, Answer = answer };
            logger.LogInformation("[{Time,5:F1}s] Worker {Id} {Status} (thread {Thread}): {Answer}",
                answeredAt, id, status.ToLowerInvariant(), endThread, answer);
        }
    }
}
