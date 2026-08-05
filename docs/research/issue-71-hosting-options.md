---
title: Issue 71 hosting options
description: Can the stateless .NET API in Issue #71 be hosted on Vercel, and what are the strongest alternatives?
---

Date: 2026-08-04

## Question

Can the stateless .NET API in [Issue #71](https://github.com/Allann/Modeller.Next/issues/71) be hosted on Vercel, and what are the strongest alternatives?

## Short answer

**Yes.** As of 30 June 2026, Vercel Functions can run an OCI-compatible image built from a `Dockerfile.vercel` or `Containerfile.vercel`. That makes an ordinary containerized ASP.NET Core HTTP server a supported deployment shape even though .NET is not one of Vercel's native source runtimes. Vercel builds and stores the image, routes HTTP traffic to it, autoscales it, and scales it to zero when idle ([Vercel announcement](https://vercel.com/changelog/bring-your-dockerfile-to-vercel-functions), [Docker guide](https://vercel.com/kb/guide/docker)).

This capability is unusually new. A short deployment spike should precede committing #71 to Vercel.

## Fit with Issue #71

Issue #71 explicitly requires the API to be stateless, packaged as a container independently of the frontend, bounded by time/concurrency/request limits, observable, and rollable back. The repository currently targets .NET 10 and contains no ASP.NET Core host, container definition, or deployment pipeline, so platform choice is unconstrained by existing backend infrastructure.

Vercel's container functions align well with the required stateless HTTP shape:

- Any OCI-packaged HTTP server can run; the process listens on the platform port. A `Dockerfile.vercel` is built, stored in Vercel Container Registry, and deployed as an autoscaling Function ([Vercel Docker guide](https://vercel.com/kb/guide/docker)).
- The API and frontend may be separate services in one Vercel project, with rewrites routing `/api/*` to the backend. They can also remain independent projects, which more literally matches the issue wording ([Vercel Docker guide](https://vercel.com/kb/guide/docker)).
- Container functions are stateless and scale to zero. Production instances without traffic scale in after five minutes and receive `SIGTERM` with a 30-second grace period ([Vercel Docker guide](https://vercel.com/kb/guide/docker)).
- Container images inherit normal Function limits and Active CPU pricing. Current published Function limits include a 4.5 MB request/response payload ceiling, 2 GB/1 vCPU on Hobby, and up to 4 GB/2 vCPU on Pro/Enterprise; maximum duration is plan-dependent ([Vercel Functions limits](https://vercel.com/docs/functions/limitations), [container-image limitations](https://vercel.com/kb/guide/does-vercel-support-docker-deployments)). These platform limits complement, but do not replace, #71's stricter application-level limits and cancellation.
- Vercel currently does not support Secure Compute or Static IPs for custom container images. Neither appears necessary for the anonymous playground API, but this should be rechecked if the service later needs private networking or outbound IP allowlisting ([Vercel container-image limitations](https://vercel.com/kb/guide/does-vercel-support-docker-deployments)).

The main caution is maturity, not feasibility: first-class container deployment is only weeks old. The implementation should prove .NET 10 startup, health checks, cancellation on disconnect/termination, deployment rollback, observability export, and the no-source-logging policy before locking in the platform. In particular, log only request metadata and bounded operational measurements—never submitted RML or identity registries.

## Strong alternatives

| Platform | Fit | Important trade-off |
| --- | --- | --- |
| **Azure Container Apps** | Excellent direct container fit. Managed HTTPS ingress, immutable revisions, traffic splitting, secrets, logs, and autoscaling; HTTP apps default to 0–10 replicas and can scale to zero ([overview](https://learn.microsoft.com/en-us/azure/container-apps/overview), [scaling](https://learn.microsoft.com/en-us/azure/container-apps/scale-app)). | Mature .NET/Azure path and explicit revision controls; separate cloud/platform from the Vercel frontend. Scale-to-zero introduces cold-start latency; set `minReplicas >= 1` if latency matters. |
| **Google Cloud Run** | Excellent generic container fit. It explicitly supports .NET or any language that builds to a container, gives a stable HTTPS endpoint, stateless autoscaling instances, traffic splitting, rollback, and scale to zero ([Cloud Run overview](https://docs.cloud.google.com/run/docs/overview/what-is-cloud-run)). | Very close match to #71 and mature container operations; separate platform. A request that wakes a zero-scaled service has added startup latency. |
| **Azure App Service** | Good conventional ASP.NET Core PaaS when a continuously available process is preferred ([ASP.NET Core quickstart](https://learn.microsoft.com/azure/app-service/quickstart-dotnetcore), [scaling](https://learn.microsoft.com/en-us/azure/app-service/manage-scale-up)). | Simpler steady-state latency, but baseline always-on capacity/cost rather than the clean scale-to-zero model of Vercel, Container Apps, or Cloud Run. |
| **AWS Lambda** | Technically viable with .NET container images, including an official .NET 10 base image ([AWS .NET container images](https://docs.aws.amazon.com/lambda/latest/dg/csharp-image.html)). | Requires Lambda's runtime interface/hosting model rather than running the container as an unchanged Kestrel service. It is a weaker fit than a serverless container host for the API described by #71. |

## Recommendation

Run a small **Vercel container deployment spike first**. The new capability now makes Vercel the lowest-friction operational option: frontend and backend can share previews, routing, domains, observability, and a deployment platform while the backend remains its own containerized service.

The spike should fail fast on these questions:

1. Does the .NET 10 image boot and remain comfortably within the selected plan's memory/CPU limits?
2. Can the API enforce a much shorter application timeout than Vercel's platform duration and reliably observe cancellation?
3. Do health checks, structured metadata-only logs, OpenTelemetry export, preview deployment, and rollback behave as #71 requires?
4. Is cold-start latency acceptable for the playground after scale-to-zero?

If any answer is unsatisfactory—or if avoiding a brand-new hosting feature is more important than one-platform convenience—choose **Azure Container Apps**. **Google Cloud Run** is an equally sound generic-container alternative. Keep the website on Vercel and call the external API over HTTPS with a narrow CORS policy.

## Decision summary

- Vercel is now **possible and supported via containers**, not via a native .NET runtime.
- It is plausibly the best first choice for this anonymous, stateless, bounded HTTP API, subject to a spike because support is new.
- Azure Container Apps is the conservative recommendation for a mature .NET-oriented serverless container host.
- Cloud Run is the strongest cloud-neutral alternative.
- Do not reshape the service into Azure Functions or AWS Lambda merely to host it; #71 already asks for a portable container, and ordinary serverless container platforms preserve that design.
