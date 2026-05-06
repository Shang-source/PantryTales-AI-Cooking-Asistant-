# PantryTales Mobile App - Publishing Guide

This guide covers building and publishing the PantryTales mobile app to iOS App Store and Android app stores.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [EAS Setup](#eas-setup)
3. [iOS App Store](#ios-app-store)
4. [Android Distribution](#android-distribution)
5. [Build Scripts](#build-scripts)
6. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Required Accounts

| Platform | Requirement | Cost |
|----------|-------------|------|
| iOS App Store | Apple Developer Program | $99/year |
| Google Play | Google Play Developer | $25 one-time |
| Third-party Android | None | Free |

### Install EAS CLI

```bash
npm install -g eas-cli
eas login
```

---

## EAS Setup

### 1. Configure EAS in Your Project

```bash
cd mobile
eas build:configure
```

This creates an `eas.json` file with build profiles.

### 2. Recommended eas.json Configuration

```json
{
  "cli": {
    "version": ">= 5.0.0"
  },
  "build": {
    "development": {
      "developmentClient": true,
      "distribution": "internal",
      "ios": {
        "simulator": true
      }
    },
    "preview": {
      "distribution": "internal",
      "android": {
        "buildType": "apk"
      },
      "ios": {
        "simulator": false
      }
    },
    "production": {
      "android": {
        "buildType": "app-bundle"
      },
      "autoIncrement": true
    }
  },
  "submit": {
    "production": {}
  }
}
```

### Build Profiles Explained

| Profile | Purpose | Output |
|---------|---------|--------|
| `development` | Local development with dev client | Debug build |
| `preview` | Internal testing, third-party stores | APK (Android), IPA (iOS) |
| `production` | App Store / Play Store submission | AAB (Android), IPA (iOS) |

---

## iOS App Store

### Prerequisites

1. **Apple Developer Account** ($99/year)
2. **App Store Connect** app entry created
3. **Certificates & Provisioning Profiles** (EAS handles this automatically)

### Step 1: Create App in App Store Connect

1. Go to [App Store Connect](https://appstoreconnect.apple.com/)
2. Click **My Apps** → **+** → **New App**
3. Fill in:
   - Platform: iOS
   - Name: PantryTales
   - Primary Language: English
   - Bundle ID: (from app.json)
   - SKU: pantrytales-ios

### Step 2: Build for App Store

```bash
cd mobile

# Build production IPA
bun build:ios

# Or manually:
eas build --platform ios --profile production
```

### Step 3: Submit to App Store

```bash
# Submit the latest build
bun submit:ios

# Or manually:
eas submit --platform ios
```

### Step 4: Complete App Store Listing

In App Store Connect, fill in:
- Screenshots (required sizes: 6.7", 6.5", 5.5")
- App description
- Keywords
- Support URL
- Privacy Policy URL
- Age rating
- Price

### Step 5: Submit for Review

Click **Submit for Review** in App Store Connect. Review typically takes 24-48 hours.

---

## Android Distribution

### Option 1: Google Play Store

#### Prerequisites
1. **Google Play Developer Account** ($25 one-time)
2. **App signing** configured in Play Console

#### Build & Submit

```bash
cd mobile

# Build production AAB (Android App Bundle)
bun build:android

# Submit to Play Store
bun submit:android
```

#### Play Console Setup

1. Go to [Google Play Console](https://play.google.com/console/)
2. Create new app
3. Complete store listing
4. Upload AAB from EAS build
5. Submit for review

### Option 2: Third-Party Android Stores (APK)

For stores like APKPure, Amazon Appstore, Samsung Galaxy Store, etc.

```bash
cd mobile

# Build APK (not AAB)
bun build:android:apk

# Or manually:
eas build --platform android --profile preview
```

The APK can be downloaded from [expo.dev](https://expo.dev) and uploaded to any store.

### Option 3: Direct APK Distribution

For internal testing or direct distribution:

```bash
# Build APK
eas build --platform android --profile preview

# Download the APK from the build URL
# Share via email, website, or QR code
```

---

## Build Scripts

Add these scripts to `mobile/package.json`:

```json
{
  "scripts": {
    "build:ios": "eas build --platform ios --profile production",
    "build:android": "eas build --platform android --profile production",
    "build:android:apk": "eas build --platform android --profile preview",
    "build:all": "eas build --platform all --profile production",
    "submit:ios": "eas submit --platform ios",
    "submit:android": "eas submit --platform android"
  }
}
```

### Script Reference

| Script | Description |
|--------|-------------|
| `bun build:ios` | Build iOS app for App Store |
| `bun build:android` | Build Android AAB for Play Store |
| `bun build:android:apk` | Build Android APK for third-party stores |
| `bun build:all` | Build both platforms |
| `bun submit:ios` | Submit iOS build to App Store Connect |
| `bun submit:android` | Submit Android build to Play Store |

---

## Environment Variables for Builds

EAS builds use environment variables from `eas.json` or EAS Environment Variables.

### Local Development

For local development, create a `.env.local` file in the `mobile/` directory:

```bash
EXPO_PUBLIC_CLERK_PUBLISHABLE_KEY=pk_test_your-key-here
EXPO_PUBLIC_API_BASE_URL=https://pantrytalesapi.tty0.top
EXPO_PUBLIC_USE_REAL_AUTH=true
```

These are automatically loaded when running `expo start` or `expo run:ios`.

### Setting EAS Environment Variables (for Cloud Builds)

Use `eas env:create` to set environment variables for EAS cloud builds:

```bash
cd mobile

# Set environment variables for production builds
# Note: EXPO_PUBLIC_ variables use "plaintext" visibility (they're embedded in the app)
eas env:create --name EXPO_PUBLIC_CLERK_PUBLISHABLE_KEY --value "pk_test_your-key" --visibility plaintext --environment production
eas env:create --name EXPO_PUBLIC_API_BASE_URL --value "https://pantrytalesapi.tty0.top" --visibility plaintext --environment production
eas env:create --name EXPO_PUBLIC_USE_REAL_AUTH --value "true" --visibility plaintext --environment production
```

### Managing Environment Variables

```bash
# List all environment variables
eas env:list --environment production

# Update an existing variable
eas env:update --name EXPO_PUBLIC_API_BASE_URL --value "https://new-api.example.com" --environment production

# Delete a variable
eas env:delete --name EXPO_PUBLIC_API_BASE_URL --environment production
```

### Using in eas.json (Alternative)

You can also hardcode non-sensitive values directly in `eas.json`:

```json
{
  "build": {
    "production": {
      "env": {
        "EXPO_PUBLIC_API_BASE_URL": "https://pantrytalesapi.tty0.top",
        "EXPO_PUBLIC_USE_REAL_AUTH": "true"
      }
    }
  }
}
```

> **Note:** Prefer `eas env:create` for values that may change between environments or contain sensitive data like API keys.

---

## Troubleshooting

### iOS Build Fails

**"No matching provisioning profile"**
```bash
# Let EAS regenerate credentials
eas credentials --platform ios
```

**"Bundle ID mismatch"**
- Ensure `app.json` bundle identifier matches App Store Connect

### Android Build Fails

**"Keystore not found"**
```bash
# Generate new keystore (EAS manages this)
eas credentials --platform android
```

### Submission Rejected

**Common rejection reasons:**
1. Missing privacy policy
2. Incomplete metadata
3. Crashes on launch (test thoroughly!)
4. Guideline violations

### Build Takes Too Long

EAS builds run on Expo's cloud servers. For faster builds:
- Use `--local` flag for local builds (requires Xcode/Android Studio)
- Upgrade to EAS Production plan for priority queues

---

## Version Management

### Auto-increment Version

In `eas.json`:
```json
{
  "build": {
    "production": {
      "autoIncrement": true
    }
  }
}
```

### Manual Version Update

In `app.json`:
```json
{
  "expo": {
    "version": "1.0.1",
    "ios": {
      "buildNumber": "2"
    },
    "android": {
      "versionCode": 2
    }
  }
}
```

---

## Quick Reference

```bash
# Development
bun ios:device          # Run on physical iPhone
bun android             # Run on Android emulator

# Building
bun build:ios           # iOS App Store build
bun build:android       # Android Play Store build
bun build:android:apk   # Android APK for other stores

# Submitting
bun submit:ios          # Submit to App Store
bun submit:android      # Submit to Play Store
```
