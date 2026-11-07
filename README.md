# Shipment Assistant

[![CI](https://github.com/CodingAccountNick/shipment-assistant/actions/workflows/ci.yml/badge.svg)](https://github.com/CodingAccountNick/shipment-assistant/actions/workflows/ci.yml)

An AI assistant that answers questions about shipments in plain language: where a package is, when it will arrive, and why it's delayed.

> 🟡 **Work in progress.** I'm building this in the open, so expect frequent changes.

## Structure

| Folder | What it is | Status |
|---|---|---|
| `web/` | Angular front end (standalone components, signals): a shipment list and a chat with the assistant, served by nginx in Docker | In progress |
| `ai-service/` | Python service (FastAPI) that talks to a locally hosted LLM (Llama 3.2 via Ollama) and searches the policy documents | In progress |
| `ai-service/docs/` | Company policy documents (delivery, missed deliveries, delays) that the assistant answers from | Done |
| `src/Shipments.Api/` | ASP.NET Core 8 Web API that stores shipments and forwards questions to the AI service | In progress |
| `src/Notifications.Worker/` | .NET 10 background service that listens for "shipment delivered" events from RabbitMQ and notifies the customer | Done |
| `src/Shipments.Api/Data/` | `Shipment` entity and `ShipmentsDbContext` (Entity Framework Core, code-first) | Done |
| `src/Shipments.Api/Migrations/` | EF Core migrations that create the database tables | Done |
| `tests/Shipments.Api.Tests/` | xUnit unit tests for the status rules, and endpoint tests against a real PostgreSQL in Testcontainers | Done |
| `docker-compose.yml` | Starts the whole app (web, API, AI service, notifications, PostgreSQL, RabbitMQ) in Docker with one command | Done |

```text
Browser ──► web (Angular + nginx, :8080)
                 │  /api/...
                 ▼
            Shipments.Api (.NET, :5108) ──► ai-service (Python, :8001) ──► Ollama / Llama 3.2 (your machine, :11434)
                 │          │                    │
                 │          │                    └──► PostgreSQL + pgvector (:5432)
                 │          └──► PostgreSQL ◄────┘
                 │
                 └── "shipment.delivered" ──► RabbitMQ (:5672, UI :15672) ──► Notifications.Worker (.NET 10)
```

## Quick start

You need:

- [Docker Desktop](https://www.docker.com/products/docker-desktop/), running
- [Ollama](https://ollama.com), running, with both models pulled: `ollama pull llama3.2` and `ollama pull nomic-embed-text`

Then:

```bash
git clone https://github.com/CodingAccountNick/shipment-assistant.git
cd shipment-assistant
docker compose up -d --build
```

The first build takes a few minutes. The database and its tables are created automatically on first start. When it's done:

- **The app:** open **http://localhost:8080** to see the shipment list and chat with the assistant. Click one of the suggested questions to try it. The first answer can take a little longer while Ollama loads the model.
- **The API:** open **http://localhost:5108**, which redirects to Swagger, to try the endpoints directly.
- **Messaging:** open **http://localhost:15672** (user `shipments`, password `devpassword`) to watch RabbitMQ's exchanges and queues. Deliver a shipment and run `docker compose logs notifications` to see the notification it triggers.

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
| Answers ignore the policy documents | Check that `nomic-embed-text` is pulled in Ollama, and look for "Stored 9 chunks" in `docker compose logs ai-service` |
| No notification after delivering a shipment | Check that the last status change returned `200` (shipments must go `InTransit` → `OutForDelivery` → `Delivered`), then `docker compose logs notifications` |
| `port is already allocated` | Something else is using port 8080, 5108, 8001, 5432, 5672 or 15672, for example the API running from Visual Studio or a local PostgreSQL install. Stop it and run `docker compose up -d` again |
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

The same tests run automatically on GitHub Actions for every push and pull request to `main` (see `.github/workflows/ci.yml`), together with a build of all the Docker images, which also checks that the Angular app compiles. CI installs both .NET 8 and .NET 10, because the solution contains projects for both. The badge at the top shows the latest result.

There are two kinds of tests:

- **Unit tests** (`ShipmentStatusRulesTests`) check the status rules on their own, with no database or HTTP. A shipment can only move along the allowed path (`Created → InTransit → OutForDelivery → Delivered`, with `Delayed` possible on the way), and a delivered shipment can't change again.
- **Endpoint tests** (`ShipmentsEndpointTests`) start the real API in memory with `WebApplicationFactory` and send real HTTP requests to it. They check status codes (201, 400, 404, 409), response bodies and the ProblemDetails errors. [Testcontainers](https://testcontainers.com) starts a fresh PostgreSQL container for each test run and removes it afterwards, so the tests use a real database and never touch your development data. Each test that changes data creates its own shipment, so the tests don't depend on each other or on the order they run in.

## Messaging: shipment delivered events

When a shipment is marked as delivered, the API announces it through RabbitMQ instead of calling other services directly:

```text
Shipments.Api ──► exchange "shipments" ──(routing key: shipment.delivered)──► queue "notifications.shipment-delivered" ──► Notifications.Worker
```

The worker logs the email it would send:

```text
Email to customer: your shipment SH-6010 from London to Vienna was delivered on 07 Nov 2026 15:40.
```

- **The API doesn't wait and doesn't know who listens.** It publishes the event and returns. New listeners (invoicing, analytics) can be added by binding their own queue to the exchange, with no change to the API.
- **Nothing is lost when a listener is down.** Stop the worker (`docker compose stop notifications`), deliver a shipment, and the message waits in its queue. Start the worker again and the notification is sent straight away.
- **Events go out only after the change is saved.** The API publishes after `SaveChangesAsync`. If RabbitMQ is unreachable, the delivery is still saved and the failure is logged.
- **Messages are confirmed, not just received.** The worker acknowledges each message only after handling it, so a crash mid-way means the message is delivered again. A message that can't be processed is rejected, so it doesn't block the queue.
- **One message at a time.** The worker takes the next message only after confirming the current one (prefetch 1).
- **The contract is the JSON, not shared code.** Each service has its own copy of the `ShipmentDelivered` record, so they can change and deploy independently. Both use camelCase JSON.
- **Different .NET versions per service.** The worker runs on .NET 10 while the API is on .NET 8, which separate services make possible. (Building the worker in Visual Studio needs Visual Studio 2026.)

**What I'd add for production**

- **The outbox pattern.** Today, if RabbitMQ is down at the moment of delivery, that event is lost. With an outbox, the event is saved in the database in the same transaction as the status change, and a background job sends it to RabbitMQ, retrying until it succeeds.
- **A dead-letter queue** for messages the worker rejects, so they can be inspected and replayed instead of dropped.
- **A fake publisher in the tests.** The endpoint tests currently try to publish to a real RabbitMQ. Putting the publisher behind an interface would let the tests use a fake and check exactly which events were sent.

## How the Docker setup works

- **Six containers, one network.** Docker Compose puts the services on a private network, where they reach each other by service name: nginx forwards `/api/...` to `http://shipments-api:8080`, the API calls `http://ai-service:8001`, both connect to the database at `db`, and the API and the worker connect to RabbitMQ at `rabbitmq`.
- **Each service waits for what it needs.** PostgreSQL and RabbitMQ have health checks, and the services that depend on them only start once they pass.
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

## How the assistant uses the policy documents

Ask "What happens if nobody is home?" and the assistant answers from the company's own policies in `ai-service/docs/`, found by **meaning**, not by matching words:

```text
docs/*.md ──► one chunk per section ──► embedded with nomic-embed-text ──► stored in PostgreSQL (pgvector)

question ──► embedded ──► pgvector finds the 3 closest chunks ──► chunks scoring ≥ 0.57 go into the prompt
```

- **Embeddings.** An embedding model turns each text into 768 numbers that represent its meaning. Texts with similar meanings get similar vectors, so "What if I miss the courier?" finds the section about recipients who aren't home, even though they share no words.
- **Chunks.** Each `## ` section is one chunk, with the document's title in front for context, so a short section like "Compensation" still says it's about delays.
- **Search in the database.** pgvector compares the question's vector with every chunk using cosine distance (`embedding <=> query`) and returns the closest ones.
- **Question and document prefixes.** `nomic-embed-text` is trained with `search_query:` and `search_document:` labels. Using them widened the gap between the right chunk and the next best from 0.007 to 0.037 in testing.
- **A minimum score.** Chunks scoring below 0.57 are ignored, so off-topic questions don't pull random policy text into the prompt.
- **Combined with shipment data.** "Where is SH-1003 and why might it be late?" gets the shipment's facts from the API and the delays policy from pgvector, in one prompt.
- **Built on startup.** The AI service re-reads and re-embeds the documents every time it starts, so editing a file in `docs/` and restarting is all it takes. If the database or embedding model is unavailable, the assistant still answers, just without the documents.

### Measuring retrieval

`ai-service/eval_retrieval.py` runs 12 test questions and checks which document is found for each. The questions deliberately use different words from the documents ("expensive parcel" instead of "worth more than 500 euros"), and two are off-topic and should find nothing.

```bash
cd ai-service
python eval_retrieval.py
```

Current results:

| Measure | Result |
|---|---|
| Top-1 accuracy (best chunk from the right document) | 10/10 |
| Hit rate (right document in the top 3) | 10/10 |
| Off-topic questions with no documents | 2/2 |

The minimum score of 0.57 was picked from these results: real questions scored at least 0.639 and the highest off-topic question 0.505, so the threshold sits in the middle of that gap. Re-run the evaluation after changing the documents, the chunking, the threshold or the model.

Questions without a tracking number or a matching policy ("What does out for delivery mean?") are answered from the model's general knowledge.

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
- [x] RAG over documents: delivery policies split into chunks, embedded and searched with pgvector, with a test set to measure retrieval accuracy
- [x] Messaging: a "shipment delivered" event sent through RabbitMQ to a separate .NET 10 notifications service
- [ ] Outbox pattern, so no event is lost when RabbitMQ is unreachable
- [ ] Upgrade the API and tests to .NET 10 (.NET 8 reaches end of support in November 2026)
- [ ] Conversation history in the chat, so follow-up questions keep their context

## Tech

Angular · TypeScript · nginx · C# · ASP.NET Core 8 · .NET 10 worker service · RabbitMQ · Entity Framework Core · PostgreSQL · pgvector · xUnit · Testcontainers · Python · FastAPI · Docker · Docker Compose · GitHub Actions · Ollama · Llama 3.2 · nomic-embed-text · psycopg
