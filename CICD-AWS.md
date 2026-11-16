# CarLease AWS deployment configuration

## Deployment architecture

CarLease packages the React frontend and .NET API into one Docker image. The Dockerfile builds the React application, copies the built files into the .NET API container's `wwwroot`, and serves the frontend and API from the same ECS-hosted application.

The frontend is not independently deployed to S3 by the current `frontend.yml` workflow. That workflow performs CI validation only. The API image workflow builds and publishes the combined frontend/API image.

## Git and deployment flow

The approved branch promotion model is:

```text
feature/* -> develop -> nonprod -> main -> production
```

- Pull requests into `develop` run backend and frontend CI.
- A merge to `develop` builds and publishes an immutable ECR image, then deploys it to nonprod when the `NONPROD_DEPLOY_ENABLED` environment variable is `true`.
- After nonprod validation, a pull request from `develop` to `main` promotes the same immutable ECR image to production after the production GitHub Environment approval. The production workflow runs on the resulting main-branch update and verifies that the commit is associated with a merged PR from `develop`.
- Database migrations remain a separate, reviewed operation.

See [CICD-GITHUB-FLOW.md](CICD-GITHUB-FLOW.md) for workflow triggers, GitHub configuration and release steps.

## AWS resources used by the workflows

- Region: `ap-southeast-2`
- Account: `996661624843`
- ECR repository: `carlease-api`
- ECS cluster: `carlease-cluster`
- Non-production ECS service: `carlease-api-nonprod`
- Production ECS service: `carlease-api-prod`
- ECS task execution role: `arn:aws:iam::996661624843:role/carlease/carlease-ecs-task-execution-role`
- ECS application task role: `arn:aws:iam::996661624843:role/carlease/carlease-api-task-role`

Task definitions:
- `ecs/task-definition-nonprod.json`
- `ecs/task-definition-production.json`

Both task definitions use Fargate, `awsvpc`, Linux X86_64 and container port 8080. The API exposes `/health`.

## GitHub Actions secrets and environments

Create GitHub Environments named `nonprod` and `production`.

### Repository-level secret

| Secret | Purpose |
|---|---|
| `AWS_PUBLISH_ROLE_ARN` | OIDC role used to push immutable images to ECR |

The publisher role's OIDC trust policy must allow:

```text
repo:mdshahidkamal/carlease:ref:refs/heads/develop
```

### nonprod environment

Secrets:
- `AWS_NONPROD_ROLE_ARN`
- `DATABASE_SECRET_ARN`

Variables:
- `NONPROD_DEPLOY_ENABLED` — set to `true` only after nonprod is ready
- `FRONTEND_URL`

### production environment

Secrets:
- `AWS_PRODUCTION_ROLE_ARN`
- `DATABASE_SECRET_ARN`

Variables:
- `DB_HOST`
- `DB_NAME`
- `DB_PORT` (optional; defaults to 5432)
- `FRONTEND_URL`

Configure required reviewers for the production Environment. The production OIDC subject is:

```text
repo:mdshahidkamal/carlease:environment:production
```

The nonprod deploy role's OIDC subject is:

```text
repo:mdshahidkamal/carlease:environment:nonprod
```

Ensure each role has least-privilege ECR/ECS permissions and narrowly scoped `iam:PassRole` permissions for the CarLease ECS task roles.

## Database credentials

The GitHub Environment secret `DATABASE_SECRET_ARN` must identify an AWS Secrets Manager JSON secret with `username` and `password` keys. ECS injects those JSON keys as `DB_USERNAME` and `DB_PASSWORD`.

The database host, database name and port are provided as task environment variables. The ECS task execution role must be allowed to read the relevant Secrets Manager secret and use its KMS key if customer-managed encryption is configured.

Never commit credentials, connection strings or secret values to Git.

## Database migration controls

The deployment workflows do not run EF Core migrations. For schema changes, generate and review the migration, back up the database, test the migration, and apply it through an authorized database administration process before deploying a compatible API version.

## Infrastructure boundary

The workflows deploy application images and ECS task definitions. They do not create or change:
- RDS databases or PostgreSQL schemas
- Secrets Manager secrets or secret values
- IAM roles or OIDC trust policies
- ECS clusters, VPCs, subnets or security groups
- Application Load Balancers, listeners or target groups
- DNS records or TLS certificates

These AWS resources remain independently managed. Validate their readiness before setting `NONPROD_DEPLOY_ENABLED=true) or merging a production release.
