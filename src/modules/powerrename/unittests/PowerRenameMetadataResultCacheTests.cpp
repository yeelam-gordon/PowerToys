// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "MetadataResultCache.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace PowerRenameLib;

namespace PowerRenameMetadataResultCacheTests
{
    TEST_CLASS (MetadataResultCacheTests)
    {
    public:
        TEST_METHOD (SuccessfulLoads_SeparateKeysAndSources_ClearAllReloads)
        {
            MetadataResultCache cache;
            EXIFMetadata exif;
            XMPMetadata xmp;
            int exifCalls = 0;
            int xmpCalls = 0;
            int replacementCalls = 0;
            const auto canonLoader = [&](EXIFMetadata& metadata) {
                ++exifCalls;
                metadata.cameraMake = L"Canon";
                return true;
            };
            const auto nikonLoader = [&](EXIFMetadata& metadata) {
                ++exifCalls;
                metadata.cameraMake = L"Nikon";
                return true;
            };
            const auto titleLoader = [&](XMPMetadata& metadata) {
                ++xmpCalls;
                metadata.title = L"Original title";
                return true;
            };
            const auto replacementLoader = [&](EXIFMetadata& metadata) {
                ++replacementCalls;
                metadata.cameraMake = L"Replacement camera";
                return true;
            };

            Assert::IsTrue(cache.GetOrLoadEXIF(L"first-key", exif, canonLoader));
            Assert::IsTrue(exif.cameraMake == L"Canon");
            exif.cameraMake = L"Caller mutation";
            Assert::IsTrue(cache.GetOrLoadEXIF(L"first-key", exif, replacementLoader));
            Assert::IsTrue(exif.cameraMake == L"Canon");
            Assert::AreEqual(1, exifCalls);
            Assert::AreEqual(0, replacementCalls);

            Assert::IsTrue(cache.GetOrLoadEXIF(L"second-key", exif, nikonLoader));
            Assert::IsTrue(exif.cameraMake == L"Nikon");
            Assert::AreEqual(2, exifCalls);
            Assert::IsTrue(cache.GetOrLoadXMP(L"first-key", xmp, titleLoader));
            Assert::IsTrue(xmp.title == L"Original title");
            xmp.title = L"Caller mutation";
            Assert::IsTrue(cache.GetOrLoadXMP(L"first-key", xmp, titleLoader));
            Assert::IsTrue(xmp.title == L"Original title");
            Assert::AreEqual(1, xmpCalls);
            Assert::IsTrue(cache.GetOrLoadEXIF(L"first-key", exif, replacementLoader));
            Assert::IsTrue(exif.cameraMake == L"Canon");
            Assert::AreEqual(0, replacementCalls);

            cache.ClearAll();

            Assert::IsTrue(cache.GetOrLoadEXIF(L"first-key", exif, replacementLoader));
            Assert::IsTrue(exif.cameraMake == L"Replacement camera");
            Assert::IsTrue(cache.GetOrLoadEXIF(L"second-key", exif, replacementLoader));
            Assert::IsTrue(exif.cameraMake == L"Replacement camera");
            Assert::AreEqual(2, replacementCalls);
            const auto newTitleLoader = [&](XMPMetadata& metadata) {
                ++xmpCalls;
                metadata.title = L"Reloaded title";
                return true;
            };
            Assert::IsTrue(cache.GetOrLoadXMP(L"first-key", xmp, newTitleLoader));
            Assert::IsTrue(xmp.title == L"Reloaded title");
            Assert::AreEqual(2, xmpCalls);
        }

        TEST_METHOD (FailedLoads_CacheFailureAndPayload_UntilClearAllAllowsRecovery)
        {
            MetadataResultCache cache;
            EXIFMetadata exif;
            XMPMetadata xmp;
            int exifFailures = 0;
            int xmpFailures = 0;
            int exifRecoveries = 0;
            int xmpRecoveries = 0;
            const auto failingExifLoader = [&](EXIFMetadata& metadata) {
                ++exifFailures;
                metadata.cameraMake = L"Partial camera";
                return false;
            };
            const auto failingXmpLoader = [&](XMPMetadata& metadata) {
                ++xmpFailures;
                metadata.title = L"Partial title";
                return false;
            };
            const auto recoveringExifLoader = [&](EXIFMetadata& metadata) {
                ++exifRecoveries;
                metadata.cameraMake = L"Recovered camera";
                return true;
            };
            const auto recoveringXmpLoader = [&](XMPMetadata& metadata) {
                ++xmpRecoveries;
                metadata.title = L"Recovered title";
                return true;
            };

            Assert::IsFalse(cache.GetOrLoadEXIF(L"failed-key", exif, failingExifLoader));
            Assert::IsTrue(exif.cameraMake == L"Partial camera");
            Assert::IsFalse(cache.GetOrLoadXMP(L"failed-key", xmp, failingXmpLoader));
            Assert::IsTrue(xmp.title == L"Partial title");
            exif.cameraMake = L"Caller mutation";
            xmp.title = L"Caller mutation";
            Assert::IsFalse(cache.GetOrLoadEXIF(L"failed-key", exif, recoveringExifLoader));
            Assert::IsTrue(exif.cameraMake == L"Partial camera");
            Assert::IsFalse(cache.GetOrLoadXMP(L"failed-key", xmp, recoveringXmpLoader));
            Assert::IsTrue(xmp.title == L"Partial title");
            Assert::AreEqual(1, exifFailures);
            Assert::AreEqual(1, xmpFailures);
            Assert::AreEqual(0, exifRecoveries);
            Assert::AreEqual(0, xmpRecoveries);

            cache.ClearAll();

            Assert::IsTrue(cache.GetOrLoadEXIF(L"failed-key", exif, recoveringExifLoader));
            Assert::IsTrue(exif.cameraMake == L"Recovered camera");
            Assert::IsTrue(cache.GetOrLoadXMP(L"failed-key", xmp, recoveringXmpLoader));
            Assert::IsTrue(xmp.title == L"Recovered title");
            Assert::AreEqual(1, exifRecoveries);
            Assert::AreEqual(1, xmpRecoveries);
        }

        TEST_METHOD (EmptyLoaders_OnMissPreserveOutputAndDoNotCache_OnHitReturnCachedData)
        {
            MetadataResultCache cache;
            EXIFMetadata exif;
            XMPMetadata xmp;
            exif.cameraMake = L"Untouched camera";
            exif.iso = 640;
            xmp.title = L"Untouched title";
            xmp.creator = L"Untouched creator";
            const MetadataResultCache::EXIFLoader emptyExifLoader;
            const MetadataResultCache::XMPLoader emptyXmpLoader;

            Assert::IsFalse(cache.GetOrLoadEXIF(L"empty-key", exif, emptyExifLoader));
            Assert::IsTrue(exif.cameraMake == L"Untouched camera");
            Assert::IsTrue(exif.iso == 640);
            Assert::IsFalse(cache.GetOrLoadXMP(L"empty-key", xmp, emptyXmpLoader));
            Assert::IsTrue(xmp.title == L"Untouched title");
            Assert::IsTrue(xmp.creator == L"Untouched creator");

            int exifCalls = 0;
            int xmpCalls = 0;
            const auto exifLoader = [&](EXIFMetadata& metadata) {
                ++exifCalls;
                metadata.cameraMake = L"Loaded camera";
                return true;
            };
            const auto xmpLoader = [&](XMPMetadata& metadata) {
                ++xmpCalls;
                metadata.title = L"Loaded title";
                return true;
            };
            Assert::IsTrue(cache.GetOrLoadEXIF(L"empty-key", exif, exifLoader));
            Assert::IsTrue(exif.cameraMake == L"Loaded camera");
            Assert::IsFalse(exif.iso.has_value());
            Assert::IsTrue(cache.GetOrLoadXMP(L"empty-key", xmp, xmpLoader));
            Assert::IsTrue(xmp.title == L"Loaded title");
            Assert::IsFalse(xmp.creator.has_value());
            Assert::AreEqual(1, exifCalls);
            Assert::AreEqual(1, xmpCalls);

            exif.cameraMake = L"Caller mutation";
            xmp.title = L"Caller mutation";
            Assert::IsTrue(cache.GetOrLoadEXIF(L"empty-key", exif, emptyExifLoader));
            Assert::IsTrue(exif.cameraMake == L"Loaded camera");
            Assert::IsTrue(cache.GetOrLoadXMP(L"empty-key", xmp, emptyXmpLoader));
            Assert::IsTrue(xmp.title == L"Loaded title");
        }
    };
}
