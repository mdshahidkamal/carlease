# CarLease .NET API SDLC and Non-Production release process

This is the operating procedure for API changes from a developer's Windows workstation through GitHub review and AWS Non-Production deployment.

## Branching model

- `main`: Production code line. Do not develop directly on this branch.
- `develop`: integration and Non-Production release branch.
- `feature/<work-item>-<short-name>`: short-lived development branches created from `develop`.
- Pull requests from feature branches target `develop`.
- Merge to `develop` is the release event for AWS Non-Production.
- Production promotion from `develop` to `main` is a separate release decision and is not part of this Non-Production workflow.

## Local workstation prerequisites

- .NET 10 SDK
- Git for Windows
- PostgreSQL 17 (local Windows service) for local integration testing
- PowerShell
- Node.js 22 only when changing the React frontend

Docker Desktop is not required on the developer workstation. GitHub-hosted runners build the container images.

## One-time local configuration

Clone the repository and configure a local PostgreSQL database, for example `carlease_dev`. Do not use the AWS Non-Production database for everyday local development.

Set the local connection string in .NET User Secrets. Do not commit local credentials:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=carlease_dev;Username=carlease_dev;Password=<your-local-password>" --project backend/CarLease.Api.csproj
```

Restore the pinned EF Core CLI tool and solution dependencies:

```powershell
dotnet tool restore
dotnet restore backend/CarLease.Api.sln
```

## Standard API change workflow

### 1. Start from the latest develop branch

```powershell
git switch develop
git pull origin develop
git switch -c feature/1234-descriptive-change
```

Replace the example branch name with the actual work item and a short description.

### 2. Make and test the code change locally

```powershell
dotnet build backend/CarLease.Api.sln --configuration Release
dotnet test backend/CarLease.Api.sln --configuration Release
```

Run the API locally:

```powershell
dotnet run --project backend/CarLease.Api.csproj
```

The default local API URL is `http://localhost:5292`. The local database connection comes from User Secrets.

### 3. Add a database migration when the EF model changes

Create a migration with a meaningful name:

```powershell
dotnet ef migrations add AddOrChangeYourEntity --project backend/CarLease.Api.csproj --startup-project backend/CarLease.Api.csproj
```

Review the generated migration and model snapshot. Apply it to the local development database:

```powershell
dotnet ef database update --project backend/CarLease.Api.csproj --startup-project backend/CarLease.Api.csproj
```

Do not manually edit an already-applied migration. Add a new migration for a subsequent schema change.

### 4. Commit and push the feature branch

```powershell
git status
git add backend tests
git commit -m "Add: concise description of the API change"
git push -u origin HEAD
```

For a documentation-only or frontend change, stage the relevant paths instead.

### 5. Open a pull request

Open a pull request from the feature branch into `develop`. Do not push directly to `develop` or `main`.

The pull-request checks include:

- .NET 10 restore and Release build
- xUnit tests
- EF Core pending-model-change check (confirms the model snapshot and migrations are aligned)
- API container image build validation
- EF migration container image build validation
- React lint and production build when frontend files change

Fix all failed checks, review the code and migration, then merge the pull request into `develop`.

## What happens after merge to develop

The `CarLease Non-Production Deployment` workflow runs automatically after a merge to `develop`.

1. Re-runs backend build, tests and migration validation.
2. Runs frontend lint and build.
3. Builds the combined React + .NET 10 application container.
4. Builds a dedicated EF Core migration container.
5. Pushes both images to ECR with the commit SHA in their tags.
6. Assumes the Non-Production AWS deployment role using GitHub OIDC.
7. Runs the EF Core migration as a one-off Fargate task against `carlease_nonprod`.
8. Only if the migration succeeds, registers the application task definition and creates or updates `carlease-api-nonprod`.
9. Waits for ECS service stability and the ALB target health check.

The workflow uses short-lived AWS credentials. No long-lived AWS keys or database passwords are stored in GitHub.

## Non-Production validation endpoints

Temporary AWS test address (HTTP until the custom domain and HTTPS certificate are enabled):

`http://carlease-shared-alb-153321973.ap-southeast-2.elb.amazonaws.com`

| Purpose | Endpoint |
|---|---|
| API and container health | `/health` |
| PostgreSQL connectivity | `/checkconnectionstring` |
| List SDLC demo rows | `/api/sdlc-demo` |
| Create an SDLC demo row | `POST /api/sdlc-demo` |

Example PowerShell smoke test using dummy data only:

```powershell
$baseUrl = "http://carlease-shared-alb-153321973.ap-southeast-2.elb.amazonaws.com"

Invoke-RestMethod -Method Get -Uri "$baseUrl/health"
Invoke-RestMethod -Method Get -Uri "$baseUrl/checkconnectionstring"

$body = @{
    message = "Non-Production deployment smoke test"
} | ConvertTo-Json

Invoke-RestMethod -Method Post -Uri "$baseUrl/api/sdlc-demo" -ContentType "application/json" -Body $body
Invoke-RestMethod -Method Get -Uri "$baseUrl/api/sdlc-demo"
```

A successful POST returns the created record, including the environment name and creation time. A subsequent GET must return that same record from the Non-Production PostgreSQL database.

Do not submit real customer data or passwords using the temporary HTTP endpoint.

## Database migration and production safety

- Migrations are executed by a separate one-off Fargate task before the API service is updated.
- A failed migration stops the deployment workflow; the API service is not updated.
- The workflow does not run migrations inside the long-running API process.
- Production uses a separate logical database and separate application credentials. This initial cost-conscious architecture shares the RDS instance, but keeps Production and Non-Production databases separate.
- Production schema changes require a separately reviewed release process, backup and migration plan.

## Troubleshooting

- Build or tests fail: inspect the failed Backend API CI job and fix the feature branch before merge.
- EF reports pending model changes: add and commit a migration generated from the current model.
- Migration task fails: inspect CloudWatch log group `/ecs/carlease-db-migrations`.
- API task fails to start: inspect `/ecs/carlease-api-nonprod` and ECS service events.
- ALB target is unhealthy: verify container port 8080, security group ingress from the ALB security group, and `/health` returns HTTP 200.
- Database connection check returns 503: verify the Non-Production secret, ECS task execution role secret access, RDS private security-group rule and database availability.

## Environment map

| Layer | Non-Production | Production |
|---|---|---|
| Branch | `develop` | `main` |
| ECS service | `carlease-api-nonprod` | `carlease-api-prod` |
| Database | `carlease_nonprod` | `carlease_prod` |
| Credentials secret | `carlease/nonprod/database-credentials` | `carlease/prod/database-credentials` |
| Deployment | Automatic after merge to develop | Separate release process |
