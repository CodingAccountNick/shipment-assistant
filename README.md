# Shipment Assistant

An AI assistant that answers questions about shipments in plain language: where a package is, when it will arrive, and why it's delayed.

> 🟡 **Work in progress.** I'm building this in the open, so expect frequent changes.

## Structure

| Folder | What it is | Status |
|---|---|---|
| `ai-service/` | Python service that talks to a locally hosted LLM (Llama 3.2 via Ollama) | In progress |
| `src/Shipments.Api/` | ASP.NET Core 8 Web API that forwards questions to the AI service | In progress |

```text
Client ──► Shipments.Api (.NET, :5108) ──► ai-service (Python, :8001) ──► Ollama / Llama 3.2 (:11434)
```

## Running it

### 1. AI service (Python)

You'll need [Ollama](https://ollama.com) running locally with the model pulled:

```bash
ollama pull llama3.2
```

Then:

```bash
cd ai-service
python -m venv .venv
.venv\Scripts\activate        # Windows (macOS/Linux: source .venv/bin/activate)
pip install -r requirements.txt
uvicorn main:app --host 127.0.0.1 --port 8001
```

This starts the AI service on `http://127.0.0.1:8001` (interactive docs at `/docs`). To ask a single question from the terminal instead, run `python ask.py`.

### 2. Web API (.NET)

Open `ShipmentAssistant.sln` in Visual Studio 2022 and press **F5**, or run:

```bash
dotnet run --project src/Shipments.Api
```

Swagger opens at `http://localhost:5108/swagger`. The address of the AI service is set in `appsettings.json` under `AiService:BaseUrl`.

## Endpoints

| Method | Route | What it does |
|---|---|---|
| `POST` | `/api/assistant/ask` | Asks the AI one question and waits for the answer. Returns `503` if the AI service is down. |
| `POST` | `/api/demo/start` | Starts the concurrent workers demo (below) and returns `202 Accepted` immediately. |
| `GET` | `/api/demo/jobs` | Shows the live status of each demo worker. |

## Demo: concurrent fire-and-forget workers

`POST /api/demo/start` starts three workers at the same time. Each one waits for a different amount of time, then asks the AI its question:

| Worker | Question | Waits |
|---|---|---|
| 1 | Where is shipment SH-1001? | 20 s |
| 2 | When will shipment SH-1002 arrive? | 10 s |
| 3 | Why is shipment SH-1003 delayed? | 5 s |

The request returns straight away, and the workers carry on in the background without waiting for each other. They finish in reverse order: 3, then 2, then 1. The whole run takes a little over 20 seconds (the longest wait plus the model's answer time), not the 35+ it would take one after another. The console logs each step with the elapsed time and the thread it ran on. This is from a real run:

```text
[  0.0s] Worker 1 started, waiting 20s (thread 11)
[  0.0s] Worker 2 started, waiting 10s (thread 13)
[  0.0s] Worker 3 started, waiting 5s (thread 12)
[  7.6s] Worker 3 answered (thread 11): I don't have real-time information about specific shipments...
[ 12.6s] Worker 2 answered (thread 15): I don't have any information about a shipment with the number SH-1002...
[ 22.3s] Worker 1 answered (thread 15): I cannot provide information about a specific shipment...
```

The model doesn't know about these shipments yet, because it isn't connected to any shipment data (see the roadmap).

Call `GET /api/demo/jobs` while it runs to watch each worker go from `Waiting` to `Asking AI` to `Answered`.

### Why demonstrate this?

Calls to an LLM are slow (seconds, not milliseconds) and spend almost all of that time waiting on the network. That's the case where async code pays off:

- **Nothing blocks while waiting.** `await Task.Delay` and `await HttpClient` hand their thread back to the pool, so three workers waiting 20 s don't tie up three threads for 20 s.
- **Slow requests don't hold up fast ones.** Question 3 gets its answer after 5 s even though question 1 was sent first and is still waiting.
- **Threads change after `await`.** A worker often resumes on a different thread than it started on, as the thread IDs in the log show. This is why you shouldn't rely on thread-local state in async code.

### Design notes

**Why `DemoWorker` is registered as a singleton**

ASP.NET Core creates a new controller for every request and disposes the request's services when it ends. The workers have to keep running after `POST /start` has returned, and `GET /jobs` has to see the same workers from a later request. A singleton lives for the whole lifetime of the app, so it can own the running workers and their shared status.

Because one instance is shared by every request and every worker thread, its state has to be thread-safe. The job list is a `ConcurrentDictionary`, and each update replaces a whole immutable `record` instead of changing fields one at a time.

**Why each worker creates its own scope**

`AiServiceClient` is a typed `HttpClient`, registered as transient. If the singleton took one in its constructor, it would hold that client for the app's whole lifetime (a *captive dependency*) and miss DNS changes and connection recycling from `IHttpClientFactory`. Each worker asks `IServiceScopeFactory` for a fresh client instead.

**Making fire-and-forget safe**

- **Every exception is caught inside the worker.** Nothing awaits the task, so an unhandled exception would disappear without a trace.
- **Workers use `IHostApplicationLifetime.ApplicationStopping` as their cancellation token**, not the request's token. The request's token is cancelled as soon as `/start` returns, and stopping the app should still cancel the workers cleanly.
- **The loop index is copied into a local before it's captured by the lambda**, so each worker gets its own ID.

**What I'd do differently in production**

This is kept simple to show the idea. A production version would:

- **Queue the work** with `Channel<T>` and a `BackgroundService`, or a job library like Hangfire, so the number of concurrent jobs is limited and the app shuts down cleanly.
- **Store job status in a database**, so it survives restarts and works across several server instances.
- **Give each run its own ID.** Right now, calling `/start` while a run is still going replaces the previous run's results.

## AI assistance

Just so you know, I used an AI coding assistant (Claude) for some parts of this project. Here's where it helped:

- **The concurrent workers demo** (`Services/DemoWorker.cs` and `Controllers/DemoController.cs`). I explained what I wanted to show, and Claude wrote the code.
- **Fixing the API setup.** When my controller wouldn't compile, Claude found what was missing and wired up `AiServiceClient` properly.
- **The FastAPI service** (`ai-service/main.py`) that puts my `ask()` function behind an `/ask` endpoint.
- **This README**, including the design notes.

The Python script, `AiServiceClient` and the first version of `AssistantController` are my own work.

I think this is worth sharing because it's how a lot of us build software now. For me, the interesting part isn't who typed each line. It's knowing what to build, guiding the tool there, and understanding the result well enough to explain it. If anything here catches your eye, I'm happy to talk it through.

## Roadmap

- [x] Send a question to a local Llama 3.2 model and return its answer
- [x] ASP.NET Core Web API with a typed `HttpClient` for the AI service
- [x] Concurrent fire-and-forget workers demo
- [x] Python REST API (FastAPI) exposing `/ask` for the .NET API to call
- [ ] Connect the model to real shipment data so its answers are based on actual records
- [ ] Web chat interface
- [ ] Docker setup

## Tech

C# · ASP.NET Core 8 · Python · FastAPI · Ollama · Llama 3.2


# Run by hand

Powershell
.venv\Scripts\python.exe -m uvicorn main:app --host 127.0.0.1 --port 8001

Browser
Run SWAGGER and make a request

