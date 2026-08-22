# QA procedure: A centre records its service offerings, operating hours, and registration details

Proves that `samples/child-care` can capture a centre's service offerings,
weekly operating hours, service care type, Australian Company Number, and
geographic location, as a deterministic addition to the sample.

## Preconditions

- A checkout of this repository with the .NET SDK installed.
- Run commands from the repository root.

## Steps

1. **Confirm the current gap.**
   Open `samples/child-care/model/entities/centre.modeller`. Confirm none
   of ServiceOfferings, OperatingHours, ACN, ServiceCareType, or a location
   field are present.

2. **Add the service offering entity and relationship.**
   Add a new entity file for a service offering (name and description
   fields), and a "many" relationship "Service offerings" on Centre
   targeting it.

3. **Add the operating hours entity and relationship.**
   Add a new entity file for an operating-hours entry: a day-of-week
   enumeration field, an opening time field, and a closing time field. Add
   a "many" relationship "Operating hours" on Centre targeting it. Reuse an
   existing week-day enumeration if one is already declared in the
   workspace; otherwise add one.

4. **Add the service care type enumeration and field.**
   Add a "Service care type" enumeration (`CBC`, `FDC`, `OSHC`, matching the
   legacy `ServiceCareType` enum) and a required "Service care type" field
   on Centre using it.

5. **Add the ACN and location fields.**
   Add an optional "Australian Company Number" string field on Centre. Add
   a "Location" field on Centre using the `coordinate` data type (a single
   geographic-coordinate field replaces the legacy's separate
   Latitude/Longitude pair).

6. **Declare the new sources and mint identities.**
   Add every new file to `samples/child-care/.modeller/config.json`. Follow
   `docs/getting-started/create-definition.md` to mint UUIDv7 identities for
   each new entity, enumeration, and field/relationship line, in document
   order.

7. **Preview the change.**
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care --dry-run
   ```
   Confirm the preview lists only the expected files as changed, and every
   other file as unchanged. Confirm the command exits successfully.

8. **Generate for real.**
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care
   ```
   Confirm it exits successfully. Open Centre's generated file and confirm
   it exposes the service-offerings and operating-hours relationships, the
   service-care-type field, the optional ACN field, and the location field.

9. **Confirm idempotence.**
   Run the same `generate` command a second time. Confirm every file is
   reported as `Unchanged`.

10. **Confirm the projections still work.**
    Run:
    ```
    dotnet run --project src/Modeller.Cli -- project --workspace samples/child-care --view Structural
    ```
    Confirm Centre shows edges to the new service-offering and
    operating-hours entities, and still shows its existing Rooms
    relationship unchanged (Rooms stays a direct relationship — this change
    does not touch it).

11. **Update the sample's own documentation.**
    In `samples/child-care/gaps.md`, remove `ServiceOfferings`,
    `OperatingHours`, `ACN`, `ServiceCareType`, and `Longitude`/`Latitude`
    from the Centre bullet, keeping the sentence about `StructureNodes` and
    the direct Rooms relationship (that part is still an accurate,
    deliberate simplification). Update `samples/child-care/README.md`'s
    "Current slice" section to mention the centre's service offerings,
    operating hours, and registration details.

## Pass criteria

- Steps 7–9 all succeed with no errors, and step 9's second generation
  reports no changes.
- A centre can be authored with service offerings, operating hours, a
  service care type, an ACN, and a location.
- A centre with no ACN still compiles (the field is optional).
- Centre's Rooms relationship and structure-node simplification are
  untouched.
- `gaps.md` and `README.md` reflect what is now covered.
