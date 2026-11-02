# Shipment Assistant
## docker compose up -d --build shipments-api 
## docker compose up -d --build ai-service      



An AI assistant that answers questions about shipments in plain language: where a package is, when it will arrive, and why it's delayed.

> 🟡 **Work in progress.** I'm building this in the open, so expect frequent changes.

## Structure

| Folder | What it is | Status |
|---|---|---|
| `ai-service/` | Python service (FastAPI) that talks to a locally hosted LLM (Llama 3.2 via Ollama) | In progress |
| `src/Shipments.Api/` | ASP.NET Core 8 Web API that forwards questions to the AI service | In progress |
| `docker-compose.yml` | Starts both services in Docker with one command | Done |

```text
   Client ──► Shipments.Api (Docker, :5108) ──► ai-service (Docker, :8001) ──► Ollama / Llama 3.2 (your machine, :11434)
                    │
                    └──► PostgreSQL + pgvector (Docker, :5432)
```

## Quick start

You need:

- [Docker Desktop](https://www.docker.com/products/docker-desktop/), running
- [Ollama](https://ollama.com), running, with the model pulled: `ollama pull llama3.2`

Then:

```bash
git clone https://github.com/CodingAccountNick/shipment-assistant.git
cd shipment-assistant
docker compose up -d --build
```

The first build takes a few minutes. When it's done, open **http://localhost:5108/swagger**, expand `POST /api/assistant/ask`, click **Try it out**, then **Execute**. The request body already contains a sample question. The first answer can take a little longer while Ollama loads the model.

| Command | What it does |
|---|---|
| `docker compose up -d --build` | Build and start both services in the background |
| `docker compose ps` | Show whether both containers are running |
| `docker compose logs -f` | Follow both services' logs |
| `docker compose down` | Stop everything |

### Troubleshooting

| Problem | Fix |
|---|---|
| `503 AI service is not available` | Check that both containers are running (`docker compose ps`) and that Ollama is running on your machine |
| `port is already allocated` | Something else is using port 5108 or 8001, for example the API running from Visual Studio. Stop it and run `docker compose up -d` again |
| Swagger page doesn't load | The build may still be running. Check `docker compose logs -f shipments-api` |

## Developing without Docker

To debug the API in Visual Studio, stop its container first so port 5108 is free:

```bash
docker compose stop shipments-api
```

Then open `ShipmentAssistant.sln` and press **F5**, or run `dotnet run --project src/Shipments.Api`. The API reads the AI service's address from `appsettings.json` (`AiService:BaseUrl` = `http://127.0.0.1:8001`), so it still uses the AI service container.

To run the AI service without Docker too, stop its container (`docker compose stop ai-service`) and then:

```bash
cd ai-service
python -m venv .venv
.venv\Scripts\activate        # Windows (macOS/Linux: source .venv/bin/activate)
pip install -r requirements.txt
uvicorn main:app --host 127.0.0.1 --port 8001
```

If `uvicorn` isn't found, run it through the virtual environment's Python instead: `.venv\Scripts\python.exe -m uvicorn main:app --host 127.0.0.1 --port 8001`. To ask a single question from the terminal, run `python ask.py`.

## How the Docker setup works

- **Two containers, one network.** Docker Compose puts both services on a private network, where the API reaches the AI service by its service name: `http://ai-service:8001`.
- **Configuration by environment.** `appsettings.json` points the API at `http://127.0.0.1:8001` for local development. In Docker, the environment variable `AiService__BaseUrl` overrides it (`__` stands for `:` in .NET configuration), so the same code works in both places.
- **Ollama stays on your machine.** The AI service reaches it through `host.docker.internal`, set as `OLLAMA_URL` in its Dockerfile. When run directly, `ask.py` falls back to `http://localhost:11434`.
- **Multi-stage .NET build.** The API is compiled in the .NET SDK image, then only the published output is copied into the smaller ASP.NET runtime image.
- **Fixed ports.** The API is published on `5108` (port `8080` inside its container) and the AI service on `8001`, the same ports used when running without Docker.
- **Faster rebuilds.** Both Dockerfiles restore or install packages before copying the code, so Docker reuses those cached layers when only the code changes.
 **Data survives restarts.** PostgreSQL stores its data in a named volume (`db-data`), so `docker compose down` keeps it. Only `docker compose down -v` deletes it.

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
- **The Docker setup.** Claude walked me through containerising both services step by step. I wrote the files and fixed the problems along the way.
- **This README**, including the design notes.

The Python script, `AiServiceClient` and the first version of `AssistantController` are my own work.

I think this is worth sharing because it's how a lot of us build software now. For me, the interesting part isn't who typed each line. It's knowing what to build, guiding the tool there, and understanding the result well enough to explain it. If anything here catches your eye, I'm happy to talk it through.

## Roadmap

- [x] Send a question to a local Llama 3.2 model and return its answer
- [x] ASP.NET Core Web API with a typed `HttpClient` for the AI service
- [x] Concurrent fire-and-forget workers demo
- [x] Python REST API (FastAPI) exposing `/ask` for the .NET API to call
- [x] Docker: the AI service and the .NET API each run in their own container, started together with one Docker Compose command
- [ ] Shipments stored in PostgreSQL (in Docker) with Entity Framework Core (code-first, migrations), with xUnit tests
- [ ] Angular front end (standalone components, signals) with a shipment list and a chat box
- [ ] RAG: the AI answers from real shipment documents using pgvector, with a small test set to measure accuracy
- [ ] Messaging: a "shipment delivered" event sent through RabbitMQ to a separate .NET notifications service
- [ ] GitHub Actions: build and run all tests on every push

## Tech

C# · ASP.NET Core 8 · Python · FastAPI · Docker · Docker Compose · Ollama · Llama 3.2