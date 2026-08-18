// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Awake.ModuleServices.UnitTests;

[TestClass]
public sealed class AwakeStateMappingTests
{
    [TestMethod]
    public void CreateState_NullSettings_ReturnsPassiveState()
    {
        var state = AwakeService.CreateState(isRunning: false, settings: null);

        Assert.IsFalse(state.IsRunning);
        Assert.AreEqual(AwakeStateMode.Passive, state.Mode);
        Assert.IsFalse(state.KeepDisplayOn);
        Assert.IsNull(state.Duration);
        Assert.IsNull(state.Expiration);
    }

    [TestMethod]
    public void CreateState_PassiveSettings_ReturnsPassiveState()
    {
        var settings = new AwakeSettings
        {
            Properties =
            {
                Mode = AwakeMode.PASSIVE,
                KeepDisplayOn = false,
                IntervalHours = 1,
                IntervalMinutes = 30,
                ExpirationDateTime = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero),
            },
        };

        var state = AwakeService.CreateState(isRunning: true, settings: settings);

        Assert.IsTrue(state.IsRunning);
        Assert.AreEqual(AwakeStateMode.Passive, state.Mode);
        Assert.IsFalse(state.KeepDisplayOn);
        Assert.IsNull(state.Duration);
        Assert.IsNull(state.Expiration);
    }

    [TestMethod]
    public void CreateState_IndefiniteSettings_ReturnsIndefiniteState()
    {
        var settings = new AwakeSettings
        {
            Properties =
            {
                Mode = AwakeMode.INDEFINITE,
                KeepDisplayOn = true,
            },
        };

        var state = AwakeService.CreateState(isRunning: true, settings: settings);

        Assert.IsTrue(state.IsRunning);
        Assert.AreEqual(AwakeStateMode.Indefinite, state.Mode);
        Assert.IsTrue(state.KeepDisplayOn);
        Assert.IsNull(state.Duration);
        Assert.IsNull(state.Expiration);
    }

    [TestMethod]
    public void CreateState_TimedSettings_ReturnsTimedStateWithDuration()
    {
        var settings = new AwakeSettings
        {
            Properties =
            {
                Mode = AwakeMode.TIMED,
                KeepDisplayOn = true,
                IntervalHours = 1,
                IntervalMinutes = 30,
                ExpirationDateTime = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero),
            },
        };

        var state = AwakeService.CreateState(isRunning: true, settings: settings);

        Assert.IsTrue(state.IsRunning);
        Assert.AreEqual(AwakeStateMode.Timed, state.Mode);
        Assert.IsTrue(state.KeepDisplayOn);
        Assert.AreEqual(TimeSpan.FromMinutes(90), state.Duration);
        Assert.IsNull(state.Expiration);
    }

    [TestMethod]
    public void CreateState_ExpirableSettings_ReturnsExpirableStateWithExpiration()
    {
        var expiration = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var settings = new AwakeSettings
        {
            Properties =
            {
                Mode = AwakeMode.EXPIRABLE,
                KeepDisplayOn = true,
                IntervalHours = 1,
                IntervalMinutes = 30,
                ExpirationDateTime = expiration,
            },
        };

        var state = AwakeService.CreateState(isRunning: true, settings: settings);

        Assert.IsTrue(state.IsRunning);
        Assert.AreEqual(AwakeStateMode.Expirable, state.Mode);
        Assert.IsTrue(state.KeepDisplayOn);
        Assert.IsNull(state.Duration);
        Assert.AreEqual(expiration, state.Expiration);
    }
}
