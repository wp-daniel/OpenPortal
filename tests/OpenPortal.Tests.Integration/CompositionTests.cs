using Microsoft.Extensions.DependencyInjection;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Audit.Application.Abstractions;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Notifications.Application.Abstractions;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Messaging;
using OpenPortal.SharedKernel.Notifications;
using OpenPortal.SharedKernel.Privacy;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// The architecture tests assert that module services are internal, which means the only way a host can
/// obtain one is by resolving its published interface. Nothing else in the suite proves those registrations
/// actually exist, so this test does: it resolves every module contract from the real composition root.
/// <para>
/// A missing registration here means a controller or middleware fails only when that specific endpoint is
/// first called in production.
/// </para>
/// </summary>
public sealed class CompositionTests(OpenPortalFactory factory) : IClassFixture<OpenPortalFactory>
{
    public static TheoryData<Type> ModuleContracts() => new()
    {
        typeof(IAuthenticationService),
        typeof(IAccountService),
        typeof(ITwoFactorService),
        typeof(ISecuritySettingsService),
        typeof(IUserAdministrationService),
        typeof(IUserAvatarService),
        typeof(IExternalLoginSettingsService),
        typeof(IExternalLoginService),
        typeof(IExternalLoginProviders),
        typeof(ISessionService),
        typeof(IOrganizationAutoJoin),
        typeof(IAuthorizedApplicationService),
        typeof(IDomainOwnershipVerifier),
        typeof(IOrganizationInvitationAuthorization),
        typeof(ILanguageCatalog),
        typeof(IUserLookupService),
        typeof(IRegistrationService),
        typeof(IRegistrationSettingsService),
        typeof(IInvitationService),
        typeof(IUserStatisticsService),
        typeof(IAccountMessaging),
        typeof(IApplicationRegistryService),
        typeof(IGroupService),
        typeof(IAccessAdministrationService),
        typeof(IAccessEvaluator),
        typeof(IApplicationAnnouncementService),
        typeof(IApplicationApiService),
        typeof(IApplicationApiCaller),
        typeof(IAccessCaller),
        typeof(IAccessRequestService),
        typeof(IOrganizationService),
        typeof(IAccessStatisticsService),
        typeof(IAccessProvisioningService),
        typeof(IAccessAdminAuthorization),
        typeof(IUserDirectory),
        typeof(IPagePermissionService),
        typeof(IPagePermissionEvaluator),
        typeof(IPortalPageCatalog),
        typeof(IUserAdministrationAuthorization),
        typeof(IUserPageSource),
        typeof(IAuditTrail),
        typeof(IAuditLogService),
        typeof(IAuditStatisticsService),
        typeof(IAuditRequestContext),
        typeof(IAuditLogAuthorization),
        typeof(INotifier),
        typeof(INotificationService),
        typeof(INotificationMaintenance),
        typeof(INotificationContext),
        typeof(INotificationPreferenceService),
        typeof(INotificationMailer),
        typeof(IAccountErasure),
        typeof(IAccessUserMaintenance),
        typeof(IAnnouncementService),
        typeof(IAnnouncementAuthorization),
        typeof(ILegalDocumentService),
        typeof(IPrivacySettingsService),
        typeof(IAccountPrivacyService),
        typeof(IImpersonationService),
        typeof(IPersonalAccessTokenService),
        typeof(IPersonalAccessTokenValidator),
        typeof(IEnumerable<IPersonalDataSource>),
        typeof(IOutbox),
        typeof(IEnumerable<IOutboxHandler>),
        typeof(IOutboxAdministration),
        typeof(IOutboxMonitor),
        typeof(IWebhookDirectory),
        typeof(IAccessReviewService),
        typeof(INotificationPublisher),
        typeof(IPasskeyService),
        typeof(ITwoFactorPolicy),
        typeof(IPasswordRecoveryService),
        typeof(IMailSettingsService),
        typeof(IMailSettingsReader),
        typeof(IMailFallbackSource),
        typeof(IDeliveryAdministrationAuthorization),
    };

    [Theory]
    [MemberData(nameof(ModuleContracts))]
    public async Task Every_module_contract_resolves_from_the_composition_root(Type contract)
    {
        await using var provider = factory.Services.CreateAsyncScope();

        var instance = provider.ServiceProvider.GetService(contract);

        instance.ShouldNotBeNull($"{contract.Name} has no registration; the host cannot build without it.");
    }

    [Fact]
    public async Task Module_services_are_resolvable_for_every_request_scope()
    {
        // A registration that only works in the root scope would be scoped-capture or captive-dependency
        // trouble: it would appear to work here and then leak state between requests in production.
        await using var first = factory.Services.CreateAsyncScope();
        await using var second = factory.Services.CreateAsyncScope();

        var a = first.ServiceProvider.GetRequiredService<IUserAdministrationService>();
        var b = second.ServiceProvider.GetRequiredService<IUserAdministrationService>();

        a.ShouldNotBeSameAs(b);
    }
}