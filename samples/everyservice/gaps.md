# Truthful coverage gaps

The current Device domain has no truthful use for `byte`, `int16`, or `int64`:
quantities and revisions are normal 32-bit business counts. It also has no
truthful use for a raw binary field. The model uses text, boolean, int32,
decimal precision, date, time, datetime, datetimeoffset, identifier, and
coordinate where the domain needs them.

EveryService has more than one business boundary, but this generated sample is
one context. Current workspace generation emits one canonical context package;
cross-context imports cannot preserve entity relationships across generated
packages. A future multi-context generation contract is required before the
Member Registry fact can be imported truthfully by the trusted-service model.
