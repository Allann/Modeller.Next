---
title: "Service Discovery for Inter-Service HTTP Calls"
---

# Service Discovery for Inter-Service HTTP Calls


## The Standard

When one service calls another over HTTP inside the platform, it MUST address the target by a logical service name resolved through a service-discovery client (e.g. Steeltoe + Consul) with client-side load balancing, not by a hardcoded host/port or static DNS name baked into configuration.

## Why

The "before" sample has no HTTP client for the reporting service at all in `Newsletter.Api` — any inter-service call would have had to hardcode a URL (as `MessageBroker:Host` does for RabbitMQ: `"amqp://rabbitmq:5672"`), which breaks the moment the target service moves, scales to multiple instances, or changes port/host in a different environment. The "after" sample instead registers `AddServiceDiscovery(o => o.UseConsul())` and configures the typed client with `AddHttpClient<GetReportingArticle.Client>(client => client.BaseAddress = new Uri("http://reporting-service")).AddServiceDiscovery().AddRoundRobinLoadBalancer()`. The base address uses the service's logical name (`reporting-service`), and Steeltoe's discovery client resolves it against Consul's service registry at call time, load-balancing round-robin across available instances. This decouples the caller from any specific host/port and supports horizontal scaling without config changes.

## Before (Anti-pattern)

```csharp
// No service-discovery-aware client - an inter-service call would have to
// hardcode a host, exactly like the message broker config here does:
configurator.Host(new Uri(builder.Configuration["MessageBroker:Host"]!), h => { /* ... */ });
// (No HTTP client existed yet to call the reporting service.)
```

## After (Standard)

```csharp
builder.Services.AddServiceDiscovery(o => o.UseConsul());

builder.Services
    .AddHttpClient<GetReportingArticle.Client>(client =>
        client.BaseAddress = new Uri("http://reporting-service"))
    .AddServiceDiscovery()
    .AddRoundRobinLoadBalancer();

// Client just calls the logical name - resolution/load balancing happens underneath.
public sealed class Client(HttpClient httpClient)
{
    public async Task<Response?> GetAsync(Guid id) =>
        await httpClient.GetFromJsonAsync<Response>($"api/articles/{id}");
}
```

## Rules for LLMs / Agents

- Register `AddServiceDiscovery(...)` (with the platform's chosen registry, e.g. Consul) once per service that needs to call other internal services over HTTP.
- Configure typed `HttpClient`s for internal services with a `BaseAddress` using the target's logical service name (`http://<service-name>`), never a hardcoded host/IP/port.
- Chain `.AddServiceDiscovery().AddRoundRobinLoadBalancer()` (or the equivalent load-balancing strategy) onto every internal typed HTTP client registration.
- Keep the actual HTTP call code (the typed client's methods) unaware of discovery — it should just call relative paths against `HttpClient`; discovery/load-balancing is purely a registration-time concern.
- Do not put internal service URLs in `appsettings.json`/environment variables as literal hosts — externally-facing/third-party APIs (e.g. `https://www.alphavantage.co`) are the exception and may remain as configured absolute URLs.

## When NOT to apply

External third-party APIs (payment providers, market data feeds, etc.) are addressed by their real DNS name/URL as normal — service discovery only applies to calls between services owned and deployed within the same platform/registry.
