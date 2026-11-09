# CarLease backend CI/CD - GitHub Flow

## Scope

This document describes CI, image publishing and ECS deployment for the ASP.NET Core API under `backend/`.

The repository's frontend workflow is unchanged. PostgreSQL schema migrations remain a separately controlled activity; this pipeline does not execute `dotnet ef database update`.

## Workflows

| Workflow | Trigger | Responsibility |
|---|---|---|
| `.github/workflows/api-ci.yml` | Push to feature/bugfix/hotfix branches and pull requests into main | Restore, build, run xUnit tests and validate Docker build |
| `.github/workflows/api-image.yml` | Push to main when API or ECS task configuration changes | Re-run CI gates, build Docker image, push SHA-tagged image to ECR |
| `.github/workflows/api-deploy.yml` | Manual workflow dispatch | Deploy a previously published image SHA to ECS Fargate |

## GitHub Flow

1. Create a short-lived branch from `main`, e.g. `feature/booking-page`.
2. Push commits. Backend CI runs for feature branches.
3. Open a pull request targeting `main`. CI runs again against the PR merge candidate.
4. Require CI checks and code review before merging.
5. Merging to `main` publishes a Docker image tagged with the full commit SHA.
6. Deploy the selected image using the manual `Backend API - Deploy to ECS` workflow.
7. Verify ECS service stability and the application `/health` endpoint.

Do not deploy from feature branches or pull-request workflows.

## GitHub repository configuration

### Actions secrets

Keep the existing AWS secrets:

- `AWS_ROLE_ARN`: IAM role trusted by GitHub OIDC
- `AWS_REGION`: AWS region (may be a repository variable instead)
- `ECS_CLUSTER`: ECS cluster
- `ECS_SERVICE`: ECS service
- `ECS_EXECUTION_ROLE_ARN`: task execution role
- `ECS_TASK_ROLE_ARN`: application task role
- `DATABASE_SECRET_ARN`: Secrets Manager ARN with the PostgreSQL connection string
- `FRONTEND_URL`: allowed frontend origin

The deploy workflow uses GitHub Environment `production`. Create this environment under Settings -> Environments and configure required reviewers if production approval is desired.

### OIDC trust

The IAM role trust policy should constrain the subject to this repository and the production deployment environment, for example:

```json
{
  "Effect": "Allow",
  "Principal": {
    "Federated": "arn:aws:iam::<ACCOUNT_ID>:oidc-provider/token.actions.githubusercontent.com"
  },
  "Action": "sts:AssumeRoleWithWebIdentity",
  "Condition": {
    "StringEquals": {
      "token.actions.githubusercontent.com:aud": "sts.amazonaws.com",
      "token.actions.githubusercontent.com:sub": "repo:mdshahidkamal/carlease:environment:production"
    }
  }
}
```

This environment-scoped trust applies to the manual deploy workflow. The image-publishing workflow uses a branch-scoped subject for `main`; either create a separate least-privilege IAM role for publishing or deliberately extend the trust policy to include `repo:mdshahidkamal/carlease:ref:refs/heads/main`.

Do not give the CI test workflow AWS permissions.

### Branch protection / ruleset for main

Configure a ruleset or branch protection for `main`:

- Require a pull request before merging.
- Require at least one approval (adjust for a solo-maintainer repository).
- Require the backend CI check.
- Require branches to be up to date where practical.
- Block force pushes and branch deletion.
- Do not allow direct pushes to `main`.

Use the exact check name displayed by GitHub Actions after the first successful run.

## Image and deployment flow

Images are immutable by commit SHA:

`<account>.dkr.ecr.<region>.amazonaws.com/carlease-api:<40-char-commit-sha>`

The deploy workflow validates the SHA and confirms that the image exists in ECR before registering a task definition and updating ECS.

For production, configure ECS service health checks through the load balancer using `/health`, and verify the target group uses the container's port 8080.

## Database changes

Schema migrations are not run by the API deployment workflow. Before a schema-changing release:

1. Generate and review an EF Core migration.
2. Back up the database and test the migration against a representative environment.
3. Apply it through an authorized migration process with the correct production connection secret.
4. Deploy the API version compatible with the resulting schema.

Prefer backward-compatible expand/contract migrations to avoid downtime during rolling ECS deployments.

## Notes

- The project currently targets .NET 8. Plan an upgrade to a supported LTS release.
- The test project uses SQLite in-memory for its current database connectivity test; CI does not require RDS access.
- The frontend workflow remains independent and is not modified here.
- The existing API deployment workflow is retained temporarily for reference during this staged transition. Disable or remove it after validating the new image and deploy workflows, otherwise it remains an alternate production deployment path.
