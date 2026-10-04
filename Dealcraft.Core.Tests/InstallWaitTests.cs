using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

public class InstallWaitTests
{
    [Fact]
    public void Looks_again_while_nothing_is_there()
    {
        var wait = new InstallWait(30f);

        Assert.Equal(InstallVerdict.LookAgain, wait.Look(SceneLook.Absent, 0f));
        Assert.Equal(InstallVerdict.LookAgain, wait.Look(SceneLook.Absent, 10f));
    }

    [Fact]
    public void Gives_up_on_a_scene_that_never_shows_one()
    {
        var wait = new InstallWait(30f);
        wait.Look(SceneLook.Absent, 0f);

        Assert.Equal(InstallVerdict.NeverAppeared, wait.Look(SceneLook.Absent, 30.1f));
    }

    [Fact]
    public void The_bound_is_measured_from_the_first_look_not_from_zero()
    {
        var wait = new InstallWait(30f);
        wait.Look(SceneLook.Absent, 100f);

        Assert.Equal(InstallVerdict.LookAgain, wait.Look(SceneLook.Absent, 129f));
        Assert.Equal(InstallVerdict.NeverAppeared, wait.Look(SceneLook.Absent, 130.1f));
    }

    [Fact]
    public void The_deadline_itself_is_still_inside_the_wait()
    {
        var wait = new InstallWait(30f);
        wait.Look(SceneLook.Absent, 0f);

        Assert.Equal(InstallVerdict.LookAgain, wait.Look(SceneLook.Absent, 30f));
    }

    /// <summary>
    /// The mistake this type exists for: an empty pointer is a state of a scene
    /// that is still coming up, and reading failure out of it once is what made
    /// the app stop appearing.
    /// </summary>
    [Fact]
    public void Something_there_but_not_ready_is_waited_for_not_failed()
    {
        var wait = new InstallWait(30f);

        Assert.Equal(InstallVerdict.LookAgain, wait.Look(SceneLook.NotReady, 0f));
        Assert.Equal(InstallVerdict.LookAgain, wait.Look(SceneLook.NotReady, 5f));
        Assert.Equal(InstallVerdict.Done, wait.Look(SceneLook.Settled, 6f));
    }

    [Fact]
    public void The_clock_starts_again_when_the_scene_first_shows_something()
    {
        var wait = new InstallWait(30f);
        wait.Look(SceneLook.Absent, 0f);

        // Seen with a second to spare: the wait for the pointers inside it
        // starts here rather than inheriting one second of the first bound.
        wait.Look(SceneLook.NotReady, 29f);

        Assert.Equal(InstallVerdict.LookAgain, wait.Look(SceneLook.NotReady, 55f));
    }

    [Fact]
    public void The_clock_starts_again_only_once()
    {
        var wait = new InstallWait(30f);
        wait.Look(SceneLook.Absent, 0f);
        wait.Look(SceneLook.NotReady, 29f);
        wait.Look(SceneLook.NotReady, 50f);

        Assert.Equal(InstallVerdict.NeverReady, wait.Look(SceneLook.NotReady, 59.1f));
    }

    [Fact]
    public void Says_which_of_the_two_ran_out()
    {
        var appeared = new InstallWait(10f);
        appeared.Look(SceneLook.NotReady, 0f);

        var never = new InstallWait(10f);
        never.Look(SceneLook.Absent, 0f);

        Assert.Equal(InstallVerdict.NeverReady, appeared.Look(SceneLook.NotReady, 10.1f));
        Assert.Equal(InstallVerdict.NeverAppeared, never.Look(SceneLook.Absent, 10.1f));
    }

    [Fact]
    public void Something_that_showed_up_and_went_away_is_still_counted_as_seen()
    {
        var wait = new InstallWait(10f);
        wait.Look(SceneLook.NotReady, 0f);
        wait.Look(SceneLook.Absent, 5f);

        Assert.Equal(InstallVerdict.NeverReady, wait.Look(SceneLook.Absent, 10.1f));
    }

    [Fact]
    public void A_settled_look_ends_it()
    {
        var wait = new InstallWait(30f);

        Assert.Equal(InstallVerdict.Done, wait.Look(SceneLook.Settled, 0f));
    }

    /// <summary>
    /// Said once: a wait that has given up must not give up again on the next
    /// frame, or the log fills with the same sentence.
    /// </summary>
    [Fact]
    public void A_wait_that_gave_up_says_nothing_more()
    {
        var wait = new InstallWait(10f);
        wait.Look(SceneLook.Absent, 0f);
        Assert.Equal(InstallVerdict.NeverAppeared, wait.Look(SceneLook.Absent, 10.1f));

        Assert.Equal(InstallVerdict.Done, wait.Look(SceneLook.Absent, 10.2f));
        Assert.Equal(InstallVerdict.Done, wait.Look(SceneLook.NotReady, 11f));
    }

    [Fact]
    public void A_wait_that_finished_stays_finished()
    {
        var wait = new InstallWait(30f);
        wait.Look(SceneLook.Settled, 0f);

        Assert.Equal(InstallVerdict.Done, wait.Look(SceneLook.Absent, 1f));
    }

    [Fact]
    public void A_patience_that_is_not_a_figure_falls_back_to_the_default()
    {
        var zero = new InstallWait(0f);
        zero.Look(SceneLook.Absent, 0f);

        Assert.Equal(InstallVerdict.LookAgain, zero.Look(SceneLook.Absent, InstallWait.DefaultPatienceSeconds));
        Assert.Equal(
            InstallVerdict.NeverAppeared,
            zero.Look(SceneLook.Absent, InstallWait.DefaultPatienceSeconds + 0.1f));
    }

    /// <summary>
    /// The figure the phone installs actually wait, so that a change to it is a
    /// change somebody made on purpose.
    /// </summary>
    [Fact]
    public void The_installs_wait_half_a_minute_for_a_phone()
    {
        var wait = new InstallWait(InstallWait.DefaultPatienceSeconds);
        wait.Look(SceneLook.Absent, 0f);

        Assert.Equal(InstallVerdict.LookAgain, wait.Look(SceneLook.Absent, 29.9f));
        Assert.Equal(InstallVerdict.NeverAppeared, wait.Look(SceneLook.Absent, 30.1f));
    }
}
