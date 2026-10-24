// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class PowerRename
    {
        public const string GeneralSettingsFileName = "Test\\PowerRename";

        private static readonly string[] ExpectedModuleMessageRootKeys = { "powertoys" };

        private static readonly string[] ExpectedPowerRenameModuleKeys = { "PowerRename" };

        private Mock<SettingsUtils> mockGeneralSettingsUtils;

        private Mock<SettingsUtils> mockPowerRenamePropertiesUtils;

        [TestInitialize]
        public void SetUpStubSettingUtils()
        {
            mockGeneralSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();
            mockPowerRenamePropertiesUtils = ISettingsUtilsMocks.GetStubSettingsUtils<PowerRenameLocalProperties>();
        }

        /// <summary>
        /// Test if the original settings files were modified.
        /// </summary>
        [TestMethod]
        [DataRow("v0.18.2", "power-rename-settings.json")]
        [DataRow("v0.19.2", "power-rename-settings.json")]
        [DataRow("v0.20.1", "power-rename-settings.json")]
        [DataRow("v0.22.0", "power-rename-settings.json")]
        public void OriginalFilesModificationTest(string version, string fileName)
        {
            var settingPathMock = new Mock<SettingPath>();
            var mockIOProvider = BackCompatTestProperties.GetModuleIOProvider(version, PowerRenameSettings.ModuleName, fileName);

            var mockSettingsUtils = new SettingsUtils(mockIOProvider.Object, settingPathMock.Object);
            PowerRenameLocalProperties originalSettings = mockSettingsUtils.GetSettingsOrDefault<PowerRenameLocalProperties>(PowerRenameSettings.ModuleName);

            var mockGeneralIOProvider = BackCompatTestProperties.GetGeneralSettingsIOProvider(version);

            var mockGeneralSettingsUtils = new SettingsUtils(mockGeneralIOProvider.Object, settingPathMock.Object);
            GeneralSettings originalGeneralSettings = mockGeneralSettingsUtils.GetSettingsOrDefault<GeneralSettings>();
            var generalSettingsRepository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(mockGeneralSettingsUtils);

            // Initialise View Model with test Config files
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockSettingsUtils, generalSettingsRepository, sendMockIPCConfigMSG);

            // Verify that the old settings persisted
            Assert.AreEqual(originalGeneralSettings.Enabled.PowerRename, viewModel.IsEnabled);
            Assert.AreEqual(originalSettings.ExtendedContextMenuOnly, viewModel.EnabledOnContextExtendedMenu);
            Assert.AreEqual(originalSettings.MRUEnabled, viewModel.MRUEnabled);
            Assert.AreEqual(originalSettings.ShowIcon, viewModel.EnabledOnContextMenu);

            // Verify that the stub file was used
            var expectedCallCount = 2;  // once via the view model, and once by the test (GetSettings<T>)
            BackCompatTestProperties.VerifyModuleIOProviderWasRead(mockIOProvider, PowerRenameSettings.ModuleName, expectedCallCount);
            BackCompatTestProperties.VerifyGeneralSettingsIOProviderWasRead(mockGeneralIOProvider, expectedCallCount);
        }

        [TestMethod]
        public void IsEnabledShouldEnableModuleWhenSuccessful()
        {
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsTrue(snd.GeneralSettings.Enabled.PowerRename);
                return 0;
            };

            // arrange
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, GeneralSettingsFileName);

            // act
            viewModel.IsEnabled = true;
        }

        [TestMethod]
        public void MRUEnabledShouldSetValue2TrueWhenSuccessful()
        {
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                PowerRenameSettingsIPCMessage snd = JsonSerializer.Deserialize<PowerRenameSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.PowerRename.Properties.MRUEnabled.Value);
                return 0;
            };

            // arrange
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, GeneralSettingsFileName);

            // act
            viewModel.MRUEnabled = true;
        }

        [TestMethod]
        public void WhenIsEnabledIsOffAndMRUEnabledIsOffGlobalAndMruShouldBeOff()
        {
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, GeneralSettingsFileName);

            viewModel.IsEnabled = false;
            viewModel.MRUEnabled = false;

            Assert.IsFalse(viewModel.GlobalAndMruEnabled);
        }

        [TestMethod]
        public void WhenIsEnabledIsOffAndMRUEnabledIsOnGlobalAndMruShouldBeOff()
        {
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, GeneralSettingsFileName);

            viewModel.IsEnabled = false;
            viewModel.MRUEnabled = true;

            Assert.IsFalse(viewModel.GlobalAndMruEnabled);
        }

        [TestMethod]
        public void WhenIsEnabledIsOnAndMRUEnabledIsOffGlobalAndMruShouldBeOff()
        {
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, GeneralSettingsFileName);

            viewModel.IsEnabled = true;
            viewModel.MRUEnabled = false;

            Assert.IsFalse(viewModel.GlobalAndMruEnabled);
        }

        [TestMethod]
        public void WhenIsEnabledIsOnAndMRUEnabledIsOnGlobalAndMruShouldBeOn()
        {
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, GeneralSettingsFileName);

            viewModel.IsEnabled = true;
            viewModel.MRUEnabled = true;

            Assert.IsTrue(viewModel.GlobalAndMruEnabled);
        }

        [TestMethod]
        public void EnabledOnContextMenuShouldSetValue2TrueWhenSuccessful()
        {
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                PowerRenameSettingsIPCMessage snd = JsonSerializer.Deserialize<PowerRenameSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.PowerRename.Properties.ShowIcon.Value);
                return 0;
            };

            // arrange
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, GeneralSettingsFileName);

            // act
            viewModel.EnabledOnContextMenu = true;
        }

        [TestMethod]
        public void EnabledOnContextExtendedMenuShouldSetValue2TrueWhenSuccessful()
        {
            var messages = new List<string>();
            var localSettings = new PowerRenameLocalProperties();
            PowerRenameViewModel viewModel = CreateCapturingViewModel(localSettings, new GeneralSettings(), messages);

            viewModel.EnabledOnContextExtendedMenu = true;

            Assert.IsTrue(viewModel.EnabledOnContextExtendedMenu);
            Assert.IsFalse(viewModel.EnabledOnContextMenu);
            Assert.IsTrue(messages.Count > 0);
            foreach (string message in messages)
            {
                PowerRenameSettingsIPCMessage sent = JsonSerializer.Deserialize<PowerRenameSettingsIPCMessage>(message);
                Assert.IsTrue(sent.Powertoys.PowerRename.Properties.ExtendedContextMenuOnly.Value);
                Assert.IsFalse(sent.Powertoys.PowerRename.Properties.ShowIcon.Value);
            }
        }

        [TestMethod]
        [DataRow(nameof(PowerRenameViewModel.EnabledOnContextMenu), "bool_show_icon_on_menu")]
        [DataRow(nameof(PowerRenameViewModel.EnabledOnContextExtendedMenu), "bool_show_extended_menu")]
        [DataRow(nameof(PowerRenameViewModel.MRUEnabled), "bool_mru_enabled")]
        [DataRow(nameof(PowerRenameViewModel.RestoreFlagsOnLaunch), "bool_persist_input")]
        [DataRow(nameof(PowerRenameViewModel.UseBoostLib), "bool_use_boost_lib")]
        public void ModuleBooleanSetterShouldSerializeTransitionsAndRetainOtherValues(string propertyName, string serializedKey)
        {
            var messages = new List<string>();
            var localSettings = new PowerRenameLocalProperties
            {
                ShowIcon = true,
                ExtendedContextMenuOnly = true,
                MRUEnabled = true,
                PersistState = true,
                UseBoostLib = true,
                MaxMRUSize = 17,
            };
            var generalSettings = new GeneralSettings();
            PowerRenameViewModel viewModel = CreateCapturingViewModel(localSettings, generalSettings, messages);
            var expectedProperties = new Dictionary<string, bool>
            {
                ["bool_show_icon_on_menu"] = true,
                ["bool_show_extended_menu"] = true,
                ["bool_mru_enabled"] = true,
                ["bool_persist_input"] = true,
                ["bool_use_boost_lib"] = true,
            };
            (Action<bool> setValue, Func<bool> getValue) = GetModuleBooleanAccessors(viewModel, propertyName);

            Assert.IsTrue(getValue());
            foreach (bool value in new[] { false, true, false })
            {
                messages.Clear();
                setValue(value);
                expectedProperties[serializedKey] = value;

                Assert.AreEqual(value, getValue());
                AssertModuleValues(viewModel, expectedProperties, 17);
                AssertModuleMessages(messages, expectedProperties, 17);
                Assert.IsTrue(generalSettings.Enabled.PowerRename);

                messages.Clear();
                setValue(value);
                Assert.AreEqual(value, getValue());
                Assert.AreEqual(0, messages.Count, "Assigning the current value must not send IPC.");
            }
        }

        [TestMethod]
        [DataRow(false, false, false)]
        [DataRow(false, true, false)]
        [DataRow(true, false, false)]
        [DataRow(true, true, false)]
        [DataRow(false, false, true)]
        [DataRow(false, true, true)]
        [DataRow(true, false, true)]
        [DataRow(true, true, true)]
        public void ContextMenuSettingsShouldRemainIndependentOfEachOtherAndEnabledState(bool showIcon, bool extendedMenu, bool enabled)
        {
            var messages = new List<string>();
            var localSettings = new PowerRenameLocalProperties
            {
                ShowIcon = !showIcon,
                ExtendedContextMenuOnly = !extendedMenu,
                MRUEnabled = true,
                PersistState = true,
                UseBoostLib = true,
                MaxMRUSize = 17,
            };
            var generalSettings = new GeneralSettings
            {
                Startup = true,
                Theme = "dark",
            };
            generalSettings.Enabled.PowerRename = !enabled;
            PowerRenameViewModel viewModel = CreateCapturingViewModel(localSettings, generalSettings, messages);
            if (viewModel.IsEnabledGpoConfigured)
            {
                Assert.Inconclusive("An enforced PowerRename policy prevents testing user-controlled enabled transitions.");
            }

            viewModel.IsEnabled = enabled;

            Assert.AreEqual(enabled, viewModel.IsEnabled);
            var generalMessages = new List<string>();
            var moduleMessages = new List<string>();
            foreach (string message in messages)
            {
                using JsonDocument document = JsonDocument.Parse(message);
                string[] destinations = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();
                Assert.AreEqual(1, destinations.Length);
                if (destinations[0] == "general")
                {
                    generalMessages.Add(message);
                    JsonElement general = document.RootElement.GetProperty("general");
                    Assert.AreEqual(enabled, general.GetProperty("enabled").GetProperty("PowerRename").GetBoolean());
                    Assert.IsTrue(general.GetProperty("enabled").GetProperty("FancyZones").GetBoolean());
                    Assert.IsTrue(general.GetProperty("startup").GetBoolean());
                    Assert.AreEqual("dark", general.GetProperty("theme").GetString());
                }
                else
                {
                    Assert.AreEqual("powertoys", destinations[0]);
                    moduleMessages.Add(message);
                }
            }

            Assert.IsTrue(generalMessages.Count > 0, "An enabled transition must send general settings.");
            var expectedProperties = new Dictionary<string, bool>
            {
                ["bool_show_icon_on_menu"] = !showIcon,
                ["bool_show_extended_menu"] = !extendedMenu,
                ["bool_mru_enabled"] = true,
                ["bool_persist_input"] = true,
                ["bool_use_boost_lib"] = true,
            };
            AssertModuleValues(viewModel, expectedProperties, 17);
            AssertModuleMessages(moduleMessages, expectedProperties, 17);

            messages.Clear();
            viewModel.EnabledOnContextMenu = showIcon;
            expectedProperties["bool_show_icon_on_menu"] = showIcon;
            AssertModuleValues(viewModel, expectedProperties, 17);
            AssertModuleMessages(messages, expectedProperties, 17);

            messages.Clear();
            viewModel.EnabledOnContextExtendedMenu = extendedMenu;
            expectedProperties["bool_show_extended_menu"] = extendedMenu;
            AssertModuleValues(viewModel, expectedProperties, 17);
            AssertModuleMessages(messages, expectedProperties, 17);
            Assert.AreEqual(enabled, viewModel.IsEnabled);
            Assert.AreEqual(enabled, generalSettings.Enabled.PowerRename);

            messages.Clear();
            viewModel.IsEnabled = enabled;
            viewModel.EnabledOnContextMenu = showIcon;
            viewModel.EnabledOnContextExtendedMenu = extendedMenu;
            Assert.AreEqual(0, messages.Count, "Assigning the current enabled/menu values must not send IPC.");
        }

        [TestMethod]
        public void RestoreFlagsOnLaunchShouldSetValue2TrueWhenSuccessful()
        {
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                PowerRenameSettingsIPCMessage snd = JsonSerializer.Deserialize<PowerRenameSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.PowerRename.Properties.PersistState.Value);
                return 0;
            };

            // arrange
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, GeneralSettingsFileName);

            // act
            viewModel.RestoreFlagsOnLaunch = true;
        }

        [TestMethod]
        public void MaxDispListNumShouldSetMaxSuggListTo20WhenSuccessful()
        {
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                PowerRenameSettingsIPCMessage snd = JsonSerializer.Deserialize<PowerRenameSettingsIPCMessage>(msg);
                Assert.AreEqual(20, snd.Powertoys.PowerRename.Properties.MaxMRUSize.Value);
                return 0;
            };

            // arrange
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, GeneralSettingsFileName);

            // act
            viewModel.MaxDispListNum = 20;
        }

        private PowerRenameViewModel CreateCapturingViewModel(PowerRenameLocalProperties localSettings, GeneralSettings generalSettings, List<string> messages)
        {
            mockPowerRenamePropertiesUtils
                .Setup(settings => settings.GetSettingsOrDefault<PowerRenameLocalProperties>(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(localSettings);
            mockGeneralSettingsUtils
                .Setup(settings => settings.GetSettingsOrDefault<GeneralSettings>(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(generalSettings);
            var repository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(mockGeneralSettingsUtils.Object);
            return new PowerRenameViewModel(
                mockPowerRenamePropertiesUtils.Object,
                repository,
                message =>
                {
                    messages.Add(message);
                    return 0;
                },
                GeneralSettingsFileName);
        }

        private static (Action<bool> SetValue, Func<bool> GetValue) GetModuleBooleanAccessors(PowerRenameViewModel viewModel, string propertyName)
        {
            return propertyName switch
            {
                nameof(PowerRenameViewModel.EnabledOnContextMenu) => (value => viewModel.EnabledOnContextMenu = value, () => viewModel.EnabledOnContextMenu),
                nameof(PowerRenameViewModel.EnabledOnContextExtendedMenu) => (value => viewModel.EnabledOnContextExtendedMenu = value, () => viewModel.EnabledOnContextExtendedMenu),
                nameof(PowerRenameViewModel.MRUEnabled) => (value => viewModel.MRUEnabled = value, () => viewModel.MRUEnabled),
                nameof(PowerRenameViewModel.RestoreFlagsOnLaunch) => (value => viewModel.RestoreFlagsOnLaunch = value, () => viewModel.RestoreFlagsOnLaunch),
                nameof(PowerRenameViewModel.UseBoostLib) => (value => viewModel.UseBoostLib = value, () => viewModel.UseBoostLib),
                _ => throw new ArgumentOutOfRangeException(nameof(propertyName)),
            };
        }

        private static void AssertModuleValues(PowerRenameViewModel viewModel, Dictionary<string, bool> expectedProperties, int maxMruSize)
        {
            Assert.AreEqual(expectedProperties["bool_show_icon_on_menu"], viewModel.EnabledOnContextMenu);
            Assert.AreEqual(expectedProperties["bool_show_extended_menu"], viewModel.EnabledOnContextExtendedMenu);
            Assert.AreEqual(expectedProperties["bool_mru_enabled"], viewModel.MRUEnabled);
            Assert.AreEqual(expectedProperties["bool_persist_input"], viewModel.RestoreFlagsOnLaunch);
            Assert.AreEqual(expectedProperties["bool_use_boost_lib"], viewModel.UseBoostLib);
            Assert.AreEqual(maxMruSize, viewModel.MaxDispListNum);
        }

        private static void AssertModuleMessages(List<string> messages, Dictionary<string, bool> expectedProperties, int maxMruSize)
        {
            Assert.IsTrue(messages.Count > 0, "A changed module setting must send IPC.");
            foreach (string message in messages)
            {
                using JsonDocument document = JsonDocument.Parse(message);
                CollectionAssert.AreEqual(ExpectedModuleMessageRootKeys, document.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
                JsonElement modules = document.RootElement.GetProperty("powertoys");
                CollectionAssert.AreEqual(ExpectedPowerRenameModuleKeys, modules.EnumerateObject().Select(property => property.Name).ToArray());
                JsonElement properties = modules.GetProperty("PowerRename").GetProperty("properties");
                CollectionAssert.AreEquivalent(expectedProperties.Keys.Append("int_max_mru_size").ToArray(), properties.EnumerateObject().Select(property => property.Name).ToArray());
                foreach (KeyValuePair<string, bool> expected in expectedProperties)
                {
                    Assert.AreEqual(expected.Value, properties.GetProperty(expected.Key).GetProperty("value").GetBoolean(), expected.Key);
                }

                Assert.AreEqual(maxMruSize, properties.GetProperty("int_max_mru_size").GetProperty("value").GetInt32());
            }
        }
    }
}
