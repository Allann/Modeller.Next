---
title: "Distributed Tracing and Structured Logging with OpenTelemetry/Serilog/Seq"
---

# Distributed Tracing and Structured Logging with OpenTelemetry/Serilog/Seq


## The Standard

Every service in a distributed system (APIs, message consumers) MUST be wired with structured logging (Serilog, configured from `IConfiguration`, `UseSerilogRequestLogging()`) and OpenTelemetry tracing (`AddOpenTelemetry().WithTracing(...)`) that instruments ASP.NET Core, `HttpClient`, and the message bus (e.g. MassTransit's `DiagnosticHeaders.DefaultListenerName`), exported via OTLP so traces/logs correlate across service boundaries (e.g. in Seq). Message consumers MUST log at each meaningful step (received, not found, rejected, processed, published) using structured message templates, not string interpolation.

## Why

The "before" `Program.cs` has no logging configuration and no tracing at all — a `PurchaseOrderSentConsumer` failure or a slow request leaves no trace across the API -> RabbitMQ -> consumer -> downstream HTTP call chain, making cross-service debugging effectively blind. The "after" version adds `UseSerilog` reading from configuration, `UseSerilogRequestLogging()` for HTTP request logs, and `AddOpenTelemetry()...WithTracing()` registering `AddAspNetCoreInstrumentation`, `AddHttpClientInstrumentation`, and `AddSource(MassTransit.Logging.DiagnosticHeaders.DefaultListenerName)` so that a single business operation's trace spans the web request, the RabbitMQ message, and the outbound stock-price HTTP call, all exported via `AddOtlpExporter()` to a collector/Seq. `PurchaseOrderSentConsumer` gains `ILogger<T>` injection and logs at every decision branch (order received, order not found, price rejected, order filled, event published) using structured templates (`{OrderId}`, `{@Order}`) rather than string concatenation, so log fields remain queryable.

## Before (Anti-pattern)

```csharp
// No logging, no tracing configured anywhere in the pipeline.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMassTransit(configure => { /* ... */ });
var app = builder.Build();
app.MapEndpoints();
app.Run();

// Consumer has no observability into what happened to a message.
public async Task Consume(ConsumeContext<PurchaseOrderSent> context)
{
    var order = OrdersDb.Instance.GetValueOrDefault(context.Message.OrderId);
    if (order is null) return;
    // ...
}
```

## After (Standard)

```csharp
builder.Host.UseSerilog((context, loggerConfig) => loggerConfig.ReadFrom.Configuration(context.Configuration));

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("Stocks.Api"))
    .WithTracing(tracing =>
    {
        tracing
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation()
            .AddSource(MassTransit.Logging.DiagnosticHeaders.DefaultListenerName);

        tracing.AddOtlpExporter();
    });

app.UseSerilogRequestLogging();

// Consumer logs structured events at each decision point.
public class PurchaseOrderSentConsumer(StocksClient stocksClient, ILogger<PurchaseOrderSentConsumer> logger)
    : IConsumer<PurchaseOrderSent>
{
    public async Task Consume(ConsumeContext<PurchaseOrderSent> context)
    {
        logger.LogInformation("Processing purchase order {OrderId}", context.Message.OrderId);
        var order = OrdersDb.Instance.GetValueOrDefault(context.Message.OrderId);
        if (order is null)
        {
            logger.LogInformation("Couldn't find purchase order {OrderId}", context.Message.OrderId);
            return;
        }
        // ...
    }
}
```

## Rules for LLMs / Agents

- Every ASP.NET Core service MUST configure Serilog from `IConfiguration` (`UseSerilog(...ReadFrom.Configuration...)`) and call `app.UseSerilogRequestLogging()`.
- Every service MUST register OpenTelemetry tracing with `AddAspNetCoreInstrumentation()` and `AddHttpClientInstrumentation()` at minimum, exporting via `AddOtlpExporter()`.
- Services using a message bus (MassTransit or similar) MUST add the bus's diagnostic listener as an OpenTelemetry trace source so message-driven work is part of the same distributed trace as the triggering HTTP request.
- Log with structured message templates and named placeholders (`"Processing order {OrderId}"`), never string interpolation/concatenation, so log fields stay queryable in Seq/structured sinks.
- Message consumers MUST log at every meaningful branch: message received, early-exit/rejection reasons, success, and any outbound event published — so a message's full lifecycle is reconstructable from logs alone.
- Tag the OpenTelemetry resource with a distinct service name (`ConfigureResource(r => r.AddService("<ServiceName>"))`) per service so traces are attributable in the tracing backend.

## When NOT to apply

None observed — every networked/message-driven service in a distributed system benefits from this baseline observability; the only variation is which exporter endpoint (Seq, another OTLP collector) is configured per environment.
