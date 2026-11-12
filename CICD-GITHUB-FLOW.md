# CarLease backend CI/CD - GitHub Flow

## Deployment policy

- Feature branches and pull requests: CI only (restore, build, unit tests, Docker build validation).
- Merge to `main`: build and test the API, build one Docker image, tag it with the full 40-character commit SHA, push it to ECR, and automatically deploy that same image to nonprod.
- Production: manual workflow dispatch using an image SHA that already exists in ECR. The production GitHub Environment can require reviewer approval.
- The production workflow never rebuilds the image. It promotes the previously published SHA.
- Database schema migrations remain a separately reviewed and controlled operation. These workflows do not run EF Core migrations.
- The frontend workflow is intentionally unchanged.

## Workflows

| Workflow | Trigger | Responsibility |
|---|---|---|
| `.github/workflows/api-ci.yml` | Feature/bugfix/hotfix push, PR to main, manual CI run | Restore, build, xUnit tests and Docker build validation; no AWS credentials |
| `.github/workflows/api-image.yml` | Push to main, or manual run on main | Build/test, publish immutable SHA image to ECR, then deploy that same image to nonprod |
| `.github/workflows/api-deploy.yml` | Manual workflow dispatch | Promote a selected, previously published SHA to production |

The old `.github/workflows/api.yml` is removed because it independently built and deployed the API on main and would create a competing deployment path.

## AWS resources

- Region: `ap-southeast-2`
- Account: `996661624843`
- ECR repository: `carlease-api`
- ECS cluster: `carlease-cluster`
- Nonprod ECS service: `carlease-api-nonprod`
- Production ECS service: `carlease-api-prod`
- Task execution role: `arn:aws:iam::996661624843:role/carlease/carlease-ecs-task-execution-role`
- Application task role: `arn:aws:iam::996661624843:role/carlease/carlease-api-task-role`

Task definitions are separate:
- `ecs/task-definition-nonprod.json`
- `ecs/task-definition-production.json`

Both use Fargate `awsvpc`, Linux X86_64, container port 8080, and the existing environment-specific CloudWatch log groups. The initial task size is 0.25 vCPU and 512 MiB; adjust after observing actual memory/CPU utilization.

## GitHub configuration

Create GitHub Environments named `nonprod` and `production`.

### Repository Actions secret

| Name | Purpose |
|---|---|
| `AWS_PUBLISH_ROLE_ARN` | IAM OIDC role used only by the main-branch image publishing job |

### Nonprod environment

Set the environment variable `NONPROD_DEPLOY_ENABLED` to `true` only after the nonprod ECS service, database secret, OIDC deploy role and frontend URL are ready. Keep it unset or `false` during initial setup. This lets the first merge publish the immutable ECR image without attempting a deployment. Once the service is ready, manually run the image workflow on `main`; the immutable SHA check will reuse the already-published image. After that, future pushes to `main` deploy nonprod automatically.

Secrets:
- `AWS_NONPROD_ROLE_ARN`: GitHub OIDC role allowed to deploy the nonprod ECS service.
- `DATABASE_SECRET_ARN`: ARN of the nonprod PostgreSQL connection-string secret in AWS Secrets Manager.

Variable:
- `FRONTEND_URL`: exact frontend origin allowed by the API CORS policy.

### Production environment

Secrets:
- `AWS_PRODUCTION_ROLE_ARN`: GitHub OIDC role allowed to deploy the production ECS service.
- `DATABASE_SECRET_ARN`: ARN of the production PostgreSQL connection-string secret in AWS Secrets Manager.

Variable:
- `FRONTEND_URL`: exact production frontend origin allowed by the API CORS policy.

Do not store plaintext AWS credentials or database connection strings in GitHub. The database value belongs in Secrets Manager; GitHub stores only its ARN. The ECS execution role must be granted `secretsmanager:GetSecretValue` for the two approved database secret ARNs (and `kms:Decrypt` if a customer-managed KMS key is used).

## GitHub OIDC trust subjects

Create three separate IAM roles:

1. ECR publisher role. Trust subject:
   `repo:mdshahidkamal/carlease:ref:refs/heads/main`
2. Nonprod deploy role. Trust subject:
   `repo:mdshahidkamal/carlease:environment:nonprod`
3. Production deploy role. Trust subject:
   `repo:mdshahidkamal/carlease:environment:production`

All trust policies should also require audience `sts.amazonaws.com` and constrain `aws:SourceAccount` where applicable.

Permissions:
- Publisher: ECR authorization and image push actions only, scoped to repository `carlease-api`.
- Nonprod deployer: ECR image describe/read as needed, ECS task definition registration and update/describe for `carlease-api-nonprod`, and `iam:PassRole` only for the two CarLease ECS task roles.
- Production deployer: equivalent ECS permissions scoped to `carlease-api-prod`, plus ECR image verification/read and narrowly scoped `iam:PassRole`.
- ECS task execution role: ECR image pull, CloudWatch Logs write, and read access to the nonprod and production Secrets Manager connection-string secrets.
- Application task role: no AWS permissions until the application actually requires AWS APIs.

## First deployment prerequisites

Before merging this PR or triggering a deployment:

1. Create and validate the nonprod and production logical PostgreSQL databases and their separate Secrets Manager connection-string secrets. Do not point both environments at the same logical database.
2. Attach narrowly scoped Secrets Manager read permissions to the ECS execution role.
3. Create the three GitHub OIDC IAM roles and add the corresponding GitHub secret values.
4. Create the GitHub Environments `nonprod` and `production`; set their secrets and `FRONTEND_URL` variables. Add required reviewers to production.
5. Create both ECS Fargate services in `carlease-cluster`, attached to their existing target groups, using the public subnets with public IP assignment enabled (the current VPC has no NAT gateway or private-subnet egress path). The task security group must continue to accept port 8080 only from the ALB security group.
6. Ensure the ALB path rules strip `/nonprod` and `/prod` before forwarding. The API exposes `/health`, so the existing target-group health check must continue to work with the configured URL rewrite.
7. Validate CI on this PR. Only merge after OIDC roles, secrets, services and workflow checks are ready.

## GitHub Flow

1. Create a short-lived feature branch from `main`.
2. Push code; CI runs on the feature branch.
3. Open a pull request to `main`; CI runs against the PR.
4. Require the backend CI check and code review before merge.
5. Merge to `main`; GitHub Actions builds and publishes `carlease-api:<full-commit-sha>`, then deploys that exact URI to nonprod.
6. After nonprod verification, manually run `Backend API - Promote image to production` with the same 40-character SHA. GitHub's production Environment review gate applies before deployment.

## Database change controls

The workflows do not run `dotnet ef database update`. For schema-changing releases:

1. Generate and review an EF Core migration.
2. Back up the database and test against a representative environment.
3. Apply the migration through an authorized process.
4. Deploy an API version compatible with the resulting schema.

Prefer backward-compatible expand/contract migrations for rolling ECS deployments.

## Validation and limitations

- The repository targets .NET 8. GitHub Actions uses the .NET 8 SDK; local Docker Desktop is not required because image builds run on GitHub-hosted Ubuntu runners.
- The API health endpoint is `/health` and the Dockerfile listens on port 8080.
- Task definitions begin at 0.25 vCPU / 512 MiB; verify memory headroom during load tests.
- The existing ALB is currently HTTP-only. HTTPS and a valid ACM certificate are required before production customer traffic.
- This PR does not create AWS databases, Secrets Manager values, GitHub Environments, OIDC roles, ECS services, or deploy anything.
