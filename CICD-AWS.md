# CarLease AWS deployment architecture

## Target AWS environment

- AWS account: Make It Happen (`996661624843`)
- Region: Asia Pacific (Sydney), `ap-southeast-2`
- ECS cluster: `carlease-cluster`
- ECR repository: `carlease-api`
- Shared Application Load Balancer: `carlease-shared-alb`
- PostgreSQL instance: `carlease-postgres`

## Application packaging

The React frontend is built with Node.js 22 and copied into the ASP.NET Core `wwwroot` directory. The .NET 10 API serves both the React single-page application and API endpoints from one container.

- Website routes: `/` and `/booking`
- API routes: `/api/*`
- Health endpoint: `/health`
- Container port: `8080`

The Docker build context is the repository root:

```bash
docker build --file backend/Dockerfile --tag carlease-api:local .
```

## Environments

| Environment | Git branch | ECS service | Task definition family | Database |
|---|---|---|---|---|
| Non-Production | `develop` | `carlease-api-nonprod` | `carlease-api-nonprod` | `carlease_nonprod` |
| Production | `main` | `carlease-api-prod` | `carlease-api-prod` | `carlease_prod` |

Both environments use separate application tasks, separate database names and separate database credentials. They initially share one RDS PostgreSQL instance and one ALB to control costs.

## CI/CD

- Backend CI: `.github/workflows/api-ci.yml`
- Frontend CI: `.github/workflows/frontend.yml`
- AWS deployment: `.github/workflows/deploy-aws.yml`

Pushes to `develop` deploy Non-Production. Pushes to `main` deploy Production. GitHub Actions assumes a branch-restricted AWS IAM role through OIDC; no long-lived AWS access keys are stored in GitHub.

## Runtime secrets

The ECS task execution role retrieves the environment-specific connection string from Secrets Manager at task start:

- `carlease/nonprod/database-credentials`
- `carlease/prod/database-credentials`

The ECS task receives the database username and password as `Database__Username` and `Database__Password`; the application builds its Npgsql connection string using the non-secret host, port and database settings. Never put database credentials in source code, Docker build arguments, frontend `VITE_` variables, task-definition plaintext environment values, or GitHub Actions logs.

A change to a Secrets Manager value requires an ECS service deployment so new tasks receive the updated secret.

## Networking and access

- The ALB accepts public HTTP (port 80) for temporary functional testing.
- ECS tasks run in public subnets with public IPs to avoid a NAT Gateway charge; their security group only accepts application traffic from the ALB security group.
- RDS is in private database subnets and has PubliclyAccessible disabled.
- RDS port 5432 accepts traffic only from the CarLease ECS security group.
- ACM certificate is requested for `royallimoservice.us`, `www.royallimoservice.us` and `dev.royallimoservice.us`. DNS validation is pending in GoDaddy.
- Until DNS validation is completed, use the ALB's AWS-generated hostname for HTTP testing only. Do not enter real customer information or passwords on that temporary HTTP endpoint.

## Database migrations

Do not automatically run EF Core migrations in the API container startup or the standard application deployment workflow.

Create and review migrations separately, take a backup before Production migration, then apply the migration using a controlled one-off task or authorized database administration environment.

## Current temporary test hostname

`http://carlease-shared-alb-153321973.ap-southeast-2.elb.amazonaws.com`

The hostname will serve the application when the Non-Production ECS service is deployed and its target passes the ALB `/health` check.
