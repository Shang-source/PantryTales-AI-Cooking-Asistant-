#!/bin/bash
# PantryTales Backend - Deploy to AWS App Runner
# Automatically handles SSO authentication

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

AWS_REGION="${AWS_REGION:-ap-southeast-2}"
AWS_PROFILE="${AWS_PROFILE:-}"
ECR_REPOSITORY="pantrytales-backend"

cd "$PROJECT_ROOT"

echo "==================================="
echo "PantryTales AWS Deployment"
echo "==================================="
echo ""

# Auto-detect SSO profile if not set
if [ -z "$AWS_PROFILE" ]; then
    # Look for SSO profiles in AWS config
    if [ -f ~/.aws/config ]; then
        SSO_PROFILE=$(grep -E '^\[profile ' ~/.aws/config | grep -v '#' | head -1 | sed 's/\[profile \(.*\)\]/\1/')
        if [ -n "$SSO_PROFILE" ]; then
            echo "Auto-detected AWS profile: $SSO_PROFILE"
            AWS_PROFILE="$SSO_PROFILE"
        fi
    fi
fi

if [ -z "$AWS_PROFILE" ]; then
    echo "✗ No AWS profile found."
    echo ""
    echo "Run this first to configure SSO:"
    echo "  aws configure sso"
    echo ""
    exit 1
fi

# Build AWS CLI args
AWS_ARGS="--profile $AWS_PROFILE"

echo "Using AWS profile: $AWS_PROFILE"
echo ""

# Function to check credentials
check_credentials() {
    aws sts get-caller-identity $AWS_ARGS --query Account --output text 2>&1
}

# Check if credentials are valid
echo "Checking AWS credentials..."
set +e
ACCOUNT_ID=$(check_credentials)
EXIT_CODE=$?
set -e

# If credentials failed, try to login
if [ $EXIT_CODE -ne 0 ] || [ -z "$ACCOUNT_ID" ] || [[ "$ACCOUNT_ID" == *"error"* ]] || [[ "$ACCOUNT_ID" == *"Error"* ]] || [[ "$ACCOUNT_ID" == *"expired"* ]] || [[ "$ACCOUNT_ID" == *"token"* ]]; then
    echo "Session expired or not logged in. Starting SSO login..."
    echo ""

    # Run SSO login
    aws sso login --profile "$AWS_PROFILE"

    echo ""
    echo "Verifying credentials..."
    set +e
    ACCOUNT_ID=$(check_credentials)
    EXIT_CODE=$?
    set -e

    if [ $EXIT_CODE -ne 0 ] || [ -z "$ACCOUNT_ID" ] || [[ "$ACCOUNT_ID" == *"error"* ]]; then
        echo ""
        echo "✗ SSO login failed. Please check your configuration:"
        echo "  aws configure sso"
        exit 1
    fi
fi

echo "✓ Using AWS Account: $ACCOUNT_ID"
echo "  Profile: $AWS_PROFILE"

ECR_URI="$ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/$ECR_REPOSITORY"
IMAGE_TAG=$(git rev-parse --short HEAD)

echo ""
echo "Building Docker image for AMD64..."
docker build --platform linux/amd64 -t $ECR_REPOSITORY:$IMAGE_TAG -f backend/Dockerfile .

echo ""
echo "Logging in to ECR..."
aws ecr get-login-password $AWS_ARGS --region $AWS_REGION | docker login --username AWS --password-stdin $ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com

echo ""
echo "Tagging and pushing image..."
docker tag $ECR_REPOSITORY:$IMAGE_TAG $ECR_URI:$IMAGE_TAG
docker tag $ECR_REPOSITORY:$IMAGE_TAG $ECR_URI:latest
docker push $ECR_URI:$IMAGE_TAG
docker push $ECR_URI:latest

echo ""
echo "Triggering App Runner deployment..."
SERVICE_ARN=$(aws apprunner list-services $AWS_ARGS --region $AWS_REGION --query "ServiceSummaryList[?ServiceName=='pantrytales-api'].ServiceArn" --output text)

if [ -n "$SERVICE_ARN" ] && [ "$SERVICE_ARN" != "None" ]; then
    aws apprunner start-deployment $AWS_ARGS --service-arn $SERVICE_ARN --region $AWS_REGION
    echo "✓ Deployment triggered!"
else
    echo "Warning: App Runner service not found. Deploy manually from AWS Console."
fi

echo ""
echo "==================================="
echo "Deployment complete!"
echo "==================================="
echo ""
echo "Image: $ECR_URI:$IMAGE_TAG"
echo "Check status: https://$AWS_REGION.console.aws.amazon.com/apprunner"
echo ""
