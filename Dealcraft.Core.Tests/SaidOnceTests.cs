using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

public class SaidOnceTests
{
    [Fact]
    public void Says_something_new()
    {
        var said = new SaidOnce();

        Assert.True(said.ShouldSay("contract-1", "the game refused the handover"));
    }

    [Fact]
    public void Does_not_repeat_itself()
    {
        var said = new SaidOnce();
        said.ShouldSay("contract-1", "the game refused the handover");

        Assert.False(said.ShouldSay("contract-1", "the game refused the handover"));
    }

    [Fact]
    public void Says_it_again_when_the_words_change()
    {
        var said = new SaidOnce();
        said.ShouldSay("contract-1", "the game refused the handover");

        Assert.True(said.ShouldSay("contract-1", "a dealer took the contract"));
    }

    [Fact]
    public void Keeps_contracts_apart()
    {
        var said = new SaidOnce();
        said.ShouldSay("contract-1", "the game refused the handover");

        Assert.True(said.ShouldSay("contract-2", "the game refused the handover"));
    }

    [Fact]
    public void A_contract_that_ended_is_news_again_if_it_comes_back()
    {
        var said = new SaidOnce();
        said.ShouldSay("contract-1", "the game refused the handover");

        said.Forget(new[] { "contract-2" });

        Assert.True(said.ShouldSay("contract-1", "the game refused the handover"));
    }

    [Fact]
    public void Keeps_what_is_still_live()
    {
        var said = new SaidOnce();
        said.ShouldSay("contract-1", "the game refused the handover");

        said.Forget(new[] { "contract-1" });

        Assert.False(said.ShouldSay("contract-1", "the game refused the handover"));
    }

    [Fact]
    public void Says_nothing_without_a_key()
    {
        var said = new SaidOnce();

        Assert.False(said.ShouldSay(" ", "the game refused the handover"));
    }
}
