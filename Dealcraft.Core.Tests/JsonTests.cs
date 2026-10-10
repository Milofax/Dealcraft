using System.Globalization;
using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

public class JsonTests
{
    [Fact]
    public void Members_keep_the_order_they_were_added_in()
    {
        Assert.Equal(
            "{\"b\":1,\"a\":2}",
            new JsonObject().Number("b", 1).Number("a", 2).ToString());
    }

    /// <summary>
    /// A ledger written on a machine whose decimal separator is a comma must
    /// still be a file <c>jq</c> can read, so nothing here may go through the
    /// current culture.
    /// </summary>
    [Fact]
    public void Numbers_are_written_with_a_decimal_point()
    {
        Assert.Equal("{\"x\":1.5}", new JsonObject().Number("x", 1.5f).ToString());
    }

    /// <summary>
    /// The satisfaction gate the Generosity Bonus turns on is 0.99, so a reading
    /// just under it has to survive the trip to the file intact. A shortened
    /// spelling here would turn a row that explains itself into a row that
    /// contradicts itself.
    /// </summary>
    [Fact]
    public void A_float_survives_being_written_and_read_back()
    {
        const float satisfaction = 0.98999995f;

        string written = new JsonObject().Number("satisfaction", satisfaction).ToString();
        string number = written
            .Replace("{\"satisfaction\":", string.Empty)
            .Replace("}", string.Empty);

        Assert.Equal(satisfaction, float.Parse(number, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// JSON has no spelling for these, and a ledger line no parser accepts is a
    /// row lost. A reading the game gave as nonsense is a reading we do not
    /// have, which is what null says.
    /// </summary>
    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void A_number_that_is_not_one_is_written_as_null(float value)
    {
        Assert.Equal("{\"x\":null}", new JsonObject().Number("x", value).ToString());
    }

    [Fact]
    public void A_missing_value_is_a_present_key_holding_null()
    {
        Assert.Equal(
            "{\"weather\":null,\"curfew\":null}",
            new JsonObject().Number("weather", (float?)null).Flag("curfew", (bool?)null).ToString());
    }

    /// <summary>
    /// Customer names are the game's, and the game's are not ours to assume
    /// anything about.
    /// </summary>
    [Fact]
    public void Text_is_escaped()
    {
        string written = new JsonObject().Text("name", "a\"b\\c\nd").ToString();

        Assert.Equal("{\"name\":\"a\\\"b\\\\c\\nd\\u0001\"}", written);
    }

    [Fact]
    public void Nesting_and_lists_come_out_as_json()
    {
        string written = new JsonObject()
            .Nest("shown", new JsonObject().Text("title", "Generosity Bonus"))
            .List("bonuses", new[] { new JsonObject().Number("amount", 10) })
            .Texts("unreadable", new[] { "weather" })
            .ToString();

        Assert.Equal(
            "{\"shown\":{\"title\":\"Generosity Bonus\"},\"bonuses\":[{\"amount\":10}],"
                + "\"unreadable\":[\"weather\"]}",
            written);
    }

    [Fact]
    public void An_absent_nest_or_list_is_null_rather_than_empty()
    {
        Assert.Equal(
            "{\"shown\":null,\"bonuses\":null}",
            new JsonObject().Nest("shown", null).List("bonuses", null).ToString());
    }
}
