// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;

using EnvironmentVariablesUILib.Helpers;
using EnvironmentVariablesUILib.Models;

namespace EnvironmentVariablesUILib.UnitTests.Helpers;

[TestClass]
public class EnvironmentVariableComparisonHelperTests
{
    [TestMethod]
    public void NamesEqual_IgnoresCase()
    {
        Assert.IsTrue(EnvironmentVariableComparisonHelper.NamesEqual("PATH", "path"));
    }

    [TestMethod]
    public void EntriesEqual_NameIsCaseInsensitiveButValueIsOrdinal()
    {
        var upperName = new Variable("PATH", "Value", VariablesSetType.User);
        var lowerNameSameValue = new Variable("path", "Value", VariablesSetType.System);
        var lowerNameDifferentValueCase = new Variable("path", "value", VariablesSetType.System);

        Assert.IsTrue(EnvironmentVariableComparisonHelper.EntriesEqual(upperName, lowerNameSameValue));
        Assert.IsFalse(EnvironmentVariableComparisonHelper.EntriesEqual(upperName, lowerNameDifferentValueCase));
    }

    [TestMethod]
    public void GetDuplicateNameGroups_ReturnsLegacyEntriesThatDifferOnlyByCase()
    {
        var first = new Variable("PATH", "SystemValue", VariablesSetType.System);
        var second = new Variable("path", "UserValue", VariablesSetType.User);
        var variables = new[]
        {
            first,
            second,
            new Variable("TEMP", "TempValue", VariablesSetType.User),
        };

        var duplicates = EnvironmentVariableComparisonHelper.GetDuplicateNameGroups(variables).ToList();

        Assert.AreEqual(1, duplicates.Count);
        CollectionAssert.AreEquivalent(new[] { first, second }, duplicates[0].ToList());
    }

    [TestMethod]
    public void GetDuplicateNameGroups_IgnoresSameSpellingAcrossScopes()
    {
        var variables = new[]
        {
            new Variable("PROFILE_VAR", "ProfileValue", VariablesSetType.Profile),
            new Variable("PROFILE_VAR", "UserValue", VariablesSetType.User),
            new Variable("PROFILE_VAR", "SystemValue", VariablesSetType.System),
        };

        var duplicates = EnvironmentVariableComparisonHelper.GetDuplicateNameGroups(variables).ToList();

        Assert.AreEqual(0, duplicates.Count);
    }
}
