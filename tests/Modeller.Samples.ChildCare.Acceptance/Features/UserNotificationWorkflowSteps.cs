using Modeller.Model;
using Modeller.Parsing;
using Reqnroll;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

[Binding]
public sealed class UserNotificationWorkflowSteps
{
    private readonly WorkspaceCompilationContext _context;
    private NotificationState _state = NotificationState.NotificationDraft;
    private string _type = "User";

    public UserNotificationWorkflowSteps(WorkspaceCompilationContext context)
    {
        _context = context;
        _context.Compile = Compile;
    }

    [Given("the bounded user notification model has been added to the sample workspace")]
    public void GivenTheBoundedUserNotificationModelHasBeenAdded() { }

    [Given("the user notification workflow starts from a draft notification")]
    public void GivenTheWorkflowStartsFromADraftNotification()
    {
        _state = NotificationState.NotificationDraft;
        _type = "User";
    }

    [Given("a user notification is New")]
    public void GivenAUserNotificationIsNew() => _state = NotificationState.NotificationNew;

    [Given("a user notification is Completed")]
    public void GivenAUserNotificationIsCompleted() => _state = NotificationState.NotificationCompleted;

    [When("a user notification is created")]
    public void WhenAUserNotificationIsCreated()
    {
        if (_state == NotificationState.NotificationDraft)
        {
            _state = NotificationState.NotificationNew;
            _type = "User";
        }
    }

    [When("the user views the notification")]
    public void WhenTheUserViewsTheNotification()
    {
        if (_state == NotificationState.NotificationNew)
        {
            _state = NotificationState.NotificationViewed;
        }
    }

    [When("the user completes the notification")]
    public void WhenTheUserCompletesTheNotification()
    {
        if (_state == NotificationState.NotificationViewed)
        {
            _state = NotificationState.NotificationCompleted;
        }
    }

    [When("the user tries to view it as a new notification again")]
    public void WhenTheUserTriesToViewItAsNewAgain()
    {
        if (_state == NotificationState.NotificationNew)
        {
            _state = NotificationState.NotificationViewed;
        }
    }

    [Then("a user notification belongs to one organisation")]
    public void ThenNotificationBelongsToOrganisation() =>
        AssertModelContains("entities/user-notification.modeller", "owner \"Organisation\"");

    [Then("a user notification identifies one user")]
    public void ThenNotificationIdentifiesUser()
    {
        AssertModelContains("entities/user-notification.modeller", "relationship User");
        AssertModelContains("entities/user-notification.modeller", "target \"User\"");
        AssertModelContains("entities/user-notification.modeller", "cardinality one");
    }

    [Then("a user notification records a subject, description, and optional URL")]
    public void ThenNotificationRecordsContent()
    {
        AssertModelContains("entities/user-notification.modeller", "field Subject");
        AssertModelContains("entities/user-notification.modeller", "field Description");
        AssertModelContains("entities/user-notification.modeller", "field Url");
        AssertModelContains("entities/user-notification.modeller", "optional");
    }

    [Then("a user notification has a user notification type and status")]
    public void ThenNotificationHasTypeAndStatus()
    {
        AssertModelContains("entities/user-notification.modeller", "type enumeration \"User notification type\"");
        AssertModelContains("entities/user-notification.modeller", "type enumeration \"User notification status\"");
        AssertModelContains("enumerations/user-notification-type.modeller", "member User");
        AssertModelContains("enumerations/user-notification-type.modeller", "member Centre");
        AssertModelContains("enumerations/user-notification-type.modeller", "member Provider");
        AssertModelContains("enumerations/user-notification-status.modeller", "member New");
        AssertModelContains("enumerations/user-notification-status.modeller", "member Viewed");
        AssertModelContains("enumerations/user-notification-status.modeller", "member Completed");
    }

    [Then("the notification is New")]
    public void ThenNotificationIsNew() => Assert.Equal(NotificationState.NotificationNew, _state);

    [Then("the notification is for the User audience")]
    public void ThenNotificationIsForUserAudience() => Assert.Equal("User", _type);

    [Then("the notification is Viewed")]
    public void ThenNotificationIsViewed() => Assert.Equal(NotificationState.NotificationViewed, _state);

    [Then("the notification is Completed")]
    [Then("the notification stays Completed")]
    public void ThenNotificationIsCompleted() => Assert.Equal(NotificationState.NotificationCompleted, _state);

    private void Compile()
    {
        var result = RmlCompiler.Compile([new SourceDocument("notification.rml", BuildSource())], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken);
        _context.IsSuccess = result.IsSuccess;
        _context.FailureSummary = string.Join("; ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));
    }

    private static string BuildSource() => """
        rml 1.0
        context Child Care
          version 1.0.0
        end
        entity Organisation
        end
        entity User
        end
        enumeration User notification type
          member User
            value 1
          end
          member Centre
            value 2
          end
          member Provider
            value 3
          end
        end
        enumeration User notification status
          member New
            value 1
          end
          member Viewed
            value 2
          end
          member Completed
            value 3
          end
        end
        entity User notification
          owner "Organisation"
          lifecycle User notification lifecycle
            stage Notification Draft
            stage Notification New
            stage Notification Viewed
            stage Notification Completed
          end
          relationship User
            target "User"
            cardinality one
          end
          field Subject
            type string
          end
          field Description
            type string
          end
          field Url
            type string
            optional
          end
          field Type
            type enumeration "User notification type"
          end
          field Status
            type enumeration "User notification status"
          end
        end
        """;

    private static void AssertModelContains(string relativePath, string expected)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../samples/child-care/model"));
        Assert.Contains(expected, File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar))));
    }

    private enum NotificationState
    {
        NotificationDraft,
        NotificationNew,
        NotificationViewed,
        NotificationCompleted
    }
}
