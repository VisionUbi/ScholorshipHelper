# Scholarship AutoFill Assistant

Production shaped MVP for storing scholarship application profile data, managing documents, tracking university applications, analyzing application links, mapping fields with AI, and running guarded browser automation.

The most important safety rule is enforced in both API behavior and UI copy: automation must never click final submit. The user must review and submit manually.

## Stack

- Angular frontend
- ASP.NET Core Web API
- PostgreSQL
- Entity Framework Core migrations
- Playwright for .NET
- JWT authentication
- Local file storage behind an abstraction
- OpenAI API compatible AI service

## Project Structure

- `backend/ScholarshipAutoFill.Domain`: entities and domain model
- `backend/ScholarshipAutoFill.Application`: DTOs and service contracts
- `backend/ScholarshipAutoFill.Infrastructure`: EF Core, PostgreSQL, storage, AI, Playwright
- `backend/ScholarshipAutoFill.Api`: REST API, auth, Swagger, seed data
- `frontend`: Angular MVP application
- `docker-compose.yml`: PostgreSQL for local development

## Seed Login

- Email: `ubaidkhank1998@gmail.com`
- Password: `ChangeMe123!`

Change this password immediately outside local development.

## Run PostgreSQL

```powershell
docker compose up -d postgres
```

## Database Connection String

Local development connection string is in `backend/ScholarshipAutoFill.Api/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=1234;Include Error Detail=true"
}
```

Replace `DefaultConnection` with your real PostgreSQL connection string when you are ready:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=YOUR_POSTGRES_HOST;Port=5432;Database=YOUR_DATABASE_NAME;Username=YOUR_DATABASE_USER;Password=YOUR_DATABASE_PASSWORD"
}
```

## Run Backend

```powershell
dotnet restore ScholarshipAutoFillAssistant.sln
dotnet ef database update --project backend\ScholarshipAutoFill.Infrastructure\ScholarshipAutoFill.Infrastructure.csproj --startup-project backend\ScholarshipAutoFill.Api\ScholarshipAutoFill.Api.csproj
dotnet run --project backend\ScholarshipAutoFill.Api\ScholarshipAutoFill.Api.csproj
```

Swagger is available at the API host root `/swagger`.

## Run Angular

Node.js is required. It was not available on this machine during generation.

```powershell
cd frontend
npm install
npm start
```

Open `http://localhost:4200`.

## Playwright Setup

After restoring backend packages, install browser binaries:

```powershell
pwsh backend\ScholarshipAutoFill.Api\bin\Debug\net10.0\playwright.ps1 install
```

The automation service launches Chromium visibly, fills only mapped fields, pauses naturally for login or captcha because it does not bypass security controls, and records a review object. It does not click final submit.

## AI Configuration

Set these values in `appsettings.json`, user secrets, or environment variables:

- `OpenAI:Endpoint`
- `OpenAI:Model`
- `OpenAI:ApiKey`

If no API key is configured, the app uses deterministic heuristic mapping and a simple local draft generator so the MVP remains usable.

## Scholarship AI Workflow

Use the Angular `Scholarship AI` tab to paste a scholarship or admission link. The backend will:

- Fetch and analyze visible page content.
- Compare detected requirements with the saved applicant profile.
- Save an application tracker record.
- Save an eligibility analysis record.
- Let you save portal username and encrypted password.
- Optionally start browser filling for the created application record.

Automation does not bypass captcha, does not bypass login security, and does not click final submit.

Portal passwords are encrypted using:

```json
"Security": {
  "CredentialEncryptionKey": "replace-this-with-a-long-local-secret-before-saving-real-passwords"
}
```

## Security Notes

- JWT is used for API authentication.
- Uploaded files are validated by MIME type and limited to 10 MB.
- Local file paths are stored as internal storage keys and are not exposed as direct paths.
- OpenAI keys are used only by the backend.
- Sensitive production values should be moved to environment variables or a secrets manager.
- Add field level encryption for passport, CNIC, and login note fields before hosting this beyond a trusted local environment.

## Current MVP Scope

Implemented:

- Seeded applicant profile
- Profile API and Angular profile screen
- Document upload, list, download API, and Angular vault screen
- Universities, scholarships, applications CRUD APIs
- Tracker screen
- Link analyzer endpoint and screen
- AI field mapping service with JSON contract and fallback heuristics
- Playwright automation service with final submit safety boundary
- Review screen
- SOP and email generator endpoint and screen
- EF Core migration for PostgreSQL

Recommended next hardening:

- Add refresh tokens and password reset
- Add full DTO validation with FluentValidation
- Add encrypted columns for sensitive identifiers
- Add S3 or Azure Blob storage implementation
- Add richer Playwright selectors and manual intervention workflow
- Add test projects for API and infrastructure services
