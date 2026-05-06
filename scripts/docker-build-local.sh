#!/bin/bash
# Build and run Docker image locally for testing

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

cd "$PROJECT_ROOT"

# Check if --amd64 flag is passed for deployment builds
PLATFORM_FLAG=""
if [ "$1" = "--amd64" ]; then
    PLATFORM_FLAG="--platform linux/amd64"
    echo "Building Docker image for AMD64 (deployment)..."
else
    echo "Building Docker image for local architecture..."
    echo "(Use --amd64 flag for AWS deployment builds)"
fi

docker build $PLATFORM_FLAG -t pantrytales-backend -f backend/Dockerfile .

echo ""
echo "Docker image built successfully!"
echo ""
echo "To run locally with environment variables:"
echo ""
echo "  docker run -p 8080:8080 \\"
echo "    -e ASPNETCORE_ENVIRONMENT=Development \\"
echo "    -e ConnectionStrings__Postgres='your-connection-string' \\"
echo "    -e CloudflareR2__AccessKeyId='your-key' \\"
echo "    -e CloudflareR2__SecretAccessKey='your-secret' \\"
echo "    -e Embedding__ApiKey='your-openai-key' \\"
echo "    -e Vision__ApiKey='your-openai-key' \\"
echo "    -e Invitation__ResendApiKey='your-resend-key' \\"
echo "    -e AdminPassword='password' \\"
echo "    -e Clerk__Authority='https://your-clerk-instance.clerk.accounts.dev' \\"
echo "    pantrytales-backend"
echo ""
echo "Then access:"
echo "  - API: http://localhost:8080"
echo "  - Health: http://localhost:8080/health"
echo "  - Admin: http://localhost:8080/Admin"
