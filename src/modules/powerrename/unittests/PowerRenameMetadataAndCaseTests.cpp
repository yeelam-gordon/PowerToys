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
                { L"codec-independent suffix eligibility", L"photo.HEIC", false, true },
                { L"AVIF eligibility", L"photo.avif", false, true },
                { L"unsupported file", L"photo.txt", false, false },
                { L"suffix in stem only", L"photo.jpg.txt", false, false },
                { L"no suffix", L"photo", false, false },
                { L"empty supplied path", L"", false, false },
                { L"folder resembling image", L"album.jpg", true, false },
                { L"optional path omitted", nullptr, false, true },
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
            };
            const TestCase cases[] = {
                { L"EXIF camera", L"$CAMERA_MAKE", MetadataType::EXIF, true },
                { L"XMP excludes camera", L"$CAMERA_MAKE", MetadataType::XMP, false },
                { L"EXIF excludes title", L"$TITLE", MetadataType::EXIF, false },
                { L"XMP title", L"$TITLE", MetadataType::XMP, true },
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
                const bool actual = isMetadataUsed(test.source, test.type, L"photo.jpg", false);

                Assert::AreEqual(test.expected, actual, test.label);
            }
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
