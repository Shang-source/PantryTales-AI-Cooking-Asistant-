<p align="center">
  <img src="mobile/assets/images/icon.png" alt="PantryTales Logo" width="120" height="120" />
</p>

<h1 align="center">PantryTales</h1>

<p align="center">
  <strong>Smart recipe suggestions based on what's in your pantry</strong>
</p>

<p align="center">
  A mobile app that helps you discover recipes using ingredients you already have, powered by AI vision and smart recommendations.
</p>

---

## Features

- **AI-Powered Ingredient Recognition** - Scan receipts or take photos of ingredients
- **Smart Recipe Suggestions** - Get personalized recipes based on your pantry inventory
- **Hands-Free Cooking Mode** - Voice-controlled step-by-step cooking assistance
- **Recipe Recognition** - Take a photo of a dish to get the recipe
- **Household Management** - Share pantries and recipes with family members
- **Inventory Tracking** - Keep track of what's in your pantry with expiration alerts

## Project Structure

```
PantryTales/
├── backend/                 # .NET 9.0 API server
│   ├── Controllers/         # API endpoints
│   ├── Services/            # Business logic & AI providers
│   │   ├── Embedding/       # OpenAI text embeddings
│   │   ├── Vision/          # OpenAI GPT-4o vision
│   │   ├── ImageGeneration/ # Gemini image generation
│   │   └── SmartRecipe/     # AI recipe suggestions
│   ├── Models/              # Database entities
│   ├── Dtos/                # Data transfer objects
│   ├── Migrations/          # EF Core migrations
│   └── Pages/               # Admin dashboard (Razor)
│
├── mobile/                  # React Native + Expo app
│   ├── app/                 # Expo Router screens
│   ├── components/          # Reusable UI components
│   ├── contexts/            # React contexts (auth, theme)
│   ├── hooks/               # Custom React hooks
│   ├── lib/                 # API client & utilities
│   ├── types/               # TypeScript type definitions
│   └── assets/              # Images, fonts, icons
│
└── README.md
```

## Prerequisites

| Tool | Version | Download |
|------|---------|----------|
| .NET SDK | 9.0+ | [Download](https://dotnet.microsoft.com/download/dotnet/9.0) |
| Bun | Latest | [Download](https://bun.sh/) |
| Rider (recommended) | Latest | [Download](https://www.jetbrains.com/rider/download) |

> **Note:** We use Bun instead of npm for faster package management:
> ```bash
> bun install          # instead of npm install
> bun add <package>    # instead of npm install <package>
> bun run <command>    # instead of npm run <command>
> ```

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/your-org/PantryTales.git
cd PantryTales
```

### 2. Backend Setup

```bash
cd backend

# Install EF Core tools (first time only)
dotnet tool install --global dotnet-ef

# Set up local secrets (API keys)
./setup-secrets.sh

# Run database migrations
dotnet ef database update

# Start the backend (with hot reload)
dotnet watch
```

The API will be available at `http://localhost:5158`

#### API Keys Setup

The backend requires API keys for AI features. These are stored securely using .NET User Secrets (never committed to git).

**Option 1: Interactive setup**
```bash
./setup-secrets.sh
```

**Option 2: Manual setup**
```bash
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=xxx;Database=xxx;Username=xxx;Password=xxx;..."
dotnet user-secrets set "CloudflareR2:AccessKeyId" "xxx"
dotnet user-secrets set "CloudflareR2:SecretAccessKey" "xxx"
dotnet user-secrets set "Embedding:ApiKey" "sk-proj-xxx"
dotnet user-secrets set "Vision:ApiKey" "sk-proj-xxx"
dotnet user-secrets set "ImageGeneration:ApiKey" "AIzaSyxxx"
dotnet user-secrets set "AdminPassword" "your-secure-password"
```

**For production (AWS App Runner):** Set environment variables with `__` separator:
```
ConnectionStrings__Postgres=Host=xxx;Database=xxx;...
CloudflareR2__AccessKeyId=xxx
CloudflareR2__SecretAccessKey=xxx
Embedding__ApiKey=sk-proj-xxx
Vision__ApiKey=sk-proj-xxx
ImageGeneration__ApiKey=AIzaSyxxx
AdminPassword=your-secure-password
```

### 3. Mobile Setup

```bash
cd mobile

# Install dependencies
bun install

# Copy environment file
cp .env.example .env.local

# Update EXPO_PUBLIC_API_BASE_URL to your machine's IP
# (Use 192.x.x.x or 172.x.x.x - NOT localhost)

# Start Expo
bun start
```

### 4. Database Setup (First Time)

The backend uses PostgreSQL (Neon). The connection string is configured via user secrets (set up in step 2).

Run migrations:
```bash
cd backend
dotnet ef database update
```

## Development

### Running the Backend

```bash
cd backend
dotnet watch    # Hot reload enabled
```

### Running the Backend in Docker

For testing the production Docker image locally:

```bash
# Build the Docker image (from project root)
# Note: Add --platform linux/amd64 if building on Apple Silicon for deployment
docker build -t pantrytales-backend -f backend/Dockerfile .

# Run with environment variables
# Use -d to run in background, --name for easy management
docker run -d --name pantrytales -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e ConnectionStrings__Postgres='your-postgres-connection-string' \
  -e CloudflareR2__AccessKeyId='your-r2-access-key' \
  -e CloudflareR2__SecretAccessKey='your-r2-secret-key' \
  -e CloudflareR2__AccountId='your-cloudflare-account-id' \
  -e CloudflareR2__BucketName='your-r2-bucket-name' \
  -e CloudflareR2__PublicBaseUrl='https://your-r2-public-url.example.com' \
  -e Embedding__ApiKey='your-openai-key' \
  -e Vision__ApiKey='your-openai-key' \
  -e ImageGeneration__ApiKey='your-gemini-key' \
  -e AdminPassword='password' \
  -e Clerk__Authority='https://your-clerk-instance.clerk.accounts.dev' \
  pantrytales-backend
```

Then access:
- API: http://localhost:8080
- Health check: http://localhost:8080/health
- Admin dashboard: http://localhost:8080/Admin

> **Tip**: Use the helper script `./scripts/docker-build-local.sh` to build the image.

### Running the Mobile App

```bash
cd mobile
bun start       # or: npx expo start
```

Press `i` for iOS simulator, `a` for Android emulator, or scan QR code with Expo Go.

### API Documentation

Interactive API docs available at: **http://localhost:5158/scalar/v1**

(Make sure the backend is running)

## Authentication

### Development Mode (Default)

Uses a simulated `dev-token` for quick testing without Clerk.

### Real Clerk Authentication

To test with real user accounts:

**1. Mobile** - In `mobile/.env.local`:
```bash
EXPO_PUBLIC_USE_REAL_AUTH=true
```

**2. Backend** - In `backend/appsettings.Development.json`:
```json
"UseRealAuth": true
```

**3. Restart both services** (clear Expo cache):
```bash
# Backend
dotnet run

# Mobile
npx expo start --clear
```

## Tech Stack

### Backend
- **.NET 9.0** / ASP.NET Core
- **Entity Framework Core** with PostgreSQL
- **OpenAI GPT-4o** for vision & embeddings
- **Google Gemini** for image generation
- **Clerk** for authentication
- **Cloudflare R2** for image storage
- **Resend** for transactional emails

### Mobile
- **React Native** with Expo
- **Expo Router** for navigation
- **TypeScript**
- **NativeWind** (Tailwind CSS)
- **Clerk** for authentication
- **React Query** for data fetching

## Environment Variables

### Backend (User Secrets / Environment Variables)

| Variable | Description |
|----------|-------------|
| `ConnectionStrings:Postgres` | PostgreSQL connection string (Neon) |
| `CloudflareR2:AccessKeyId` | Cloudflare R2 access key |
| `CloudflareR2:SecretAccessKey` | Cloudflare R2 secret key |
| `Embedding:ApiKey` | OpenAI API key for embeddings |
| `Vision:ApiKey` | OpenAI API key for GPT-4o vision |
| `ImageGeneration:ApiKey` | Gemini API key for image generation |
| `Invitation:ResendApiKey` | Resend API key for invitation emails |
| `AdminPassword` | Password for admin dashboard access |

### Backend (`appsettings.json` - non-sensitive)

| Variable | Description |
|----------|-------------|
| `Clerk:Authority` | Clerk authentication URL |
| `CloudflareR2:AccountId` | Cloudflare account ID |
| `CloudflareR2:BucketName` | R2 bucket name |
| `CloudflareR2:PublicBaseUrl` | Public URL for images |

### Mobile (`.env.local`)

| Variable | Description |
|----------|-------------|
| `EXPO_PUBLIC_API_BASE_URL` | Backend API URL (use your IP, not localhost) |
| `EXPO_PUBLIC_CLERK_PUBLISHABLE_KEY` | Clerk publishable key |
| `EXPO_PUBLIC_USE_REAL_AUTH` | Enable real Clerk auth (`true`/`false`) |

## License

Private - All rights reserved.
