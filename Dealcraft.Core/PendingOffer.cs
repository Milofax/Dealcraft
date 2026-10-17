namespace Dealcraft.Core;

/// <summary>
/// One customer's standing offer, as plain values. The adapter reads these off
/// <c>Customer</c>; nothing past this point knows an Il2Cpp type exists.
/// </summary>
public readonly struct PendingOffer
{
    public PendingOffer(
        string contractKey,
        string customerName,
        bool hasOfferedContract,
        bool alreadyOnADeal,
        bool alreadyCountered = false)
    {
        ContractKey = contractKey;
        CustomerName = customerName;
        HasOfferedContract = hasOfferedContract;
        AlreadyOnADeal = alreadyOnADeal;
        AlreadyCountered = alreadyCountered;
    }

    /// <summary>
    /// What the claim registry keys this contract on. It must be the same
    /// string on every machine in the session and must last as long as the
    /// contract does, or "exactly once per contract" stops meaning anything.
    /// </summary>
    public string ContractKey { get; }

    /// <summary>The customer's full name, as the game spells it.</summary>
    public string CustomerName { get; }

    /// <summary>Whether there is an offer on the table at all.</summary>
    public bool HasOfferedContract { get; }

    /// <summary>
    /// Whether this customer is already running a contract. Accepting a second
    /// one is not a thing the player's own UI can do, so neither does this.
    /// </summary>
    public bool AlreadyOnADeal { get; }

    /// <summary>
    /// Whether this offer is itself the result of a counter-offer — the game's
    /// own <c>ContractInfo.IsCounterOffer</c>.
    /// </summary>
    /// <remarks>
    /// The flag the server sets on every observer through the vanilla
    /// <c>SetContractIsCounterOffer</c> RPC, so it is the same answer on every
    /// machine and it survives a save and a load, which the claim registry
    /// deliberately does not. It is what stops a reloaded session from
    /// countering an offer it already countered, and it does not care whether
    /// the automation or a player did the countering.
    /// </remarks>
    public bool AlreadyCountered { get; }
}

/// <summary>
/// Whether this machine may act on a contract at all.
/// </summary>
/// <remarks>
/// Two conditions, not one. <c>IsServer</c> is the ownership rule: six players
/// may each have the mod installed and each see the same replicated customer
/// state, and without the gate six machines act on one contract. The client
/// half matters too, because <c>Customer.SendContractAccepted</c> is written by
/// the client and refuses to send without one — verified in the shipped machine
/// code, which tests <c>IsClientInitialized</c> before touching the writer. On a
/// listen-server host both hold; the distinction only shows up while a session
/// is still coming up.
/// </remarks>
public enum ServerAuthority
{
    /// <summary>
    /// This machine is not the server. Do nothing at all. The zero value, so a
    /// caller that forgot to ask cannot act by accident.
    /// </summary>
    NotTheServer,

    /// <summary>Server and client are both up. The automation may act.</summary>
    Held,

    /// <summary>
    /// The server is up but its client half is not, so the accept RPC would be
    /// dropped with a warning. Wait rather than call it.
    /// </summary>
    WithoutAClient,

    /// <summary>
    /// This machine is a guest with a live client half.
    /// </summary>
    /// <remarks>
    /// Distinguished from <see cref="NotTheServer"/> because the two are
    /// different answers to different questions, and the project used to have
    /// only one. Anything acting on <em>shared</em> state — one customer roster,
    /// one contract list, one conversation — must not run here: six installs
    /// would answer one offer six times. A handover is not shared state. It is
    /// one player's goods leaving one player's pockets, and
    /// <c>Customer.ProcessHandover</c> is a client path — it reads
    /// <c>IsClientInitialized</c> and calls <c>SendServerRpc</c>, which is
    /// exactly what a guest's own Done button does.
    /// </remarks>
    Guest,
}
