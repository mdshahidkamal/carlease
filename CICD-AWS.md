# CarLease AWS deployment setup

## Independent delivery phases

This project uses three separate development and release phases:

### 1. React frontend

Changes under `frontend/` trigger `.github/workflows/frontend.yml`. The workflow builds React, uploads `frontend/dist/` to S3, and invalidates CloudFront.

### 2. .NET API

Changes under `backend/` trigger `.github/workflows/api.yml`. The workflow builds and pushes the API image to ECR, generates an ECS task definition, and updates the ECS service.

### 3. PostgreSQL

PostgreSQL and EF Core migrations are deployed manually. Do not trigger database migrations from frontend or API CI/CD workflows.

Before manually updating an environment, create and review a migration, back up the database, and run:

```powershell
dotnet ef database update `
  --project backend/CarLease.Api.csproj `
  --startup-project backend/CarLease.Api.csproj
```

Use a production connection string supplied securely through local environment configuration or user secrets. Never commit it to the repository.

## Required GitHub Actions secrets

Add these under **Repository → Settings → Secrets and variables → Actions**:

| Secret | Example or purpose |
|---|---|
| `AWS_ROLE_ARN` | IAM role ARN trusted by GitHub OIDC |
| `AWS_REGION` | `us-east-1` |
| `ECS_CLUSTER` | ECS cluster name |
| `ECS_SERVICE` | ECS service name |
| `ECS_EXECUTION_ROLE_ARN` | ECS task execution role ARN |
| `ECS_TASK_ROLE_ARN` | ECS task role ARN |
| `DATABASE_SECRET_ARN` | Secrets Manager ARN containing the PostgreSQL connection string |
| `FRONTEND_URL` | `https://your-domain.example` |
| `VITE_API_BASE_URL` | Public HTTPS API URL |
| `VITE_GOOGLE_MAPS_API_KEY` | Browser-restricted Google Maps key |
| `FRONTEND_BUCKET` | Public CloudFront-backed S3 bucket name |
| `CLOUDFRONT_DISTRIBUTION_ID` | CloudFront distribution ID |

## Required AWS resources

- ECR repository named `carlease-api`
- ECS Fargate cluster and service named according to the secrets above
- Application Load Balancer health check path `/health`
- RDS PostgreSQL database
- Secrets Manager database secret
- S3 bucket for the React build
- CloudFront distribution using the S3 origin
- ACM certificate and Route 53 DNS records

## Database secret value

Store a single Npgsql connection string in Secrets Manager:

```text
Host=your-rds-host;Port=5432;Database=carlease;Username=carlease;Password=your-password
```

## GitHub OIDC setup

The IAM role configured by `AWS_ROLE_ARN` must trust `token.actions.githubusercontent.com` and restrict access to this repository and its `main` branch.

Required AWS role capabilities include ECR push/read, ECS task and service deployment, CloudFront invalidation, and S3 synchronization.

## Database migrations

The pipeline currently deploys the API but does not automatically migrate PostgreSQL. Before the first deployment, generate and apply the EF Core migration using an authorized administration environment or a separate ECS migration task.

Google Maps keys and React API URLs are embedded into the frontend bundle and are visible to browsers; restrict the Maps key by domain and use HTTPS in production.
