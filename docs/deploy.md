# PantryTales Backend - AWS Deployment Guide

This guide covers deploying the PantryTales .NET backend to AWS with a custom domain.

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Option 1: AWS App Runner (Recommended)](#option-1-aws-app-runner-recommended)
4. [Option 2: AWS Amplify with Docker](#option-2-aws-amplify-with-docker)
5. [Custom Domain Setup](#custom-domain-setup)
6. [GitHub Actions CI/CD](#github-actions-cicd)
7. [Environment Variables](#environment-variables)
8. [Monitoring & Troubleshooting](#monitoring--troubleshooting)

---

## Overview

### Why AWS App Runner over Amplify?

| Feature | AWS Amplify Hosting | AWS App Runner |
|---------|---------------------|----------------|
| Primary Use | Frontend/SSR apps | Backend APIs & containers |
| .NET Support | Limited (via Docker) | Native Docker support |
| Auto-scaling | Yes | Yes |
| Custom domains | Yes | Yes |
| Complexity | Higher for .NET | Lower |
| Cost | Pay per request | Pay per vCPU/memory |

**Recommendation**: Use **AWS App Runner** for the .NET backend. It's purpose-built for containerized web services and APIs.

---

## Prerequisites

### 1. Install Required Tools

#### AWS CLI

<details>
<summary><b>macOS</b></summary>

```bash
# Using Homebrew (recommended)
brew install awscli

# Or download the installer
curl "https://awscli.amazonaws.com/AWSCLIV2.pkg" -o "AWSCLIV2.pkg"
sudo installer -pkg AWSCLIV2.pkg -target /
```
</details>

<details>
<summary><b>Windows</b></summary>

```powershell
# Using winget
winget install Amazon.AWSCLI

# Or download the MSI installer from:
# https://awscli.amazonaws.com/AWSCLIV2.msi
```
</details>

<details>
<summary><b>Linux</b></summary>

```bash
# Download and install
curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip"
unzip awscliv2.zip
sudo ./aws/install

# Or using snap
sudo snap install aws-cli --classic
```
</details>

#### Docker

<details>
<summary><b>macOS</b></summary>

```bash
# Using Homebrew
brew install --cask docker

# Or download Docker Desktop from:
# https://www.docker.com/products/docker-desktop/
```
</details>

<details>
<summary><b>Windows</b></summary>

Download Docker Desktop from: https://www.docker.com/products/docker-desktop/

> **Note**: Requires WSL 2 backend. Follow the [WSL 2 installation guide](https://docs.microsoft.com/en-us/windows/wsl/install) if needed.
</details>

<details>
<summary><b>Linux</b></summary>

```bash
# Ubuntu/Debian
sudo apt-get update
sudo apt-get install docker.io docker-compose
sudo systemctl start docker
sudo systemctl enable docker
sudo usermod -aG docker $USER  # Allow running without sudo

# Or install Docker Desktop for Linux:
# https://docs.docker.com/desktop/install/linux-install/
```
</details>

#### Verify Installations

```bash
aws --version    # Should show aws-cli/2.x.x
docker --version # Should show Docker version 2x.x.x
```

### 2. Configure AWS CLI

```bash
# Configure with your AWS credentials
aws configure

# You'll be prompted for:
# - AWS Access Key ID
# - AWS Secret Access Key
# - Default region (e.g., ap-southeast-2 for Sydney)
# - Default output format (json)
```

### 3. Create IAM User for Deployment

#### Do I need to create an IAM user?

| Situation | Action |
|-----------|--------|
| **School/Organization provided account** | **No** - Use the credentials they gave you |
| **Have existing IAM user with admin access** | **No** - Use existing credentials |
| **Using AWS SSO / IAM Identity Center** | **No** - Use `aws sso login` instead |
| **New to AWS** (personal account, no credentials) | **Yes** - Create an IAM user |
| **Have root account only** | **Yes** - Never use root for deployments |

> **Using a school-provided account?** Your school likely gave you an Access Key ID and Secret Access Key. Just run `aws configure` and enter those credentials. Skip the IAM user creation steps below.

#### Creating an IAM User (Step-by-Step)

1. Go to [IAM Console](https://console.aws.amazon.com/iam/) → Users → **Create user**
2. Enter username (e.g., `pantrytales-deployer`)
3. Select **Attach policies directly** and add:
   - `AmazonEC2ContainerRegistryFullAccess` - Push Docker images
   - `AWSAppRunnerFullAccess` - Create/manage App Runner services
4. Click **Create user**
5. Go to the user → **Security credentials** → **Create access key**
6. Select **Command Line Interface (CLI)** → Create
7. **Save the Access Key ID and Secret Access Key** (shown only once!)

Then configure AWS CLI:
```bash
aws configure
# Enter the Access Key ID and Secret Access Key from step 7
```

#### Verify Access

```bash
# Should return your account info without errors
aws sts get-caller-identity
```

---

## Option 1: AWS App Runner (Recommended)

### Step 1: Create ECR Repository

```bash
# Set your AWS region
export AWS_REGION=ap-southeast-2

# Create ECR repository
aws ecr create-repository \
    --repository-name pantrytales-backend \
    --region $AWS_REGION

# Get the repository URI (save this!)
aws ecr describe-repositories \
    --repository-names pantrytales-backend \
    --query 'repositories[0].repositoryUri' \
    --output text
```

### Step 2: Build and Push Docker Image

```bash
# Navigate to project root
cd /path/to/PantryTales

# Set variables (replace with your actual values after running Step 1)
export AWS_REGION=ap-southeast-2
export AWS_ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
export ECR_URI=$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/pantrytales-backend

# Verify the ECR URI is correct (IMPORTANT - check this before pushing!)
echo "ECR URI: $ECR_URI"
# Should show something like: 123456789012.dkr.ecr.ap-southeast-2.amazonaws.com/pantrytales-backend

# Login to ECR
aws ecr get-login-password --region $AWS_REGION | \
    docker login --username AWS --password-stdin \
    $AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com

# Build the image (IMPORTANT: specify platform for Apple Silicon Macs)
docker build --platform linux/amd64 -t pantrytales-backend -f backend/Dockerfile .

# Tag for ECR
docker tag pantrytales-backend:latest $ECR_URI:latest

# Push to ECR
docker push $ECR_URI:latest
```

> **Troubleshooting**: If push fails with "repository does not exist", verify your `ECR_URI` variable is correct. Run `echo $ECR_URI` and check for typos. The repository name should be exactly `pantrytales-backend`.

**Alternative (without variables):**

If you're having issues with environment variables, use explicit values:

```bash
# Tag and push in one command (replace ACCOUNT_ID with your AWS account ID)
docker tag pantrytales-backend:latest ACCOUNT_ID.dkr.ecr.ap-southeast-2.amazonaws.com/pantrytales-backend:latest && \
docker push ACCOUNT_ID.dkr.ecr.ap-southeast-2.amazonaws.com/pantrytales-backend:latest
```

### Step 3: Create App Runner Service (Console)

1. Go to [AWS App Runner Console](https://console.aws.amazon.com/apprunner/)
2. Click **Create service**
3. Configure source:
   - **Repository type**: Container registry
   - **Provider**: Amazon ECR
   - **Container image URI**: Select your `pantrytales-backend` repository
   - **Deployment trigger**: Automatic (recommended)
   - **ECR access role**: Select **"Create new service role"** (this automatically creates `AppRunnerECRAccessRole` which allows App Runner to pull images from ECR)
4. Configure service:
   - **Service name**: `pantrytales-api`
   - **CPU**: 1 vCPU
   - **Memory**: 2 GB
   - **Port**: 8080
5. Configure environment variables (see [Environment Variables](#environment-variables))
6. Configure health check:
   - **Protocol**: HTTP ⚠️ **IMPORTANT: Must be HTTP, not TCP!**
   - **Path**: `/health`
   - **Interval**: 10 seconds
   - **Timeout**: 5 seconds
   - **Healthy threshold**: 1
   - **Unhealthy threshold**: 5

   > **⚠️ Critical**: The health check protocol MUST be set to **HTTP** (not TCP). The backend exposes an HTTP endpoint at `/health` that returns JSON. Using TCP will cause deployment failures because App Runner won't receive a valid health response.

7. Click **Create & deploy**

### Step 3 (Alternative): Create App Runner Service (CLI)

```bash
# Create the service using the provided script
./scripts/deploy-apprunner.sh
```

Or manually:

```bash
# Create App Runner service
aws apprunner create-service \
    --service-name pantrytales-api \
    --source-configuration '{
        "ImageRepository": {
            "ImageIdentifier": "'$ECR_URI':latest",
            "ImageRepositoryType": "ECR",
            "ImageConfiguration": {
                "Port": "8080",
                "RuntimeEnvironmentVariables": {
                    "ASPNETCORE_ENVIRONMENT": "Production",
                    "ASPNETCORE_URLS": "http://+:8080"
                }
            }
        },
        "AutoDeploymentsEnabled": true,
        "AuthenticationConfiguration": {
            "AccessRoleArn": "arn:aws:iam::'$(aws sts get-caller-identity --query Account --output text)':role/AppRunnerECRAccessRole"
        }
    }' \
    --instance-configuration '{
        "Cpu": "1024",
        "Memory": "2048"
    }' \
    --health-check-configuration '{
        "Protocol": "HTTP",
        "Path": "/health",
        "Interval": 10,
        "Timeout": 5,
        "HealthyThreshold": 1,
        "UnhealthyThreshold": 5
    }' \
    --region $AWS_REGION
```

---

## Option 2: AWS Amplify with Docker

> **Note**: This is more complex and not recommended for .NET backends, but included for completeness.

### Step 1: Create amplify.yml

The `amplify.yml` file in the repository root configures the build:

```yaml
version: 1
backend:
  phases:
    build:
      commands:
        - cd backend
        - dotnet publish -c Release -o ./publish
artifacts:
  baseDirectory: backend/publish
  files:
    - '**/*'
```

### Step 2: Connect Repository to Amplify

1. Go to [AWS Amplify Console](https://console.aws.amazon.com/amplify/)
2. Click **New app** > **Host web app**
3. Select **GitHub** and authorize
4. Select your repository and branch
5. Amplify will detect the `amplify.yml` configuration

> **Important**: Amplify Hosting doesn't natively support running .NET backends. For backends, use AWS App Runner or AWS Amplify Backend (which requires Lambda, not supported for .NET).

---

## Custom Domain Setup

### For App Runner

#### Step 1: Request SSL Certificate (ACM)

```bash
# Request certificate for your domain
aws acm request-certificate \
    --domain-name api.yourdomain.com \
    --validation-method DNS \
    --region $AWS_REGION

# Note the CertificateArn from the output
```

#### Step 2: Validate Certificate via DNS

1. Go to [ACM Console](https://console.aws.amazon.com/acm/)
2. Find your certificate (pending validation)
3. Click on it and expand "Domains"
4. Add the CNAME record to your DNS provider:
   - If using Cloudflare: Add the CNAME record in DNS settings
   - If using Route 53: Click "Create records in Route 53"

#### Step 3: Associate Custom Domain

```bash
# Get your App Runner service ARN
SERVICE_ARN=$(aws apprunner list-services \
    --query "ServiceSummaryList[?ServiceName=='pantrytales-api'].ServiceArn" \
    --output text)

# Associate custom domain
aws apprunner associate-custom-domain \
    --service-arn $SERVICE_ARN \
    --domain-name api.yourdomain.com \
    --region $AWS_REGION
```

#### Step 4: Add DNS Records

After association, App Runner provides DNS targets. Add these records:

### Cloudflare DNS Setup (Step-by-Step)

Since your domain `yourdomain.com` is on Cloudflare, follow these steps:

#### 1. Get App Runner Domain Target

After associating the custom domain in App Runner, you'll get output like:
```
{
    "DNSTarget": "xxxxxx.ap-southeast-2.awsapprunner.com",
    "CustomDomain": {
        "DomainName": "api.yourdomain.com",
        "Status": "pending_certificate_dns_validation",
        "CertificateValidationRecords": [
            {
                "Name": "_xxxxxx.api.yourdomain.com",
                "Type": "CNAME",
                "Value": "_xxxxxx.xxxxxx.acm-validations.aws"
            }
        ]
    }
}
```

#### 2. Add DNS Records in Cloudflare

Go to [Cloudflare Dashboard](https://dash.cloudflare.com/) → Select `yourdomain.com` → DNS → Records

**Add these records:**

| Type | Name | Target | Proxy Status |
|------|------|--------|--------------|
| CNAME | `api` | `xxxxxx.ap-southeast-2.awsapprunner.com` | **DNS only** (gray cloud) |
| CNAME | `_xxxxxx.api` | `_xxxxxx.xxxxxx.acm-validations.aws` | **DNS only** (gray cloud) |

> **Important**: Keep proxy status as "DNS only" (gray cloud icon). App Runner provides its own SSL certificate and doesn't work well with Cloudflare's proxy.

#### 3. Wait for Validation

- Certificate validation typically takes 5-30 minutes
- Check status: `aws apprunner describe-custom-domains --service-arn $SERVICE_ARN`
- Once status changes from `pending_certificate_dns_validation` to `active`, you're done!

#### 4. Verify It Works

```bash
curl https://api.yourdomain.com/health
# Should return: {"status":"healthy","timestamp":"..."}
```

**For Route 53 (alternative):**
```bash
# App Runner provides the target, add it to your hosted zone
aws route53 change-resource-record-sets \
    --hosted-zone-id YOUR_ZONE_ID \
    --change-batch '{
        "Changes": [{
            "Action": "CREATE",
            "ResourceRecordSet": {
                "Name": "api.yourdomain.com",
                "Type": "CNAME",
                "TTL": 300,
                "ResourceRecords": [{"Value": "YOUR_APP_RUNNER_URL"}]
            }
        }]
    }'
```

### Your Domain Setup (api.yourdomain.com)

Both API endpoints and admin dashboard are served from the same .NET backend:

| URL | Purpose |
|-----|---------|
| `https://api.yourdomain.com/api/*` | REST API endpoints |
| `https://api.yourdomain.com/Admin` | Admin dashboard login |
| `https://api.yourdomain.com/health` | Health check endpoint |

```
Mobile App / Web Browser
         │
         ▼
api.yourdomain.com
         │
         ▼
    App Runner
         │
    ┌────┴────┐
    │         │
 /api/*    /Admin
   │          │
   ▼          ▼
REST API   Admin Dashboard
```

---

## GitHub Actions CI/CD

The repository includes a GitHub Actions workflow at `.github/workflows/deploy.yml` that:

1. Runs on push to `main` branch
2. Builds the Docker image
3. Pushes to ECR
4. Triggers App Runner deployment

### Setup GitHub Secrets

Go to your GitHub repository → Settings → Secrets and variables → Actions → New repository secret:

| Secret Name | Description |
|-------------|-------------|
| `AWS_ACCESS_KEY_ID` | IAM user access key |
| `AWS_SECRET_ACCESS_KEY` | IAM user secret key |
| `AWS_REGION` | e.g., `ap-southeast-2` |
| `ECR_REPOSITORY` | e.g., `pantrytales-backend` |
| `APP_RUNNER_SERVICE_ARN` | Your App Runner service ARN |

> ⚠️ **Note for AWS SSO/Academy Users**: If you're using AWS SSO (common in educational accounts), you cannot use static IAM credentials in GitHub Actions. Use the **Local Deployment** option below instead.

### Alternative: Local Deployment (for AWS SSO users)

If you're using AWS SSO or don't have permission to create IAM users, deploy from your local machine:

```bash
# Make sure you're logged in to AWS SSO
aws sso login

# Run the deploy script
./scripts/deploy-to-aws.sh
```

This script will:
1. Build the Docker image for AMD64
2. Push to ECR
3. Trigger App Runner deployment

### Required Environment Variables (GitHub Secrets)

These are the secrets your application needs. Set them in App Runner:

| Secret Name | Description |
|-------------|-------------|
| `ConnectionStrings__Postgres` | PostgreSQL connection string |
| `CloudflareR2__AccessKeyId` | R2 access key |
| `CloudflareR2__SecretAccessKey` | R2 secret key |
| `Embedding__ApiKey` | OpenAI API key |
| `Vision__ApiKey` | OpenAI API key |
| `ImageGeneration__ApiKey` | Gemini API key |
| `AdminPassword` | Admin dashboard password |

---

## Environment Variables

### Setting Environment Variables in App Runner

#### Via Console:
1. Go to App Runner Console
2. Select your service
3. Click **Configuration** tab
4. Under **Environment variables**, add each variable

#### Via CLI:
```bash
aws apprunner update-service \
    --service-arn $SERVICE_ARN \
    --source-configuration '{
        "ImageRepository": {
            "ImageIdentifier": "'$ECR_URI':latest",
            "ImageRepositoryType": "ECR",
            "ImageConfiguration": {
                "Port": "8080",
                "RuntimeEnvironmentVariables": {
                    "ASPNETCORE_ENVIRONMENT": "Production",
                    "ASPNETCORE_URLS": "http://+:8080",
                    "ConnectionStrings__Postgres": "Host=...;Database=...;...",
                    "CloudflareR2__AccessKeyId": "xxx",
                    "CloudflareR2__SecretAccessKey": "xxx",
                    "CloudflareR2__AccountId": "your-cloudflare-account-id",
                    "CloudflareR2__BucketName": "your-r2-bucket-name",
                    "CloudflareR2__PublicBaseUrl": "https://your-r2-public-url.example.com",
                    "Embedding__ApiKey": "sk-proj-xxx",
                    "Vision__ApiKey": "sk-proj-xxx",
                    "ImageGeneration__ApiKey": "AIzaSyxxx",
                    "Invitation__ResendApiKey": "re_xxx",
                    "AdminPassword": "your-secure-password",
                    "Clerk__Authority": "https://your-clerk-instance.clerk.accounts.dev"
                }
            }
        }
    }'
```

### Environment Variable Reference

| Variable | Required | Description |
|----------|----------|-------------|
| `ASPNETCORE_ENVIRONMENT` | Yes | Set to `Production` |
| `ASPNETCORE_URLS` | Yes | Set to `http://+:8080` |
| `ConnectionStrings__Postgres` | Yes | PostgreSQL connection string |
| `CloudflareR2__AccessKeyId` | Yes | Cloudflare R2 access key |
| `CloudflareR2__SecretAccessKey` | Yes | Cloudflare R2 secret key |
| `CloudflareR2__AccountId` | Yes | Cloudflare account ID (`your-cloudflare-account-id`) |
| `CloudflareR2__BucketName` | Yes | R2 bucket name |
| `CloudflareR2__PublicBaseUrl` | Yes | Public URL for images (`https://your-r2-public-url.example.com`) |
| `Embedding__ApiKey` | Yes | OpenAI API key for embeddings |
| `Vision__ApiKey` | Yes | OpenAI API key for GPT-4o vision |
| `ImageGeneration__ApiKey` | No | Gemini API key (optional) |
| `Invitation__ResendApiKey` | Yes | Resend API key for invitation emails |
| `AdminPassword` | Yes | Admin dashboard password |
| `Clerk__Authority` | Yes | Clerk authentication URL |

---

## Monitoring & Troubleshooting

### View Logs

```bash
# Stream logs from App Runner
aws apprunner list-operations \
    --service-arn $SERVICE_ARN \
    --region $AWS_REGION

# Or use CloudWatch Logs (App Runner automatically creates log groups)
aws logs tail /aws/apprunner/pantrytales-api/service --follow
```

### Health Check Endpoint

The backend exposes a health check at `/health`. Verify it's working:

```bash
curl https://api.yourdomain.com/health
```

Expected response:
```json
{"status":"healthy","timestamp":"2024-01-15T10:30:00.000Z"}
```

### Common Deployment Issues

#### ⚠️ Health Check Protocol Must Be HTTP

**Symptom**: Deployment logs show image pulled successfully, but deployment fails with no application logs.

**Cause**: App Runner health check is set to TCP instead of HTTP.

**Fix**:
1. Go to App Runner Console → Your service → **Configuration**
2. Under **Health check**, change **Protocol** from `TCP` to `HTTP`
3. Set **Path** to `/health`
4. Save changes

The deployment logs will show `Performing health check on protocol 'TCP'` if this is misconfigured.

#### ⚠️ Health Check Path Must Be `/health`

**Symptom**: Deployment logs show `Performing health check on protocol 'HTTP' [Path: '/']` but deployment still fails.

**Cause**: App Runner health check path is set to `/` instead of `/health`.

**Fix**:
1. Go to App Runner Console → Your service → **Configuration**
2. Under **Health check**, change **Path** from `/` to `/health`
3. Save changes

> **Important**: The root path `/` requires authentication and will fail health checks. The `/health` endpoint is explicitly marked as `AllowAnonymous` and returns a simple JSON response.

#### ⚠️ exec format error (Apple Silicon Macs)

**Symptom**: Application logs show `exec /usr/bin/dotnet: exec format error`

**Cause**: Docker image was built for ARM64 (Apple Silicon) but App Runner runs on AMD64/x86_64.

**Fix**: Rebuild the image with the correct platform:
```bash
docker build --platform linux/amd64 -t pantrytales-backend -f backend/Dockerfile .
```

Then push to ECR and App Runner will automatically redeploy.

> **Note for Docker Desktop / OrbStack users:**
> - **OrbStack**: Has built-in Rosetta 2 support - just use `--platform linux/amd64` and it handles emulation automatically (faster than QEMU)
> - **Docker Desktop**: Enable "Use Rosetta for x86_64/amd64 emulation" in Settings → General
> - Cross-platform builds are slower than native builds due to emulation, but only need to be done once per code change

#### Missing Environment Variables

**Symptom**: Application crashes immediately after starting.

**Fix**: Ensure all required environment variables are set (see [Environment Variable Reference](#environment-variable-reference)).

### Admin Dashboard Access

The admin dashboard is available at:

```
https://api.yourdomain.com/Admin
```

Login with the password set in `AdminPassword` environment variable.

### Common Issues

#### 1. Container fails to start
- Check CloudWatch logs for startup errors
- Verify all required environment variables are set
- Ensure the health check path (`/health`) returns 200 OK

#### 2. Database connection fails
- Verify `ConnectionStrings__Postgres` is correct
- Check if your database allows connections from AWS (security groups/firewall)
- For Neon: Ensure SSL mode is enabled in connection string

#### 3. Custom domain not working
- Wait up to 24-48 hours for DNS propagation
- Verify CNAME records are correct
- Check certificate validation status in ACM

#### 4. 502 Bad Gateway
- App is starting slowly; increase health check timeout
- Check memory/CPU allocation (may need more resources)

### Scaling

App Runner auto-scales based on traffic. Configure limits:

```bash
aws apprunner update-service \
    --service-arn $SERVICE_ARN \
    --auto-scaling-configuration-arn arn:aws:apprunner:$AWS_REGION:...:autoscalingconfiguration/...
```

Or via Console:
1. Service → Configuration → Auto scaling
2. Set min/max instances (default: 1-25)

---

## Quick Reference

### Useful Commands

```bash
# List App Runner services
aws apprunner list-services

# Get service details
aws apprunner describe-service --service-arn $SERVICE_ARN

# Pause service (stop billing for compute)
aws apprunner pause-service --service-arn $SERVICE_ARN

# Resume service
aws apprunner resume-service --service-arn $SERVICE_ARN

# Delete service
aws apprunner delete-service --service-arn $SERVICE_ARN
```

### Cost Estimation

App Runner pricing (ap-southeast-2):
- **Active**: ~$0.064/vCPU-hour + ~$0.007/GB-hour
- **Paused**: No compute charges (only storage)

For a 1 vCPU, 2GB service running 24/7:
- ~$46/month (vCPU) + ~$10/month (memory) = **~$56/month**

### Architecture Diagram

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   Mobile App    │     │   Web Browser   │     │  Admin Dashboard│
│  (Expo/React    │     │                 │     │  (Web Browser)  │
│    Native)      │     │                 │     │                 │
└────────┬────────┘     └────────┬────────┘     └────────┬────────┘
         │                       │                       │
         └───────────────────────┼───────────────────────┘
                                 │
                    ┌────────────▼────────────┐
                    │  Cloudflare DNS (CNAME) │
                    │ api.yourdomain.com │
                    └────────────┬────────────┘
                                 │
                    ┌────────────▼────────────┐
                    │     AWS App Runner      │
                    │   ┌─────────────────┐   │
                    │   │  .NET 9 Backend │   │
                    │   │    (Docker)     │   │
                    │   │                 │   │
                    │   │  /api/* → API   │   │
                    │   │  /Admin → Admin │   │
                    │   └─────────────────┘   │
                    └────────────┬────────────┘
                                 │
         ┌───────────────────────┼───────────────────────┐
         │                       │                       │
┌────────▼────────┐   ┌──────────▼──────────┐   ┌───────▼───────┐
│  Neon Postgres  │   │   Cloudflare R2     │   │   External    │
│   (Database)    │   │  (Image Storage)    │   │     APIs      │
│                 │   │                     │   │ (OpenAI/Gemini│
└─────────────────┘   └─────────────────────┘   │    /Clerk)    │
                                                └───────────────┘
```
