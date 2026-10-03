namespace OpenPortal.Web.Resources;

/// <summary>
/// Marker type for the <c>Messages*.resx</c> files in this folder. <c>IStringLocalizer&lt;Messages&gt;</c>
/// resolves keys against them: the neutral file holds English and each <c>Messages.{culture}.resx</c> adds a
/// language, so a new language is a new file and a line in <c>Localization:Languages</c>, never new code.
/// </summary>
public sealed class Messages;
