# CarLease Git branching and CI/CD

## 1. Branching strategy

CarLease uses a three-level promotion model:

| Branch | Purpose | Allowed change path | Deployment |
|---|---|---|---|
| `feature/*`, `bugfix/*`, `hotfix/*` | Developer work and local testing | Developer commits and pushes to their branch | No shared deployment |
| `develop` | Integration and non-production validation | Pull requests from feature branches | Non-production |
| `main` | Production release baseline | Pull request from `develop` after non-production sign-off | Production |

Do not commit or push directly to `develop` or `main`. Merge through pull requests.

## 2. Continuous integration

The following CI workflows run on pushes to feature, bugfix, hotfix, develop and main branches, and on pull requests targeting develop or main:

| Workflow | Responsibility |
|---|---|
| `.github/workflows/api-ci.yml` | .NET restore, build, unit tests and Docker image build validation |
| `.github/workflows/frontend.yml` | Frontend dependency install, lint and production build |

CI does not require AWS credentials and does not deploy.

The backend Dockerfile builds the React frontend and copies its build output into the .NET API image under `wwwroot`. The frontend and API are therefore released together as one ECS image; `frontend.yml` is a validation workflow, not a separate S3 deployment.

## 3. Non-production deployment

Workflow: `.github/workflows/api-image.yml`

Trigger:
- Every push/merge to `develop`.
- Manual dispatch is available for an operator-run build/deployment from `develop`.

Actions:
1. Restore, build and test the .NET solution.
2. Build the full-stack Docker image.
3. Tag the image with the full 40-character develop commit SHA.
4. Push the immutable image to ECR repository `carlease-api`.
5. When GitHub Environment variable `NONPROD_DEPLOY_ENABLED` is `true`, deploy the same image to ECS service `carlease-api-nonprod`.

The deployment uses the GitHub Environment named `nonprod`. Keep the deployment gate false/unset until the environment's OIDC role, database secret, frontend URL and ECS service configuration have been validated.

## 4. Production promotion

Workflow: `.github/workflows/api-production.yml`

Trigger:
- A push/update to `main` after a pull request merge.
- The workflow queries GitHub for the pull request associated with the pushed commit and proceeds only when the merged PR source is exactly `develop` and the base is `main`.

Actions:
1. Resolve the merged `develop`-to-`main` PR associated with the main-branch commit.
2. Use that PR's head SHA (the tested `develop` commit) as the ECR image tag.
3. Verify that this immutable image already exists in ECR.
4. Do not rebuild the image.
5. Generate and register the production ECS task definition.
6. Update the existing ECS service `carlease-api-prod`.
7. Wait for ECS service stability.

The job uses the GitHub Environment named `production`. Configure required reviewers on this environment so production deployment pauses for approval. The push-based trigger also supports the first release that introduces this workflow into `main`. The workflow intentionally fails if the production ECS service is not already ACTIVE; it does not create or modify AWS networking, load balancers, databases or other infrastructure.

A release PR from `develop` to `main` must only be merged after non-production testing and sign-off. Merging that PR initiates production promotion.

## 5. GitHub Actions configuration

### Repository-level secret

| Name | Purpose |
|---|---|
| `AWS_PUBLISH_ROLE_ARN` | OIDC role that can publish the image to ECR |

The publisher role's AWS IAM OIDC trust policy must allow this subject:

```text
repo:mdshahidkamal/carlease:ref:refs/heads/develop
```

The previous deployment flow used main for image publishing. Update the publisher role trust subject before enabling the new develop deployment.

### GitHub Environment: nonprod

Secrets:
- `AWS_NONPROD_ROLE_ARN`
- `DATABASE_SECRET_ARN`

Variables:
- `NONPROD_DEPLOY_ENABLED` — set to `true` only when nonprod is ready
- `FRONTEND_URL`

The publisher role also needs ECR push permissions. The nonprod deploy role needs narrowly scoped ECS deployment permissions and `iam:PassRole` for the CarLease task roles.

### GitHub Environment: production

Secrets:
- `AWS_PRODUCTION_ROLE_ARN`
- `DATABASE_SECRET_ARN`

Variables:
- `DB_HOST`
- `DB_NAME`
- `DB_PORT` — optional; defaults to `5432`
- `FRONTEND_URL`

Configure at least one required reviewer for this environment. The production deploy role's OIDC subject should be:

```text
repo:mdshahidkamal/carlease:environment:production
```

The nonprod deploy role's OIDC subject should be:

```text
repo:mdshahidkamal/carlease:environment:nonprod
```

All three roles should also require the audience `sts.amazonaws.com` and follow least-privilege IAM permissions.

## 6. Database secret format

The deployment workflows expect `DATABASE_SECRET_ARN` to identify an AWS Secrets Manager JSON secret containing:

```json
{
  "username": "database-user",
  "password": "database-password"
}
```

The ECS task receives the JSON keys as `DB_USERNAME` and `DB_PASSWORD`. Host, database name and port are configured separately as task environment variables. Do not store database passwords or full connection strings in GitHub.

## 7. Database migration controls

The CI/CD workflows do not run EF Core migrations or `dotnet ef database update`.

For schema changes:
1. Generate and review the EF Core migration.
2. Back up the target database.
3. Test the migration against a representative environment.
4. Apply it using an authorized database administration process.
5. Deploy an API version compatible with the target schema.

Prefer backward-compatible expand/contract migrations for rolling ECS deployments.

## 8. Current AWS deployment identifiers

- Region: `ap-southeast-2`
- AWS account: `996661624843`
- ECR repository: `carlease-api`
- ECS cluster: `carlease-cluster`
- Non-production ECS service: `carlease-api-nonprod`
- Production ECS service: `carlease-api-prod`
- Task execution role: `arn:aws:iam::996661624843:role/carlease/carlease-ecs-task-execution-role`
- Application task role: `arn:aws:iam::996661624843:role/carlease/carlease-api-task-role`

Task definition files:
- `ecs/task-definition-nonprod.json`
- `ecs/task-definition-production.json`

The workflows do not create RDS databases, Secrets Manager secrets, IAM roles, ECS clusters, load balancers, target groups, subnets or security groups. Those remain separately managed AWS resources.
