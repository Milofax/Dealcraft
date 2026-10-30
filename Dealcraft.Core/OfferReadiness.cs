using System;
using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// What to do about an offer whose deal could be taken this sweep.
/// </summary>
public enum OfferReadinessOutcome
{
    /// <summary>
    /// The customer has not finished. Nothing is spent. The zero value, so a
    /// caller that forgot to ask cannot take a deal by accident.
    /// </summary>
    Wait,

    /// <summary>Answer in the conversation, as a player's own reply would.</summary>
    Reply,

    /// <summary>
    /// There is no reply to give, so the deal is taken without one. The offer is
    /// served either way; only the line in the conversation is missing.
    /// </summary>
    AcceptDirectly,
}

/// <summary>A customer's conversation as plain values, read once.</summary>
public readonly struct ConversationReading
{
    public ConversationReading(
        bool exists,
        bool rollingOut,
        bool acceptOffered,
        bool countered = false,
        bool playerSpokeLast = false)
    {
        Exists = exists;
        RollingOut = rollingOut;
        AcceptOffered = acceptOffered;
        Countered = countered;
        PlayerSpokeLast = playerSpokeLast;
    }

    /// <summary>Whether this customer has a conversation at all.</summary>
    public bool Exists { get; }

    /// <summary>Whether the customer's messages are still arriving one by one.</summary>
    public bool RollingOut { get; }

    /// <summary>Whether the reply that takes the deal is on offer right now.</summary>
    public bool AcceptOffered { get; }

    /// <summary>Whether the customer has already agreed to a counter-offer.</summary>
    public bool Countered { get; }

    /// <summary>Whether the newest message in the conversation is the player's.</summary>
    public bool PlayerSpokeLast { get; }

    /// <summary>
    /// Whether a counter-offer has gone out and the customer has not answered. The
    /// player's message is in, the replies are gone and the offer is unchanged:
    /// nothing else in the game leaves a conversation that way.
    /// </summary>
    public bool CounterOnItsWay =>
        Exists && !RollingOut && !AcceptOffered && !Countered && PlayerSpokeLast;
}

/// <summary>The readiness answer, with the reason in the words the log uses.</summary>
public readonly struct OfferReadinessDecision
{
    private readonly string? reason;

    internal OfferReadinessDecision(OfferReadinessOutcome outcome, string reason)
    {
        Outcome = outcome;
        this.reason = reason;
    }

    public OfferReadinessOutcome Outcome { get; }

    /// <summary>Why. Always populated; it goes in the log.</summary>
    public string Reason => reason ?? "nothing was asked about this offer";
}

/// <summary>
/// Whether a customer has finished talking. Pure: no game types, no clock.
/// </summary>
/// <remarks>
/// <para>
/// The game raises the reply buttons with the first message of a chain and then
/// lets the rest arrive a second apart, so the buttons alone say nothing about
/// the last message. <see cref="ConversationReading.RollingOut"/> does, and it
/// covers a counter-offer's answer from before its first message.
/// </para>
/// <para>
/// A counter-offer also takes the buttons away until the customer has answered.
/// The accept reads the text of its button, so it throws in that gap.
/// </para>
/// <para>
/// The wait has a limit because a wait with no end would let the offer expire in
/// front of the player. No chain is known to hang. The one known offer with no
/// reply coming is a save written between a counter-offer and its answer: the
/// file holds the offer without replies, and the answer died with the session.
/// An ordinary offer loaded from a save gets its replies back and needs no wait.
/// </para>
/// </remarks>
public static class OfferReadiness
{
    /// <summary>
    /// How long a customer may keep the deal waiting, in game seconds. Far longer
    /// than any chain takes and a fraction of an offer's life.
    /// </summary>
    public const float PatienceSeconds = 30f;

    /// <param name="conversation">The customer's conversation, read this sweep.</param>
    /// <param name="waitedSeconds">How long this offer has been waited for so far.</param>
    public static OfferReadinessDecision Consider(in ConversationReading conversation, float waitedSeconds)
    {
        if (!conversation.Exists)
        {
            return new OfferReadinessDecision(
                OfferReadinessOutcome.AcceptDirectly,
                "there is no conversation to answer in, so the deal is taken without a reply");
        }

        if (conversation.RollingOut || !conversation.AcceptOffered)
        {
            if (waitedSeconds < PatienceSeconds)
            {
                return new OfferReadinessDecision(
                    OfferReadinessOutcome.Wait,
                    conversation.RollingOut
                        ? "still talking, so the deal waits for the last message"
                        : "the reply that takes this deal is not up yet, so the deal waits for it");
            }

            if (!conversation.AcceptOffered)
            {
                return new OfferReadinessDecision(
                    OfferReadinessOutcome.AcceptDirectly,
                    "the reply that takes this deal never came, so the deal is taken without one");
            }
        }

        return new OfferReadinessDecision(
            OfferReadinessOutcome.Reply, "has finished talking, so the deal can be answered");
    }
}

/// <summary>
/// The clock behind <see cref="OfferReadiness"/>: how long each offer has really
/// been waited for.
/// </summary>
/// <remarks>
/// It runs only while a customer owes an answer, so an offer parked for want of a
/// window has spent none of its patience. It starts again whenever what is being
/// waited for changes, because the contract's key survives a counter-offer: the
/// customer's answer gets the whole wait to itself, and an answer takes a tick or
/// two, so the direct path can only ever follow one that is not coming.
/// </remarks>
public sealed class OfferPatience
{
    private readonly Dictionary<string, (float Since, bool Gap, bool Countered)> waits =
        new(StringComparer.Ordinal);

    /// <param name="contractKey">The offer this reading belongs to.</param>
    /// <param name="conversation">The customer's conversation, read this sweep.</param>
    /// <param name="now">The time, in game seconds.</param>
    public OfferReadinessDecision Consider(
        string contractKey, in ConversationReading conversation, float now)
    {
        if (!conversation.Exists || (!conversation.RollingOut && conversation.AcceptOffered))
        {
            waits.Remove(contractKey);
            return OfferReadiness.Consider(conversation, 0f);
        }

        bool gap = conversation.CounterOnItsWay;
        bool countered = conversation.Countered;

        if (!waits.TryGetValue(contractKey, out (float Since, bool Gap, bool Countered) wait)
            || wait.Gap != gap
            || wait.Countered != countered)
        {
            wait = (now, gap, countered);
            waits[contractKey] = wait;
        }

        return OfferReadiness.Consider(conversation, now - wait.Since);
    }

    /// <summary>The offer has ended, so its wait has too.</summary>
    public void Forget(string contractKey) => waits.Remove(contractKey);

    /// <summary>Drop every wait but those of the offers still standing.</summary>
    public void Retain(IEnumerable<string> liveContractKeys)
    {
        var live = new HashSet<string>(liveContractKeys, StringComparer.Ordinal);
        var stale = new List<string>();

        foreach (string key in waits.Keys)
        {
            if (!live.Contains(key))
            {
                stale.Add(key);
            }
        }

        foreach (string key in stale)
        {
            waits.Remove(key);
        }
    }

    /// <summary>A new scene, so every wait is gone with it.</summary>
    public void Clear() => waits.Clear();
}
