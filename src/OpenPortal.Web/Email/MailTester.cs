using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Notifications.Application.Abstractions;
using OpenPortal.Notifications.Application.Contracts;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Web.Email;

/// <summary>
/// Sends one email straight away (not through the queue) with the settings an administrator is editing, and
/// reports the mail server's answer, so a wrong password or port shows up before the settings are saved.
/// </summary>
public sealed class MailTester
{
    private readonly IMailSettingsService _settings;
    private readonly IMailTransport _transport;
    private readonly HostAccountMessaging _messaging;

    public MailTester(IMailSettingsService settings, IMailTransport transport, HostAccountMessaging messaging)
    {
        _settings = settings;
        _transport = transport;
        _messaging = messaging;
    }

    public async Task<Result<MailTestResultDto>> SendAsync(TestMailSettingsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var resolved = await _settings.ResolveForTestAsync(request, cancellationToken).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            return Result<MailTestResultDto>.Failure(resolved.Error);
        }

        var to = request.To.Trim();
        var message = _messaging.Render(new AccountMessage(
            "test",
            to,
            null,
            null,
            "/admin/settings?tab=email",
            new Dictionary<string, string?> { ["host"] = resolved.Value.Host }));

        string? error = null;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(45));

            await _transport.SendAsync(MailTransportSettings.From(resolved.Value), message, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // The server's answer is the result of a test, not a failure of the request.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            error = exception is OperationCanceledException
                ? "The mail server did not answer in time."
                : exception.GetBaseException().Message;
        }

        await _settings.RecordTestAsync(to, error is null, cancellationToken).ConfigureAwait(false);

        return Result<MailTestResultDto>.Success(new MailTestResultDto(error is null, error));
    }
}
