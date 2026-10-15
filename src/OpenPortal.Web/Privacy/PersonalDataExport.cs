using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.SharedKernel.Privacy;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Web.Privacy;

/// <summary>
/// Builds the copy of a person's data they can download: one ZIP with <c>data.json</c> (a section per module, from
/// every registered <see cref="IPersonalDataSource"/>), the profile picture when there is one, and a short README.
/// The modules never reference each other, so the host gathers them here.
/// </summary>
public sealed class PersonalDataExport
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly IEnumerable<IPersonalDataSource> _sources;
    private readonly IUserAvatarService _avatars;
    private readonly IClock _clock;

    public PersonalDataExport(IEnumerable<IPersonalDataSource> sources, IUserAvatarService avatars, IClock clock)
    {
        _sources = sources;
        _avatars = avatars;
        _clock = clock;
    }

    public async Task<(byte[] Content, string FileName)> BuildAsync(Guid userId, CancellationToken cancellationToken)
    {
        var generatedAt = _clock.UtcNow;
        var sections = new SortedDictionary<string, object>(StringComparer.Ordinal);

        foreach (var source in _sources)
        {
            sections[source.Section] = await source.ExportAsync(userId, cancellationToken).ConfigureAwait(false);
        }

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            await WriteAsync(
                    zip,
                    "data.json",
                    JsonSerializer.SerializeToUtf8Bytes(new { generatedAtUtc = generatedAt, userId, sections }, Json),
                    cancellationToken)
                .ConfigureAwait(false);

            var avatar = await _avatars.GetAsync(userId, cancellationToken).ConfigureAwait(false);
            if (avatar.IsSuccess)
            {
                var extension = avatar.Value.ContentType switch
                {
                    "image/png" => "png",
                    "image/jpeg" => "jpg",
                    _ => "webp",
                };

                await WriteAsync(zip, $"profile-picture.{extension}", avatar.Value.Content, cancellationToken).ConfigureAwait(false);
            }

            await WriteAsync(zip, "README.txt", Encoding.UTF8.GetBytes(Readme(generatedAt)), cancellationToken).ConfigureAwait(false);
        }

        var fileName = $"openportal-my-data-{generatedAt.UtcDateTime.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}.zip";

        return (buffer.ToArray(), fileName);
    }

    private static async Task WriteAsync(ZipArchive zip, string name, byte[] content, CancellationToken cancellationToken)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        var stream = entry.Open();
        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string Readme(DateTimeOffset generatedAt) =>
        $"""
        OpenPortal - a copy of your personal data
        Generated: {generatedAt.UtcDateTime.ToString("u", CultureInfo.InvariantCulture)}

        data.json            Everything the portal holds about you, by section:
                               account        your profile, roles, sign-in facts and accepted documents
                               access         groups, organizations, application grants, access requests
                                              and the applications you signed in to
                               notifications  your notifications
                               activity       the security log entries about you
        profile-picture.*    Your profile picture, if you set one.

        Passwords, second-factor keys and tokens are never included.
        """;
}
