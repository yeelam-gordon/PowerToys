// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "Helpers.h"
#include <locale>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace PowerRenameMetadataAndCaseTests
{
    TEST_CLASS (MetadataAndCaseTests)
    {
    public:
        TEST_METHOD (MetadataUsageRespectsSuppliedFileEligibility)
        {
            struct TestCase
            {
                const wchar_t* label;
                const wchar_t* path;
                bool isFolder;
                bool expected;
            };
            const TestCase cases[] = {
                { L"uppercase suffix", L"photo.JPG", false, true },
                { L"alternative JPEG suffix", L"photo.jpeg", false, true },
                { L"PNG", L"photo.png", false, true },
                { L"short TIFF suffix", L"photo.tif", false, true },
                { L"long TIFF suffix", L"photo.tiff", false, true },
                { L"codec-independent suffix eligibility", L"photo.HEIC", false, true },
                { L"alternative HEIF suffix", L"photo.heif", false, true },
                { L"AVIF eligibility", L"photo.avif", false, true },
                { L"unsupported file", L"photo.txt", false, false },
                { L"suffix in stem only", L"photo.jpg.txt", false, false },
                { L"no suffix", L"photo", false, false },
                { L"empty supplied path", L"", false, false },
                { L"folder resembling image", L"album.jpg", true, false },
                { L"optional path omitted", nullptr, false, true },
                { L"omitted path skips folder gate", nullptr, true, true },
            };

            for (const auto& test : cases)
            {
                const bool actual = isMetadataUsed(L"$CAMERA_MAKE", PowerRenameLib::MetadataType::EXIF, test.path, test.isFolder);

                Assert::AreEqual(test.expected, actual, test.label);
            }
        }

        TEST_METHOD (MetadataUsageRespectsSelectedSourceAndRecognizedToken)
        {
            using PowerRenameLib::MetadataType;
            struct TestCase
            {
                const wchar_t* label;
                const wchar_t* source;
                MetadataType type;
                bool expected;
                const wchar_t* path = L"photo.jpg";
                bool isFolder = false;
            };
            const TestCase cases[] = {
                { L"EXIF camera", L"$CAMERA_MAKE", MetadataType::EXIF, true },
                { L"XMP excludes camera", L"$CAMERA_MAKE", MetadataType::XMP, false },
                { L"EXIF excludes title", L"$TITLE", MetadataType::EXIF, false },
                { L"XMP title", L"$TITLE", MetadataType::XMP, true },
                { L"XMP title in unsupported file", L"$TITLE", MetadataType::XMP, false, L"photo.txt", false },
                { L"XMP title in image-named folder", L"$TITLE", MetadataType::XMP, false, L"album.jpg", true },
                { L"EXIF author", L"$AUTHOR", MetadataType::EXIF, true },
                { L"XMP author", L"$AUTHOR", MetadataType::XMP, true },
                { L"EXIF date taken", L"$DATE_TAKEN_YYYY", MetadataType::EXIF, true },
                { L"XMP excludes date taken", L"$DATE_TAKEN_YYYY", MetadataType::XMP, false },
                { L"EXIF excludes create date", L"$CREATE_DATE_YYYY", MetadataType::EXIF, false },
                { L"XMP create date", L"$CREATE_DATE_YYYY", MetadataType::XMP, true },
                { L"null template", nullptr, MetadataType::EXIF, false },
                { L"empty template", L"", MetadataType::EXIF, false },
                { L"plain name", L"plain name", MetadataType::EXIF, false },
                { L"unsupported token", L"$NOT_A_METADATA_TOKEN", MetadataType::EXIF, false },
                { L"case-sensitive token", L"$camera_make", MetadataType::EXIF, false },
            };

            for (const auto& test : cases)
            {
                const bool actual = isMetadataUsed(test.source, test.type, test.path, test.isFolder);

                Assert::AreEqual(test.expected, actual, test.label);
            }
        }

        TEST_METHOD (MetadataFormatterRejectsNullAndEmptySource)
        {
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"CAMERA_MAKE"] = L"Canon";

            struct TestCase
            {
                const wchar_t* label;
                const wchar_t* source;
            };
            const TestCase cases[] = {
                { L"null source", nullptr },
                { L"empty source", L"" },
            };

            for (const auto& test : cases)
            {
                wchar_t result[MAX_PATH]{};

                const HRESULT hr = GetMetadataFileName(result, ARRAYSIZE(result), test.source, patterns);

                Assert::AreEqual(E_INVALIDARG, hr, test.label);
            }
        }

        TEST_METHOD (MetadataFormatterSubstitutesSuppliedExifAuthorGpsAndAdditionalFields)
        {
            const PowerRenameLib::MetadataPatternMap patterns = {
                { L"AUTHOR", L"Ada" },
                { L"COPYRIGHT", L"ACME" },
                { L"LATITUDE", L"47N" },
                { L"LONGITUDE", L"122W" },
                { L"EXPOSURE_BIAS", L"-0.33" },
                { L"ORIENTATION", L"1" },
                { L"COLOR_SPACE", L"sRGB" },
                { L"ALTITUDE", L"12m" },
            };
            wchar_t result[MAX_PATH]{};

            const HRESULT hr = GetMetadataFileName(result, ARRAYSIZE(result), L"$AUTHOR|$COPYRIGHT|$LATITUDE|$LONGITUDE|$EXPOSURE_BIAS|$ORIENTATION|$COLOR_SPACE|$ALTITUDE", patterns);

            Assert::AreEqual(S_OK, hr);
            Assert::AreEqual(L"Ada|ACME|47N|122W|-0.33|1|sRGB|12m", result);
        }

        TEST_METHOD (MetadataFormatterSubstitutesSuppliedXmpDescriptionRightsAndProvenance)
        {
            const PowerRenameLib::MetadataPatternMap patterns = {
                { L"DESCRIPTION", L"Lake" },
                { L"SUBJECT", L"sky; lake" },
                { L"RIGHTS", L"CC" },
                { L"CREATOR_TOOL", L"Editor" },
                { L"DOCUMENT_ID", L"doc" },
                { L"INSTANCE_ID", L"instance" },
                { L"ORIGINAL_DOCUMENT_ID", L"original" },
                { L"VERSION_ID", L"v2" },
            };
            wchar_t result[MAX_PATH]{};

            const HRESULT hr = GetMetadataFileName(result, ARRAYSIZE(result), L"$DESCRIPTION|$SUBJECT|$RIGHTS|$CREATOR_TOOL|$DOCUMENT_ID|$INSTANCE_ID|$ORIGINAL_DOCUMENT_ID|$VERSION_ID", patterns);

            Assert::AreEqual(S_OK, hr);
            Assert::AreEqual(L"Lake|sky; lake|CC|Editor|doc|instance|original|v2", result);
        }

        TEST_METHOD (MetadataFormatterDistinguishesSuppliedCreateDateMonthAndMissingMinute)
        {
            PowerRenameLib::MetadataPatternMap patterns = {
                { L"CREATE_DATE_YYYY", L"2026" },
                { L"CREATE_DATE_YY", L"26" },
                { L"CREATE_DATE_MM", L"07" },
                { L"CREATE_DATE_DD", L"02" },
                { L"CREATE_DATE_HH", L"11" },
                { L"CREATE_DATE_mm", L"54" },
                { L"CREATE_DATE_SS", L"09" },
            };
            const wchar_t* source = L"$CREATE_DATE_YYYY-$CREATE_DATE_YY-$CREATE_DATE_MM-$CREATE_DATE_DD__$CREATE_DATE_HH-$CREATE_DATE_mm-$CREATE_DATE_SS";
            wchar_t completeResult[MAX_PATH]{};

            const HRESULT completeHr = GetMetadataFileName(completeResult, ARRAYSIZE(completeResult), source, patterns);

            Assert::AreEqual(S_OK, completeHr);
            Assert::AreEqual(L"2026-26-07-02__11-54-09", completeResult);

            patterns.erase(L"CREATE_DATE_mm");
            wchar_t missingMinuteResult[MAX_PATH]{};

            const HRESULT missingMinuteHr = GetMetadataFileName(missingMinuteResult, ARRAYSIZE(missingMinuteResult), source, patterns);

            Assert::AreEqual(S_OK, missingMinuteHr);
            Assert::AreEqual(L"2026-26-07-02__11-$CREATE_DATE_mm-09", missingMinuteResult);
        }

        TEST_METHOD (CaseModesDistinguishSmallWordsAndPreserveExtension)
        {
            // GetTransformedFileName changes the process-global C++ locale.
            struct RestoreLocale
            {
                std::locale saved;
                ~RestoreLocale()
                {
                    std::locale::global(saved);
                }
            } restoreLocale;

            struct TestCase
            {
                const wchar_t* label;
                const wchar_t* source;
                DWORD flags;
                bool isFolder;
                const wchar_t* expected;
            };
            const TestCase cases[] = {
                { L"Capitalized includes small words", L"wAR aND pEACE.TxT", Capitalized, false, L"War And Peace.TxT" },
                { L"Titlecase preserves small-word exception", L"wAR aND pEACE.TxT", Titlecase, false, L"War and Peace.TxT" },
                { L"Capitalized NameOnly retains extension", L"wAR aND pEACE.TxT", Capitalized | NameOnly, false, L"War And Peace.TxT" },
                { L"Capitalized ExtensionOnly is unchanged", L"wAR aND pEACE.TxT", Capitalized | ExtensionOnly, false, L"wAR aND pEACE.TxT" },
                { L"Titlecase ExtensionOnly is unchanged", L"wAR aND pEACE.TxT", Titlecase | ExtensionOnly, false, L"wAR aND pEACE.TxT" },
                { L"Capitalized without extension", L"wAR aND pEACE", Capitalized, false, L"War And Peace" },
                { L"folder dot is not a file extension", L"wAR aND pEACE.TxT", Capitalized, true, L"War And Peace.Txt" },
                { L"contraction apostrophe", L"don't STOP", Capitalized, false, L"Don't Stop" },
                { L"opening quote starts word", L"'hELLO' wORLD", Capitalized, false, L"'Hello' World" },
            };

            for (const auto& test : cases)
            {
                wchar_t result[MAX_PATH]{};

                const HRESULT hr = GetTransformedFileName(result, ARRAYSIZE(result), test.source, test.flags, test.isFolder);

                Assert::AreEqual(S_OK, hr, test.label);
                Assert::AreEqual(test.expected, result, test.label);
            }
        }
    };
}
