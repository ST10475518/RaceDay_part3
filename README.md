# RaceDay

RaceDay is a platform for running, walking and cycling events. **Organisers** create and manage events and publish
results; **Participants** browse events, enter in a category and follow their race history.

| Project | What it is |
|---|---|
| `RaceDay.Api` | ASP.NET Core 8 Web API (Part 2): EF Core + SQL Server, session auth, Swagger, **Azure Blob Storage** uploads |
| `RaceDay.Web` | ASP.NET Core 8 **MVC** front end (Part 3): consumes the API, session auth, role-specific UI, Bootstrap theme |
| `RaceDay.Tests` | xUnit tests for the API (in-memory) |
| `docker-compose.yml` | Runs SQL Server + API + MVC with one command |

## CI status
![Green build](docs/green-build.png)
<!-- Replace docs/green-build.png with your own screenshot of the green GitHub Actions run -->

## Demo video
https://youtu.be/REPLACE_ME  <!-- unlisted YouTube link -->

---

## Run with Docker (single command)

Requires Docker Desktop. From the repository root:

```bash
docker compose up --build
```

Then open:

* **MVC app:** http://localhost:8080
* **API + Swagger:** http://localhost:5100/swagger

This starts SQL Server, the API (which applies the EF Core migrations on startup and waits for the database) and the MVC app,
wired together. The containerised app behaves the same as running locally. Stop it with `Ctrl+C` / `docker compose down`
(add `-v` to also wipe the database).

> SQL Server's image needs an x86-64 machine (or Docker Desktop's emulation) and ~2 GB of RAM.

### Run only the MVC container
If the API is already running elsewhere (for example on your machine on port 5100):

```bash
docker build -t raceday-web ./RaceDay.Web
docker run --rm -p 8080:8080 -e Api__BaseUrl=http://host.docker.internal:5100 -e Api__PublicUrl=http://localhost:5100 raceday-web
```

### Azure Blob Storage in Docker
Set your storage connection string before starting; otherwise the API stores images in a local volume instead:

```bash
# PowerShell
$env:AZURE_STORAGE_CONNECTION_STRING = "<your connection string>"
docker compose up --build
```

---

## Run locally (Visual Studio 2022 / .NET 8 SDK)

1. **Database:** SQL Server or LocalDB. Edit `ConnectionStrings:DefaultConnection` in `RaceDay.Api/appsettings.json` if needed.
2. **Migrations** (first time only; commit the generated `Migrations` folder):
   ```bash
   dotnet tool install --global dotnet-ef
   dotnet ef migrations add InitialCreate --project RaceDay.Api
   ```
   (Visual Studio: Package Manager Console -> `Add-Migration InitialCreate`.) The API applies migrations on startup.
3. **Azure Blob Storage (optional):** set `AzureBlob:ConnectionString` (use `dotnet user-secrets` or an environment variable,
   never commit it). Leave it empty to use a local `uploads` folder instead.
4. **Start both projects:** in Visual Studio right-click the solution -> *Configure Startup Projects* -> *Multiple startup projects* ->
   set `RaceDay.Api` and `RaceDay.Web` to *Start*. Or in two terminals:
   ```bash
   dotnet run --project RaceDay.Api
   dotnet run --project RaceDay.Web
   ```
5. Make sure `Api:BaseUrl` / `Api:PublicUrl` in `RaceDay.Web/appsettings.json` match the URL the API prints on startup.

Run the tests with `dotnet test`.

---

## The two roles

| Role | What they can do |
|---|---|
| **Organiser** | Dashboard (events created, enrolments per event, upcoming dates); create, edit and delete events with a banner image; add categories; view enrolments and confirm / cancel them; enter finish times and positions and publish results. |
| **Participant** | Browse and filter upcoming events; view event details and categories; enter an event in a category; see their enrolments and status; view **My Results** (event, date, category, finish time and position such as "47th of 312 finishers"); edit their profile and upload a profile picture. |

Organiser sign-up requires an **organiser code** (`Registration:OrganiserCode`, default `RACEDAY-ORG-2026`) so users cannot simply promote themselves.
Change it for any real deployment.

## Architecture

```
Browser --> RaceDay.Web (MVC) --HTTP--> RaceDay.Api --> SQL Server
                                            \--> Azure Blob Storage (images)
```

* **No database or Azure access in MVC.** `RaceDay.Web` only calls the API through `Services/ApiClient.cs`; all rules, validation and file handling live in the API.
* **Session management.** On login the MVC app calls `POST /api/auth/login`, then stores the user id, role, name and the API's session cookie in its own server-side
  session (`HttpContext.Session`). `[SessionAuthorize("Organiser")]` / `[SessionAuthorize("Participant")]` protect pages: no session -> redirect to Login,
  wrong role -> Access Denied. Logout calls the API, clears the session and redirects to Login. An expired session redirects to Login with a message.
* **Role-specific navigation.** The layout renders a different menu for Organisers, Participants and anonymous visitors.
* **Blob Storage flow.** Browser uploads a file to MVC -> MVC forwards it to `POST /api/events/{id}/banner` (or `/api/users/me/picture`) -> API validates it
  (type, 5 MB) and uploads it to the Azure container -> the blob URL is saved in the database (`Events.BannerUrl`, `Users.ProfilePictureUrl`) -> MVC displays it on the event pages.
* **Colour-coded enrolment statuses:** Confirmed (green), Pending (amber), Cancelled (red).
* **Graceful errors.** Forms use validation messages; API failures and unexpected exceptions show a friendly page rather than raw errors.

### Project structure
```
RaceDay.Api/     Models | Data | Dtos | Services (blob storage) | Controllers | Dockerfile
RaceDay.Web/     Controllers | Services (ApiClient) | Filters | Models | Views | wwwroot | Dockerfile
RaceDay.Tests/   xUnit tests (auth, events, role access, enrolments, results, uploads)
.github/workflows/ci.yml   verifies structure, builds API + MVC, runs tests, builds Docker images
docker-compose.yml
```

## API endpoints (see Swagger for full descriptions)

| Method | Route | Access |
|---|---|---|
| POST | /api/auth/register, /api/auth/login | Public |
| POST | /api/auth/logout | Logged in |
| GET / PUT | /api/users/me | Logged in |
| POST | /api/users/me/picture | Logged in (multipart, stored in Blob Storage) |
| GET | /api/events, /api/events/{id} | Logged in |
| GET | /api/events/mine | Organiser |
| POST / PUT / DELETE | /api/events, /api/events/{id} | Organiser (owner) |
| POST | /api/events/{id}/banner | Organiser (owner, multipart, stored in Blob Storage) |
| GET / POST | /api/events/{eventId}/categories | Logged in / Organiser (owner) |
| PUT / DELETE | /api/categories/{id} | Organiser (owner) |
| POST / GET | /api/enrolments, /api/enrolments/my | Participant |
| DELETE | /api/enrolments/{id} | Participant (own) |
| GET | /api/events/{eventId}/enrolments | Organiser (owner) |
| PUT | /api/enrolments/{id}/status | Organiser (owner) |
| POST / PUT | /api/results, /api/results/{id} | Organiser (owner) |
| GET | /api/events/{eventId}/results | Organiser (owner) |
| GET | /api/results/my | Participant |

## CI/CD
`.github/workflows/ci.yml` runs on every push: it verifies the repository structure (README, Dockerfiles, compose file, migrations),
restores and builds the API **and the MVC project**, runs all unit tests, then builds both Docker images.

## Security notes
Passwords are stored only as salted hashes. Never commit the Azure connection string; supply it through user secrets or environment variables.
