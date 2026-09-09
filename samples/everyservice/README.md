# EveryService sample

This is a business model of trusted local service work. It models Members,
their Businesses and Service offerings, private-first Connections and
Recommendations, Job requests, itemised Quotes, and Jobs. It excludes account
access, UI, storage, APIs, messages, payment brokering, and infrastructure.

The model has one bounded context: EveryService. Member lifetime is explicit:
pending activation, active, suspended, deactivated, and banned. Activation
needs a valid contact; only suspended and deactivated Members can reactivate.
Members can hold verified contact methods and profile attributes with a
visibility choice, block another Member, join a Community, and complete
provider onboarding before professional verification is granted. Professional
evidence belongs to the provider Business. Notification preferences control
connection, recommendation, quote/job, Community, and service-offering feed
notices. Businesses have locations and Service offerings have active coverage.
Its key flows are connection
proposal, private acceptance and scoped publication; recommendation consent,
publication, withdrawal, and invalidation; job request fulfilment through an
accepted quote; and scheduled job completion confirmed by the consumer.

It exercises entities, aggregate ownership, fields, optional fields,
one/many relationships, enumerations, decimal precision, lifecycle-scoped
members, facts and exports, rules with true/false/missing findings, guarded
behaviours, outcomes, transitions, events, and effects. The existing C#
domain-project pack is the only pack used.

The RML source is split by feature under `model/`: `entities/` separates
Member, Provider, Community, Trust, and Work; `enumerations/` separates
Member and trust terms; facts, rules, and behaviours have their own files.
`context.modeller` owns the bounded-context declaration.

Validate, preview, generate, and build:

```powershell
dotnet run --project src/Modeller.Cli -- generate --workspace samples/everyservice
dotnet build samples/everyservice/generated/EveryService.slnx
```

Run the generation command a second time. All artifacts must report
`unchanged`. Use `project --workspace samples/everyservice --view Lifecycle`
or `RuleDecision` to list projection roots, then add `--root <id>` to render a
representative view.
