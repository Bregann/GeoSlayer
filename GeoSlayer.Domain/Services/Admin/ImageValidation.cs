namespace GeoSlayer.Domain.Services.Admin
{
    /// <summary>
    /// What may be uploaded as an item image (Stage 18 task 3).
    ///
    /// <para><b>An admin tool is still an upload path.</b> The role check stops a player
    /// reaching it, but it does not make the bytes trustworthy — an admin account can be
    /// compromised, and an admin can be wrong. Everything here validates the file itself
    /// rather than trusting who sent it.</para>
    ///
    /// <para>Pure and static so every rule can be asserted without a database or a web
    /// request, which is what makes them cheap enough to test exhaustively.</para>
    /// </summary>
    public static class ImageValidation
    {
        /// <summary>
        /// The largest image accepted, in bytes.
        ///
        /// <para>2 MB. These are item icons rendered at a few dozen pixels on a phone; two
        /// megabytes is already far more than any of them needs, and the limit exists to stop
        /// a mistake filling the database rather than to be generous.</para>
        /// </summary>
        public const int MaxSizeBytes = 2 * 1024 * 1024;

        /// <summary>Content types accepted, and the extension each maps to.</summary>
        private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/png"] = ".png",
            ["image/jpeg"] = ".jpg",
            ["image/webp"] = ".webp",
        };

        /// <summary>
        /// Magic bytes per format.
        ///
        /// <para>The declared content type is a <i>claim</i>, not a fact — it comes from the
        /// client and can say anything. Checking the leading bytes is what stops an HTML file
        /// labelled <c>image/png</c> being stored and later served from our own origin, which
        /// is the actual attack this guards against.</para>
        /// </summary>
        private static readonly (string ContentType, byte[] Magic, int Offset)[] Signatures =
        [
            ("image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], 0),
            ("image/jpeg", [0xFF, 0xD8, 0xFF], 0),

            // WebP is "RIFF????WEBP" — the four size bytes in between are not fixed, so the
            // format is identified by its second marker at offset 8.
            ("image/webp", [0x57, 0x45, 0x42, 0x50], 8),
        ];

        /// <summary>Why an upload was refused, or null when it is acceptable.</summary>
        /// <param name="contentType">The declared type. Checked, never trusted.</param>
        /// <param name="data">The file's bytes.</param>
        public static string? Reject(string? contentType, byte[]? data)
        {
            if (data is null || data.Length == 0)
            {
                return "No file provided.";
            }

            if (data.Length > MaxSizeBytes)
            {
                return $"Image is {data.Length / 1024}KB; the limit is {MaxSizeBytes / 1024}KB.";
            }

            if (string.IsNullOrWhiteSpace(contentType) || !AllowedTypes.ContainsKey(contentType))
            {
                return $"Only {string.Join(", ", AllowedTypes.Keys)} are accepted.";
            }

            if (!MatchesSignature(contentType, data))
            {
                // Deliberately blunt. This is the case where the declared type and the actual
                // bytes disagree, which is either a corrupt file or an attempt to smuggle
                // something — and neither should be stored.
                return "That file is not really a " + contentType + ".";
            }

            return null;
        }

        /// <summary>Whether the bytes actually look like the type they claim to be.</summary>
        public static bool MatchesSignature(string contentType, byte[] data)
        {
            foreach (var (type, magic, offset) in Signatures)
            {
                if (!string.Equals(type, contentType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (data.Length < offset + magic.Length)
                {
                    return false;
                }

                for (var i = 0; i < magic.Length; i++)
                {
                    if (data[offset + i] != magic[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        /// <summary>The canonical extension for an accepted type.</summary>
        public static string ExtensionFor(string contentType) =>
            AllowedTypes.GetValueOrDefault(contentType, ".bin");

        /// <summary>
        /// A filename safe to store and echo back.
        ///
        /// <para>The original name is client-supplied and reaches the admin interface, so it
        /// is stripped of any path and of anything that is not plainly a filename. A name
        /// like <c>../../etc/passwd</c> is not dangerous here — nothing writes to disk — but
        /// storing it would leave a trap for whoever later decides that it should.</para>
        /// </summary>
        public static string SafeFileName(string? fileName, string contentType)
        {
            var extension = ExtensionFor(contentType);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return $"image{extension}";
            }

            // Both separators, because a Windows client's name reaches a Linux server.
            var bare = fileName.Replace('\\', '/');
            bare = bare[(bare.LastIndexOf('/') + 1)..];

            var cleaned = new string([.. bare.Where(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_')]);

            if (cleaned.Length == 0 || cleaned.All(c => c == '.'))
            {
                return $"image{extension}";
            }

            return cleaned.Length > 200 ? cleaned[^200..] : cleaned;
        }
    }
}
