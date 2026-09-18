using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

public class AdvisorSettingsCopyTests
{
    /// <summary>
    /// Every property, by reflection rather than by a list written down twice.
    /// A setting added to this type and forgotten in <c>Copy</c> would silently
    /// revert to its default for whoever took the copy, which is exactly the
    /// class of bug the spec's "one store, one owner" rule exists to prevent.
    /// </summary>
    [Fact]
    public void A_copy_carries_every_setting()
    {
        AdvisorSettings original = AllDifferentFromTheDefaults();
        AdvisorSettings copy = original.Copy();

        foreach (PropertyInfo property in typeof(AdvisorSettings).GetProperties(
                     BindingFlags.Public | BindingFlags.Instance))
        {
            object? mine = property.GetValue(original);
            object? theirs = property.GetValue(copy);

            if (mine is IEnumerable and not string)
            {
                Assert.Equal(
                    ((IEnumerable)mine).Cast<object>(),
                    ((IEnumerable)theirs!).Cast<object>());
                continue;
            }

            Assert.True(
                Equals(mine, theirs),
                $"{property.Name} was not carried into the copy: {mine} became {theirs}");
        }
    }

    /// <summary>
    /// The point of taking one: turning a knob on the copy leaves the original
    /// alone, so a reading of the preferences file stays a reading of it.
    /// </summary>
    [Fact]
    public void Changing_a_copy_leaves_the_original_alone()
    {
        AdvisorSettings original = AllDifferentFromTheDefaults();
        AdvisorSettings copy = original.Copy();

        copy.AcceptanceProbabilityThreshold = 0.9f;
        copy.AutoCounterOffer = false;

        Assert.Equal(0.42f, original.AcceptanceProbabilityThreshold);
        Assert.True(original.AutoCounterOffer);
    }

    /// <summary>
    /// Every value here differs from the fresh default, so a property the copy
    /// forgot shows up as the default rather than matching by luck.
    /// </summary>
    private static AdvisorSettings AllDifferentFromTheDefaults()
    {
        var settings = new AdvisorSettings
        {
            AutoCounterOffer = true,
            AutoHandover = true,
            HandoverFromAnywhere = true,
            MayUseAHigherGrade = true,
            AcceptanceProbabilityThreshold = 0.42f,
        };

        AssertNothingWasLeftAtItsDefault(settings);
        return settings;
    }

    private static void AssertNothingWasLeftAtItsDefault(AdvisorSettings settings)
    {
        var fresh = new AdvisorSettings();

        foreach (PropertyInfo property in typeof(AdvisorSettings).GetProperties(
                     BindingFlags.Public | BindingFlags.Instance))
        {
            object? varied = property.GetValue(settings);
            object? untouched = property.GetValue(fresh);

            if (varied is IEnumerable and not string)
            {
                Assert.NotEmpty(((IEnumerable)varied).Cast<object>());
                continue;
            }

            Assert.False(
                Equals(varied, untouched),
                $"{property.Name} is still at its default here, so this test could not tell "
                + "whether a copy carried it");
        }
    }
}
