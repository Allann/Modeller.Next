---
title: Use PostHog for pseudonymous product analytics
---

# Use PostHog for pseudonymous product analytics

Modeller uses PostHog Cloud for explicit product events and a separate, owner-only admin dashboard for aggregate results. Automatic capture, person profiles, submitted business content, full Initiative URLs, and session replay are excluded because product learning does not justify their privacy risk. Vercel Analytics remains the traffic authority, and OpenTelemetry remains the operational telemetry authority.
