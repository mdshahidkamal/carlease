# CarLease GitHub Flow

The authoritative .NET API development and Non-Production deployment procedure is maintained in:

- [SDLC-DOTNET-API.md](docs/SDLC-DOTNET-API.md)

## Summary

1. Start a short-lived feature branch from `develop`.
2. Make the API change locally, run build and unit tests, and add an EF Core migration when the data model changes.
3. Commit and push the feature branch.
4. Open a pull request targeting `develop`.
5. GitHub Actions validates .NET build, unit tests, EF migration consistency, Docker image builds and React checks when relevant.
6. Review and merge the pull request into `develop`.
7. A merge to `develop` triggers the AWS Non-Production release workflow.
8. The release workflow bootstraps/validates the logical databases, applies the EF Core migration in a one-off Fargate task, deploys the API container and verifies ALB target health.

## Branch policy

| Branch | Purpose | Deployment |
|---|---|---|
| `feature/*`, `bugfix/*`, `hotfix/*` | Short-lived changes | CI only |
| `develop` | Integration and Non-Production release line | Automatic Non-Production deployment after merge |
| `main` | Production release line | Not automatically deployed by the Non-Production workflow |

Do not commit database credentials, AWS access keys, or other secrets to Git. Use .NET User Secrets for local development and AWS Secrets Manager for deployed environments.

Production promotion is a separate, reviewed release activity and is intentionally outside the current Non-Production workflow.
