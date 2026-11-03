# Shipment Assistant

[![CI](https://github.com/CodingAccountNick/shipment-assistant/actions/workflows/ci.yml/badge.svg)](https://github.com/CodingAccountNick/shipment-assistant/actions/workflows/ci.yml)

An AI assistant that answers questions about shipments in plain language: where a package is, when it will arrive, and why it's delayed.

> 🟡 **Work in progress.** I'm building this in the open, so expect frequent changes.

## Structure

| Folder | What it is | Status |
|---|---|---|
| `web/` | Angular front end (standalone components, signals): a shipment list and a chat with the assistant, served by nginx in Docker | In progress |
| `ai-service/` | Python service (FastAPI) that talks to a locally hosted LLM (Llama 3.2 via Ollama) | In progress |
| `src/Shipments.Api/` | ASP.NET Core 8 Web API that stores shipments and forwards questions to the AI service | In progress |
| `src/Shipments.Api/Data/` | `Shipment` entity and `ShipmentsDbContext` (Entity Framework Core, code-first) | Done |
| `src/Shipments.Api/Migrations/` | EF Core migrations that create the database tables | Done |
| `tests/Shipments.Api.Tests/` | xUnit unit tests for the status rules, and endpoint tests against a real PostgreSQL in Testcontainers | Done |
| `docker-compose.yml` | Starts the whole app (web, API, AI service, PostgreSQL) in Docker with one command | Done |

```text
Browser ──► web (Angular + nginx, :8080)
                 │  /api/...
                 ▼
            Shipments.Api (.NET, :5108) ──► ai-service (Python, :8001) ──► Ollama / Llama 3.2 (your machine, :11434)
                 │
                 └──► PostgreSQL + pgvector (:5432)
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

The first build takes a few minutes. The database and its tables are created automatically on first start. When it's done:

- **The app:** open **http://localhost:8080** to see the shipment list and chat with the assistant. Click one of the suggested questions to try it. The first answer can take a little longer while Ollama loads the model.
- **The API:** open **http://localhost:5108**, which redirects to Swagger, to try the endpoints directly.

| Command | What it does |
|---|---|
| `docker compose up -d --build` | Build and start all services in the background |
| `docker compose ps` | Show whether the containers are running (the database should say `healthy`) |
| `docker compose logs -f` | Follow all services' logs |
| `docker compose exec db psql -U shipments -d shipments` | Open a SQL prompt on the database (`\dt` lists the tables, `\q` exits) |
| `docker compose down` | Stop everything (the data is kept) |
| `docker compose down -v` | Stop everything and **delete the database data** |

### Troubleshooting

| Problem | Fix |
|---|---|
| `503 AI service is not available` | Check that the containers are running (`docker compose ps`) and that Ollama is running on your machine |
| `port is already allocated` | Something else is using port 8080, 5108, 8001 or 5432, for example the API running from Visual Studio or a local PostgreSQL install. Stop it and run `docker compose up -d` again |
| The app or Swagger doesn't load | The build may still be running, or the API is waiting for the database. Check `docker compose logs -f web shipments-api` |
| The app shows "Could not load shipments" | The API isn't reachable. Check `docker compose ps` and `docker compose logs shipments-api` |

## Developing without Docker

To debug the API in Visual Studio, stop its container first so port 5108 is free:

```bash
docker compose stop shipments-api
```

Then open `ShipmentAssistant.sln` and press **F5**, or run `dotnet run --project src/Shipments.Api`. The API reads its settings from `appsettings.json`, which point at the containers through your machine: the AI service at `http://127.0.0.1:8001` and the database at `localhost:5432`.

### Front end

The Angular dev server reloads instantly when you save. It runs alongside the Docker setup on a different port:

```bash
cd web
npm install
npm start
```

Open **http://localhost:4200**. `proxy.conf.json` forwards `/api/...` requests to the API on port 5108, so the app and the API look like one site to the browser and no CORS setup is needed.

### Changing the database model

After changing the `Shipment` entity or `ShipmentsDbContext`, create a migration and apply it (needs the EF tool once: `dotnet tool install --global dotnet-ef`):

```bash
dotnet ef migrations add <DescriptiveName> --project src/Shipments.Api
dotnet ef database update --project src/Shipments.Api
```

Commit the generated files in `Migrations/` with the code change. The Docker setup also applies new migrations automatically when the API starts.

To run the AI service without Docker too, stop its container (`docker compose stop ai-service`) and then:

```bash
cd ai-service
python -m venv .venv
.venv\Scripts\activate        # Windows (macOS/Linux: source .venv/bin/activate)
pip install -r requirements.txt
uvicorn main:app --host 127.0.0.1 --port 8001
```

If `uvicorn` isn't found, run it through the virtual environment's Python instead: `.venv\Scripts\python.exe -m uvicorn main:app --host 127.0.0.1 --port 8001`. To ask a single question from the terminal, run `python ask.py`.

## Running the tests

Docker Desktop must be running, because the endpoint tests start their own database container. From the repository root:

```bash
dotnet test
```

The same tests run automatically on GitHub Actions for every push and pull request to `main` (see `.github/workflows/ci.yml`), together with a build of all the Docker images, which also checks that the Angular app compiles. The badge at the top shows the latest result.

There are two kinds of tests:

- **Unit tests** (`ShipmentStatusRulesTests`) check the status rules on their own, with no database or HTTP. A shipment can only move along the allowed path (`Created → InTransit → OutForDelivery → Delivered`, with `Delayed` possible on the way), and a delivered shipment can't change again.
- **Endpoint tests** (`ShipmentsEndpointTests`) start the real API in memory with `WebApplicationFactory` and send real HTTP requests to it. They check status codes (201, 400, 404, 409), response bodies and the ProblemDetails errors. [Testcontainers](https://testcontainers.com) starts a fresh PostgreSQL container for each test run and removes it afterwards, so the tests use a real database and never touch your development data. Each test that changes data creates its own shipment, so the tests don't depend on each other or on the order they run in.

## How the Docker setup works

- **Four containers, one network.** Docker Compose puts the services on a private network, where they reach each other by service name: nginx forwards `/api/...` to `http://shipments-api:8080`, the API calls `http://ai-service:8001`, and it connects to the database at `Host=db`.
- **The front end is just files.** The Angular app is compiled in a Node image, then only the built files are copied into a small nginx image. nginx serves them, falls back to `index.html` for app routes, and forwards API calls with a longer timeout for slow AI answers.
- **Configuration by environment.** `appsettings.json` holds the addresses for local development. In Docker, the environment variables `AiService__BaseUrl` and `ConnectionStrings__Shipments` override them (`__` stands for `:` in .NET configuration), so the same code works in both places.
- **The API waits for the database.** PostgreSQL has a health check (`pg_isready`), and the API only starts once it passes, so it never tries to connect too early.
- **Tables are created automatically.** In Development, the API applies any pending EF Core migrations when it starts. In production you'd run migrations as a separate deployment step instead, so several app instances don't try to change the database at the same time.
- **Data survives restarts.** PostgreSQL stores its data in a named volume (`db-data`), so `docker compose down` keeps it. Only `docker compose down -v` deletes it.
- **Ready for RAG.** The database image includes the pgvector extension, which the AI search features will use later.
- **Ollama stays on your machine.** The AI service reaches it through `host.docker.internal`, set as `OLLAMA_URL` in its Dockerfile. When run directly, `ask.py` falls back to `http://localhost:11434`.
- **Multi-stage .NET build.** The API is compiled in the .NET SDK image, then only the published output is copied into the smaller ASP.NET runtime image.
- **Fixed ports.** The web app is published on `8080`, the API on `5108` (port `8080` inside its container) and the AI service on `8001`. The API and AI service use the same ports as when running without Docker.
- **Faster rebuilds.** All three Dockerfiles restore or install packages before copying the code, so Docker reuses those cached layers when only the code changes.

## Endpoints

| Method | Route | What it does |
|---|---|---|
| `GET` | `/api/shipments` | Lists all shipments |
| `GET` | `/api/shipments/{trackingNumber}` | Gets one shipment, or `404` if it doesn't exist |
| `POST` | `/api/shipments` | Creates a shipment. Returns `201`, `400` for invalid input (tracking numbers look like `SH-1234`), or `409` if the tracking number is taken |
| `PATCH` | `/api/shipments/{trackingNumber}/status` | Changes a shipment's status and records the delivery time when it's delivered. Returns `409` for a shipment that's already delivered |
| `POST` | `/api/assistant/ask` | Asks the AI one question and waits for the answer. If the question mentions tracking numbers, the answer is based on those shipments' real data. Returns `503` if the AI service is down. |
| `POST` | `/api/demo/start` | Starts the concurrent workers demo (below) and returns `202 Accepted` immediately. |
| `GET` | `/api/demo/jobs` | Shows the live status of each demo worker. |

Statuses are sent and received as names (`"InTransit"`, `"Delivered"`), not numbers, so clients don't depend on the enum's order. Errors use the standard [ProblemDetails](https://www.rfc-editor.org/rfc/rfc9457) format (`application/problem+json`), so every error has the same shape:

```json
{
  "title": "Shipment already delivered",
  "status": 409,
  "detail": "Shipment SH-1004 is already delivered",
  "traceId": "00-..."
}
```

In Development, the database starts with four sample shipments (`SH-1001` to `SH-1004`) to try the endpoints with.

## How the assistant uses shipment data

Ask "Where is SH-1001?" and the assistant answers from the database, not from guesswork:

```text
"Where is SH-1001?" ──► Shipments.Api finds "SH-1001" in the question and loads it from PostgreSQL
                              │
                              ▼  question + "Shipment SH-1001: from London to Vienna, status InTransit, ..."
                         ai-service adds those facts to the prompt ──► Llama 3.2 answers from them
```

This is the first step of retrieval-augmented generation (RAG): **retrieve** the relevant facts, **augment** the prompt with them, then let the model **generate** the answer.

- **The facts come from the server.** Clients only send a question, and the API builds the context itself, so nobody can feed the model made-up data.
- **Unknown shipments are facts too.** For `SH-9999`, the model is told the shipment isn't in the system, so it says so instead of inventing a location.
- **One query for every shipment mentioned.** "Compare SH-1001 and SH-1002" loads both in a single database call.
- **The prompt tells the model to stick to the facts** and to say it doesn't know when they don't contain the answer.

Questions without a tracking number ("What does out for delivery mean?") are answered from the model's general knowledge.

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

This run is from before the assistant was connected to the shipment data, so the model didn't know these shipments yet. Now the same questions are answered from the database (see "How the assistant uses shipment data").

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
- [x] PostgreSQL (with pgvector) in Docker, with data kept in a volume
- [x] `Shipment` entity with Entity Framework Core code-first and migrations, applied automatically on startup
- [x] Shipment endpoints (create, list, get, update status) with validation and ProblemDetails errors
- [x] xUnit tests: unit tests for the status rules, and endpoint tests against a real PostgreSQL in Testcontainers
- [x] Angular front end (standalone components, signals) with a shipment list and a chat box, served by nginx in Docker
- [x] GitHub Actions: build, run all tests and build the Docker images on every push and pull request
- [x] Grounded answers: questions about a tracking number are answered from that shipment's data in PostgreSQL
- [ ] RAG over documents: delivery policies split into chunks, embedded and searched with pgvector, with a small test set to measure accuracy
- [ ] Messaging: a "shipment delivered" event sent through RabbitMQ to a separate .NET notifications service

## Tech

Angular · TypeScript · nginx · C# · ASP.NET Core 8 · Entity Framework Core · PostgreSQL · pgvector · xUnit · Testcontainers · Python · FastAPI · Docker · Docker Compose · GitHub Actions · Ollama · Llama 3.2
