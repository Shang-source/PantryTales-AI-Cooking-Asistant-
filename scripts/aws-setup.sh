#!/bin/bash
# PantryTales - AWS Infrastructure Setup
# This script sets up the required AWS resources for deployment

set -e

echo "==========================================="
echo "PantryTales AWS Infrastructure Setup"
echo "==========================================="
echo ""

# Configuration
AWS_REGION="${AWS_REGION:-ap-southeast-2}"
AWS_PROFILE="${AWS_PROFILE:-}"
ECR_REPOSITORY="pantrytales-backend"
APP_RUNNER_SERVICE="pantrytales-api"

# Build AWS CLI args with profile if set
AWS_ARGS=""
if [ -n "$AWS_PROFILE" ]; then
    AWS_ARGS="--profile $AWS_PROFILE"
fi

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check prerequisites
check_prerequisites() {
    echo "Checking prerequisites..."

    if ! command -v aws &> /dev/null; then
        echo -e "${RED}Error: AWS CLI not found. Please install it first.${NC}"
        echo "  brew install awscli  # macOS"
        echo "  or visit: https://aws.amazon.com/cli/"
        exit 1
    fi

    if ! command -v docker &> /dev/null; then
        echo -e "${RED}Error: Docker not found. Please install Docker Desktop.${NC}"
        exit 1
    fi

    # Check AWS credentials
    if ! aws sts get-caller-identity $AWS_ARGS &> /dev/null; then
        echo -e "${RED}Error: AWS credentials not configured.${NC}"
        echo "Please run: aws configure"
        echo "Or set AWS_PROFILE environment variable"
        exit 1
    fi

    echo -e "${GREEN}✓ All prerequisites met${NC}"
    echo ""
}

# Get AWS account ID
get_account_id() {
    AWS_ACCOUNT_ID=$(aws sts get-caller-identity $AWS_ARGS --query Account --output text)
    echo "AWS Account: $AWS_ACCOUNT_ID"
    echo "Region: $AWS_REGION"
    if [ -n "$AWS_PROFILE" ]; then
        echo "Profile: $AWS_PROFILE"
    fi
    echo ""
}

# Create ECR repository
create_ecr_repository() {
    echo "Creating ECR repository..."

    if aws ecr describe-repositories $AWS_ARGS --repository-names $ECR_REPOSITORY --region $AWS_REGION &> /dev/null; then
        echo -e "${YELLOW}ECR repository '$ECR_REPOSITORY' already exists${NC}"
    else
        aws ecr create-repository $AWS_ARGS \
            --repository-name $ECR_REPOSITORY \
            --region $AWS_REGION \
            --image-scanning-configuration scanOnPush=true
        echo -e "${GREEN}✓ ECR repository created${NC}"
    fi

    ECR_URI="$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/$ECR_REPOSITORY"
    echo "ECR URI: $ECR_URI"
    echo ""
}

# Create IAM role for App Runner
create_apprunner_role() {
    echo "Creating IAM role for App Runner ECR access..."

    ROLE_NAME="AppRunnerECRAccessRole"

    if aws iam get-role $AWS_ARGS --role-name $ROLE_NAME &> /dev/null; then
        echo -e "${YELLOW}IAM role '$ROLE_NAME' already exists${NC}"
    else
        # Create trust policy
        cat > /tmp/trust-policy.json << 'EOF'
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Principal": {
                "Service": "build.apprunner.amazonaws.com"
            },
            "Action": "sts:AssumeRole"
        }
    ]
}
EOF

        # Create the role
        aws iam create-role $AWS_ARGS \
            --role-name $ROLE_NAME \
            --assume-role-policy-document file:///tmp/trust-policy.json

        # Attach ECR policy
        aws iam attach-role-policy $AWS_ARGS \
            --role-name $ROLE_NAME \
            --policy-arn arn:aws:iam::aws:policy/service-role/AWSAppRunnerServicePolicyForECRAccess

        echo -e "${GREEN}✓ IAM role created${NC}"
        rm /tmp/trust-policy.json
    fi

    ROLE_ARN="arn:aws:iam::$AWS_ACCOUNT_ID:role/$ROLE_NAME"
    echo "Role ARN: $ROLE_ARN"
    echo ""
}

# Build and push initial Docker image
build_and_push_image() {
    echo "Building and pushing Docker image..."

    # Navigate to project root
    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
    cd "$PROJECT_ROOT"

    # Login to ECR
    aws ecr get-login-password $AWS_ARGS --region $AWS_REGION | \
        docker login --username AWS --password-stdin "$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com"

    # Build and push
    docker build --platform linux/amd64 -t $ECR_REPOSITORY -f backend/Dockerfile .
    docker tag $ECR_REPOSITORY:latest $ECR_URI:latest
    docker push $ECR_URI:latest

    echo -e "${GREEN}✓ Docker image pushed to ECR${NC}"
    echo ""
}

# Print next steps
print_next_steps() {
    echo "==========================================="
    echo -e "${GREEN}Setup Complete!${NC}"
    echo "==========================================="
    echo ""
    echo "Next steps:"
    echo ""
    echo "1. Create App Runner service in AWS Console:"
    echo "   https://console.aws.amazon.com/apprunner/"
    echo ""
    echo "   Configure:"
    echo "   - Source: Amazon ECR"
    echo "   - Image URI: $ECR_URI:latest"
    echo "   - Access role: $ROLE_ARN"
    echo "   - Port: 8080"
    echo "   - Health check path: /health"
    echo ""
    echo "2. Set environment variables in App Runner:"
    echo "   - ASPNETCORE_ENVIRONMENT=Production"
    echo "   - ASPNETCORE_URLS=http://+:8080"
    echo "   - ConnectionStrings__Postgres=<your-connection-string>"
    echo "   - CloudflareR2__AccessKeyId=<your-key>"
    echo "   - CloudflareR2__SecretAccessKey=<your-secret>"
    echo "   - Embedding__ApiKey=<openai-key>"
    echo "   - Vision__ApiKey=<openai-key>"
    echo "   - ImageGeneration__ApiKey=<gemini-key>"
    echo "   - AdminPassword=<your-password>"
    echo "   - Clerk__Authority=https://your-clerk-instance.clerk.accounts.dev"
    echo ""
    echo "3. Configure GitHub Secrets for CI/CD:"
    echo "   - AWS_ACCESS_KEY_ID"
    echo "   - AWS_SECRET_ACCESS_KEY"
    echo "   - APP_RUNNER_SERVICE_ARN (after creating the service)"
    echo ""
    echo "4. (Optional) Configure custom domain in App Runner"
    echo ""
    echo "==========================================="
    echo "Useful values:"
    echo "  ECR_URI: $ECR_URI"
    echo "  ROLE_ARN: $ROLE_ARN"
    echo "  AWS_REGION: $AWS_REGION"
    echo "==========================================="
}

# Main execution
main() {
    check_prerequisites
    get_account_id
    create_ecr_repository
    create_apprunner_role

    read -p "Build and push Docker image now? (y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        build_and_push_image
    fi

    print_next_steps
}

main
