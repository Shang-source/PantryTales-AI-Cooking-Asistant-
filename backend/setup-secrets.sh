#!/bin/bash
# PantryTales Backend - Local Development Secrets Setup
# Run this script to configure API keys for local development

set -e

echo "==================================="
echo "PantryTales Backend Secrets Setup"
echo "==================================="
echo ""
echo "This script will configure API keys for local development."
echo "Keys are stored securely in .NET User Secrets (not in the repo)."
echo ""

# Check if dotnet is available
if ! command -v dotnet &> /dev/null; then
    echo "Error: dotnet CLI not found. Please install .NET SDK first."
    exit 1
fi

# Navigate to script directory
cd "$(dirname "$0")"

# Initialize user secrets if needed
dotnet user-secrets init 2>/dev/null || true

echo "Enter your secrets (ask your team lead if you don't have them):"
echo ""

# PostgreSQL Connection String
read -r -p "PostgreSQL Connection String: " PG_CONN
if [ -z "$PG_CONN" ]; then
    echo "Warning: Connection string not provided. Database features won't work."
else
    dotnet user-secrets set "ConnectionStrings:Postgres" "$PG_CONN"
    echo "✓ PostgreSQL connection string configured"
fi

echo ""

# Cloudflare R2 credentials
read -r -p "Cloudflare R2 Access Key ID: " R2_ACCESS_KEY
if [ -z "$R2_ACCESS_KEY" ]; then
    echo "Warning: R2 Access Key not provided. Image storage won't work."
else
    dotnet user-secrets set "CloudflareR2:AccessKeyId" "$R2_ACCESS_KEY"
    echo "✓ Cloudflare R2 Access Key configured"
fi

read -r -p "Cloudflare R2 Secret Access Key: " R2_SECRET_KEY
if [ -z "$R2_SECRET_KEY" ]; then
    echo "Warning: R2 Secret Key not provided. Image storage won't work."
else
    dotnet user-secrets set "CloudflareR2:SecretAccessKey" "$R2_SECRET_KEY"
    echo "✓ Cloudflare R2 Secret Key configured"
fi

echo ""

# OpenAI API Key (used for Embedding and Vision)
read -r -p "OpenAI API Key: " OPENAI_KEY
if [ -z "$OPENAI_KEY" ]; then
    echo "Warning: OpenAI key not provided. Embedding and Vision features won't work."
else
    dotnet user-secrets set "Embedding:ApiKey" "$OPENAI_KEY"
    dotnet user-secrets set "Vision:ApiKey" "$OPENAI_KEY"
    echo "✓ OpenAI key configured for Embedding and Vision"
fi

echo ""

# Gemini API Key (used for Image Generation)
read -r -p "Gemini API Key (optional, for image generation): " GEMINI_KEY
if [ -z "$GEMINI_KEY" ]; then
    echo "Skipping Gemini key (image generation disabled by default anyway)"
else
    dotnet user-secrets set "ImageGeneration:ApiKey" "$GEMINI_KEY"
    echo "✓ Gemini key configured for Image Generation"
fi

echo ""

# Resend API Key (for invitation emails)
read -r -p "Resend API Key (for invitation emails): " RESEND_KEY
if [ -z "$RESEND_KEY" ]; then
    echo "Warning: Resend key not provided. Invitation emails won't work."
else
    dotnet user-secrets set "Invitation:ResendApiKey" "$RESEND_KEY"
    echo "✓ Resend key configured for invitation emails"
fi

echo ""

# Admin Password (for admin dashboard)
read -r -p "Admin Dashboard Password: " ADMIN_PASSWORD
if [ -z "$ADMIN_PASSWORD" ]; then
    echo "Warning: Admin password not provided. Admin dashboard won't be accessible."
else
    dotnet user-secrets set "AdminPassword" "$ADMIN_PASSWORD"
    echo "✓ Admin dashboard password configured"
fi

echo ""
echo "==================================="
echo "Setup complete!"
echo "==================================="
echo ""
echo "Your secrets are stored at:"
echo "  ~/.microsoft/usersecrets/<project-id>/secrets.json"
echo ""
echo "To view your secrets:  dotnet user-secrets list"
echo "To remove all secrets: dotnet user-secrets clear"
echo ""
