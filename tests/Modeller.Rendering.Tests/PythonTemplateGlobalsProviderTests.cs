using System.Collections.Immutable;
using Modeller.Generation;
using Modeller.Model;
using Xunit;

namespace Modeller.Rendering.Tests;

public sealed class PythonTemplateGlobalsProviderTests
{
    [Fact]
    public void Entity_projection_snake_cases_fields_and_resolves_every_property_and_relationship_shape()
    {
        var revision = ChildCareRevision(out var status, out var booking);
        var provider = new PythonTemplateGlobalsProvider(revision, "pkg", "Proj", "3.13");

        var globals = provider.GetGlobals(Context(revision, booking));

        var definition = Assert.IsAssignableFrom<object>(globals["definition"]);
        Assert.Equal("Booking", Get<string>(definition, "name"));
        Assert.Equal("booking", Get<string>(definition, "module_name"));

        var properties = Get<System.Collections.IEnumerable>(definition, "properties").Cast<object>().ToArray();
        Assert.Contains(properties, p => Get<string>(p, "name") == "status" && Get<string>(p, "type") == "BookingStatus" && !Get<bool>(p, "nullable"));
        Assert.Contains(properties, p => Get<string>(p, "name") == "note" && Get<string>(p, "type") == "str" && !Get<bool>(p, "nullable"));
        Assert.Contains(properties, p => Get<string>(p, "name") == "grace_period" && Get<string>(p, "type") == "int" && Get<bool>(p, "nullable"));
        Assert.Contains(properties, p => Get<string>(p, "name") == "room" && Get<string>(p, "type") == "UUID" && !Get<bool>(p, "nullable"));
        Assert.Contains(properties, p => Get<string>(p, "name") == "absence" && Get<string>(p, "type") == "UUID" && Get<bool>(p, "nullable"));
        Assert.Contains(properties, p => Get<string>(p, "name") == "attendances" && Get<string>(p, "type") == "list[UUID]" && !Get<bool>(p, "nullable"));

        var imports = Get<System.Collections.IEnumerable>(definition, "imports").Cast<string>().ToArray();
        Assert.Contains("from ..enumerations.booking_status import BookingStatus", imports);
    }

    private static ArtifactRenderingContext Context(AuthoredContextRevision revision, EntityDefinition entity)
    {
        var artifact = new ProposedArtifact(
            0, "entity:booking", "entities/booking.py",
            new ArtifactOwnership("test", "test", "1.0.0", "entity"),
            [new PlannedSemanticInput(entity.Slug.Value, revision.Id.ToString(), "sha256:booking")],
            "sha256:template", "sha256:input");
        var plan = new GenerationPlan("1.0", "generated", [artifact], "sha256:plan");
        return new ArtifactRenderingContext(plan, artifact);
    }

    private static AuthoredContextRevision ChildCareRevision(out EnumerationDefinition status, out EntityDefinition booking)
    {
        var revision = AuthoredContextRevision.Create(SemanticId.New(), new SemanticName("Child Care"), new SemanticSlug("child-care"), "1.0.0");

        status = new EnumerationDefinition(SemanticId.New(), new SemanticName("Booking status"), new SemanticSlug("booking-status"),
            [new EnumerationMember(SemanticId.New(), new SemanticName("Active"), new SemanticSlug("active"), 1)]);
        var afterEnum = CanonicalModel.Apply(revision, new AddDefinition(status));
        Assert.True(afterEnum.Succeeded);

        var room = new EntityDefinition(SemanticId.New(), new SemanticName("Room"), new SemanticSlug("room"), Lifecycle: null);
        var afterRoom = CanonicalModel.Apply(afterEnum.Revision, new AddDefinition(room));
        Assert.True(afterRoom.Succeeded);

        var absence = new EntityDefinition(SemanticId.New(), new SemanticName("Absence"), new SemanticSlug("absence"), Lifecycle: null);
        var afterAbsence = CanonicalModel.Apply(afterRoom.Revision, new AddDefinition(absence));
        Assert.True(afterAbsence.Succeeded);

        var attendance = new EntityDefinition(SemanticId.New(), new SemanticName("Attendance"), new SemanticSlug("attendance"), Lifecycle: null);
        var afterAttendance = CanonicalModel.Apply(afterAbsence.Revision, new AddDefinition(attendance));
        Assert.True(afterAttendance.Succeeded);

        booking = new EntityDefinition(SemanticId.New(), new SemanticName("Booking"), new SemanticSlug("booking"), Lifecycle: null)
        {
            Fields =
            [
                new FieldDefinition(SemanticId.New(), new SemanticName("Status"), new SemanticSlug("status"), new EnumerationDataType(status.Id)),
                new FieldDefinition(SemanticId.New(), new SemanticName("Note"), new SemanticSlug("note"), new StringDataType(), IsOptional: true),
                new FieldDefinition(SemanticId.New(), new SemanticName("Grace period"), new SemanticSlug("grace-period"), new Int32DataType(), IsOptional: true)
            ],
            Relationships =
            [
                new RelationshipDefinition(SemanticId.New(), new SemanticName("Room"), new SemanticSlug("room"), room.Id, RelationshipCardinality.One),
                new RelationshipDefinition(SemanticId.New(), new SemanticName("Absence"), new SemanticSlug("absence"), absence.Id, RelationshipCardinality.One, IsOptional: true),
                new RelationshipDefinition(SemanticId.New(), new SemanticName("Attendances"), new SemanticSlug("attendances"), attendance.Id, RelationshipCardinality.Many)
            ]
        };
        var afterBooking = CanonicalModel.Apply(afterAttendance.Revision, new AddDefinition(booking));
        Assert.True(afterBooking.Succeeded);

        return afterBooking.Revision;
    }

    private static T Get<T>(object source, string property)
    {
        var value = source.GetType().GetProperty(property)!.GetValue(source);
        return (T)value!;
    }
}
