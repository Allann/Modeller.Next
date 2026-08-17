# Modeller Admin

Private product-engagement dashboard for `admin.modeller.website`.

## Configuration

- `AUTH_SECRET`: Auth.js session secret.
- `AUTH_GITHUB_ID` and `AUTH_GITHUB_SECRET`: GitHub OAuth application credentials.
- `ADMIN_GITHUB_ACCOUNT_IDS`: Comma-separated numeric GitHub account IDs that may sign in.
- `POSTHOG_HOST`: PostHog UI and query host, such as `https://us.posthog.com`.
- `POSTHOG_PROJECT_ID`: PostHog project ID.
- `POSTHOG_PERSONAL_API_KEY`: Server-only key with project query access.

The public apps need `NEXT_PUBLIC_POSTHOG_KEY` and `NEXT_PUBLIC_POSTHOG_HOST`. The API needs `ProductAnalytics__ProjectKey` and optional `ProductAnalytics__Host`. When a project key is absent, capture is disabled.
