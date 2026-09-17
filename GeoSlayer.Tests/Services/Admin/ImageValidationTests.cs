using GeoSlayer.Domain.Services.Admin;

namespace GeoSlayer.Tests.Services.Admin
{
    /// <summary>
    /// Upload validation for item images (Stage 18 task 3).
    ///
    /// <para>The role check stops a player reaching this endpoint; it does not make the bytes
    /// trustworthy. These tests are about the file itself.</para>
    /// </summary>
    [TestFixture]
    public class ImageValidationTests
    {
        private static byte[] Png(int padding = 16) =>
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. new byte[padding]];

        private static byte[] Jpeg(int padding = 16) =>
            [0xFF, 0xD8, 0xFF, .. new byte[padding]];

        private static byte[] Webp(int padding = 16) =>
            [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, .. new byte[padding]];

        // ── The happy path ──────────────────────────────────────────────

        [Test]
        public void ARealPng_IsAccepted()
        {
            Assert.That(ImageValidation.Reject("image/png", Png()), Is.Null);
        }

        [Test]
        public void ARealJpeg_IsAccepted()
        {
            Assert.That(ImageValidation.Reject("image/jpeg", Jpeg()), Is.Null);
        }

        [Test]
        public void ARealWebp_IsAccepted()
        {
            // WebP's marker sits at offset 8, after RIFF and four size bytes — worth its own
            // test because an offset of zero would pass every other format and fail this one.
            Assert.That(ImageValidation.Reject("image/webp", Webp()), Is.Null);
        }

        // ── The declared type is a claim, not a fact ────────────────────

        [Test]
        public void HtmlClaimingToBeAPng_IsRejected()
        {
            // The attack this exists for: store a page as an "image", then serve it from our
            // own origin.
            var html = "<html><script>alert(1)</script></html>"u8.ToArray();

            Assert.That(ImageValidation.Reject("image/png", html), Is.Not.Null);
        }

        [Test]
        public void AJpegClaimingToBeAPng_IsRejected()
        {
            // Not malicious, but the stored content type is echoed back on read — so bytes
            // and label disagreeing would mean serving a JPEG as image/png.
            Assert.That(ImageValidation.Reject("image/png", Jpeg()), Is.Not.Null);
        }

        [Test]
        public void AFileTooShortToHaveASignature_IsRejected()
        {
            Assert.That(ImageValidation.Reject("image/png", [0x89, 0x50]), Is.Not.Null);
        }

        [Test]
        public void SomethingNamedLikeWebpButTooShort_IsRejected()
        {
            // Fewer than 12 bytes cannot carry the marker at offset 8; the check must not
            // read past the end.
            Assert.That(
                () => ImageValidation.Reject("image/webp", [0x52, 0x49, 0x46, 0x46]),
                Throws.Nothing);

            Assert.That(ImageValidation.Reject("image/webp", [0x52, 0x49, 0x46, 0x46]), Is.Not.Null);
        }

        // ── Type and size limits ────────────────────────────────────────

        [Test]
        public void AnUnsupportedType_IsRejected()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ImageValidation.Reject("image/gif", Png()), Is.Not.Null);
                Assert.That(ImageValidation.Reject("image/svg+xml", Png()), Is.Not.Null,
                    "SVG is a script host, not an icon format");
                Assert.That(ImageValidation.Reject("application/pdf", Png()), Is.Not.Null);
            });
        }

        [Test]
        public void AMissingContentType_IsRejected()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ImageValidation.Reject(null, Png()), Is.Not.Null);
                Assert.That(ImageValidation.Reject("", Png()), Is.Not.Null);
                Assert.That(ImageValidation.Reject("   ", Png()), Is.Not.Null);
            });
        }

        [Test]
        public void ContentTypeMatchingIsCaseInsensitive()
        {
            // Browsers are not consistent about this, and rejecting "IMAGE/PNG" would be a
            // confusing failure with no security benefit.
            Assert.That(ImageValidation.Reject("IMAGE/PNG", Png()), Is.Null);
        }

        [Test]
        public void AnEmptyFile_IsRejected()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ImageValidation.Reject("image/png", null), Is.Not.Null);
                Assert.That(ImageValidation.Reject("image/png", []), Is.Not.Null);
            });
        }

        [Test]
        public void AnOversizedFile_IsRejected()
        {
            var huge = new byte[ImageValidation.MaxSizeBytes + 1];
            huge[0] = 0x89; huge[1] = 0x50; huge[2] = 0x4E; huge[3] = 0x47;

            Assert.That(ImageValidation.Reject("image/png", huge), Is.Not.Null);
        }

        [Test]
        public void AFileExactlyAtTheLimit_IsAccepted()
        {
            var atLimit = new byte[ImageValidation.MaxSizeBytes];
            byte[] magic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
            magic.CopyTo(atLimit, 0);

            Assert.That(ImageValidation.Reject("image/png", atLimit), Is.Null,
                "the limit is inclusive; an off-by-one here is a confusing rejection");
        }

        // ── Filenames ───────────────────────────────────────────────────

        [Test]
        public void ATraversalAttempt_IsStrippedToABareName()
        {
            var safe = ImageValidation.SafeFileName("../../etc/passwd", "image/png");

            Assert.Multiple(() =>
            {
                Assert.That(safe, Does.Not.Contain("/"));
                Assert.That(safe, Does.Not.Contain(".."));
                Assert.That(safe, Is.EqualTo("passwd"));
            });
        }

        [Test]
        public void AWindowsPath_IsAlsoStripped()
        {
            // A Windows client's filename reaches a Linux server, where the backslash is an
            // ordinary character and would survive a naive split.
            Assert.That(
                ImageValidation.SafeFileName(@"C:\Users\someone\icon.png", "image/png"),
                Is.EqualTo("icon.png"));
        }

        [Test]
        public void AnEmptyOrAbsurdName_FallsBackToADefault()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ImageValidation.SafeFileName(null, "image/png"), Is.EqualTo("image.png"));
                Assert.That(ImageValidation.SafeFileName("", "image/jpeg"), Is.EqualTo("image.jpg"));
                Assert.That(ImageValidation.SafeFileName("...", "image/webp"), Is.EqualTo("image.webp"));
                Assert.That(ImageValidation.SafeFileName("///", "image/png"), Is.EqualTo("image.png"));
            });
        }

        [Test]
        public void AnAbsurdlyLongName_IsTruncated()
        {
            var long_ = new string('a', 500) + ".png";

            Assert.That(ImageValidation.SafeFileName(long_, "image/png").Length,
                Is.LessThanOrEqualTo(200));
        }

        [Test]
        public void ExtensionsMatchTheAcceptedTypes()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ImageValidation.ExtensionFor("image/png"), Is.EqualTo(".png"));
                Assert.That(ImageValidation.ExtensionFor("image/jpeg"), Is.EqualTo(".jpg"));
                Assert.That(ImageValidation.ExtensionFor("image/webp"), Is.EqualTo(".webp"));
            });
        }
    }
}
