using System.Globalization;

namespace Dealcraft.Core;

/// <summary>
/// What one probe handed the game: the offer spelled out as the product
/// instance it would be handed over as.
/// </summary>
/// <remarks>
/// <para>
/// <c>Customer.GetOfferSuccessChance(List&lt;ItemInstance&gt;, float)</c> scores
/// the goods, not a product id, and its own read set includes
/// <c>ItemInstance.get_Definition</c> and
/// <c>ProductItemInstance.get_AppliedPackaging</c>
/// (<c>docs/counteroffer-truth.md</c>). So what the instance carries is half of
/// what the customer's answer is made of, and none of it is visible from
/// outside. This is that half, written down as plain values so the record can
/// carry it and a test can drive it.
/// </para>
/// <para>
/// The three shapes below are the three answers to "what did the game actually
/// get", and they are kept apart on purpose: an empty item list, an instance
/// that is not a product, and a product instance with its quality and packaging
/// readable are three different faults with three different repairs.
/// </para>
/// </remarks>
public readonly struct ProbeOffering
{
    private ProbeOffering(
        string? productName,
        int quantity,
        bool instanceBuilt,
        bool readable,
        int amount,
        int quality,
        string? packaging)
    {
        ProductName = productName;
        Quantity = quantity;
        InstanceBuilt = instanceBuilt;
        Readable = readable;
        Amount = amount;
        Quality = quality;
        Packaging = packaging;
    }

    /// <summary>The definition's own name, as the game spells it.</summary>
    public string? ProductName { get; }

    /// <summary>What <c>GetDefaultInstance</c> was asked for.</summary>
    public int Quantity { get; }

    /// <summary>Whether it answered with an instance at all.</summary>
    public bool InstanceBuilt { get; }

    /// <summary>Whether that instance is a product instance, and so readable.</summary>
    public bool Readable { get; }

    /// <summary>Units in one package, off the instance.</summary>
    public int Amount { get; }

    /// <summary>The instance's grade, on the game's quality ladder.</summary>
    public int Quality { get; }

    /// <summary>The applied packaging, or null where none is applied.</summary>
    public string? Packaging { get; }

    /// <summary><c>GetDefaultInstance</c> answered null, so nothing was handed in.</summary>
    public static ProbeOffering Nothing(string? productName, int quantity) =>
        new(productName, quantity, instanceBuilt: false, readable: false, 0, 0, null);

    /// <summary>
    /// An instance that is not a <c>ProductItemInstance</c>, so neither quality
    /// nor packaging can be read off it — which would itself be the answer.
    /// </summary>
    public static ProbeOffering Unreadable(string? productName, int quantity) =>
        new(productName, quantity, instanceBuilt: true, readable: false, 0, 0, null);

    /// <summary>
    /// A product instance, with everything the customer's own scoring reads.
    /// </summary>
    /// <param name="packaging">
    /// The applied packaging, or null where the instance has none. Null is the
    /// case the ticket is about, so it is said out loud rather than left blank.
    /// </param>
    public static ProbeOffering Of(
        string? productName,
        int quantity,
        int amount,
        int quality,
        string? packaging) =>
        new(productName, quantity, instanceBuilt: true, readable: true, amount, quality, packaging);

    /// <summary>One clause: what the game was handed.</summary>
    public string Describe()
    {
        string name = string.IsNullOrWhiteSpace(ProductName) ? "an unnamed product" : ProductName.Trim();
        string quantity = Quantity.ToString(CultureInfo.InvariantCulture);

        if (!InstanceBuilt)
        {
            return $"handed in nothing: GetDefaultInstance({quantity}) returned null for {name}, "
                + "so the item list was empty";
        }

        if (!Readable)
        {
            return $"handed in {quantity} × {name}, which is not a product instance, so neither its "
                + "quality nor its packaging could be read";
        }

        return $"handed in {quantity} × {name} at {Amount.ToString(CultureInfo.InvariantCulture)} per "
            + $"package, quality {QualityTier.Name(Quality)} ({Quality.ToString(CultureInfo.InvariantCulture)}), "
            + (string.IsNullOrWhiteSpace(Packaging) ? "no packaging applied" : $"packaged as {Packaging.Trim()}");
    }
}

/// <summary>
/// What one negotiation's probing came to: what was handed in, what came back,
/// and what went wrong — one sentence, written once per attempt.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> The probe's answer used to leave the game as a single
/// float, and a failure left it as the same float: <c>GetOfferSuccessChance</c>
/// throwing for an interop reason returned zero, at every price, with nothing
/// written anywhere. Four negotiations in the owner's session found a best
/// chance of zero for four customers who had offered the contract themselves,
/// and from the file there was no way to tell a customer who says no from a call
/// that never arrived. This is the instrument that tells them apart.
/// </para>
/// <para>
/// <b>Once per attempt, never once per call.</b> A curve is drawn at every
/// confidence and each one bisects, so a single negotiation asks a few dozen
/// times; a line per call would bury the answer in the file it is written to.
/// So this accumulates while the search runs and is spelled out once, at the end,
/// by whoever records the decision. The first and last answers are kept because
/// two points and the best of them tell a flat zero from a curve that rises and
/// never clears; the first failure is kept because the hundredth is the same one.
/// </para>
/// <para>
/// <b>It measures and does not judge.</b> Nothing here decides anything, and
/// nothing here is fed back into the search — a probe that failed still answers
/// zero to the curve, exactly as before. Reading the file is what settles the
/// cause; this only makes the file worth reading.
/// </para>
/// </remarks>
public sealed class ProbeDiagnosis
{
    /// <summary>
    /// How much of an exception message is kept. Long enough for a stack-free
    /// interop message to be recognisable, short enough that one row stays one
    /// readable line.
    /// </summary>
    public const int MostOfAMessage = 200;

    private ProbeOffering _offering;
    private bool _offered;

    private int _answers;
    private int _failures;

    private int _firstQuantity;
    private float _firstPrice;
    private float _firstChance;

    private int _lastQuantity;
    private float _lastPrice;
    private float _lastChance;

    private float _best;

    private string? _failure;

    /// <summary>How many probes answered.</summary>
    public int Answers => _answers;

    /// <summary>How many probes did not, for any reason.</summary>
    public int Failures => _failures;

    /// <summary>
    /// What was handed to the game. The first one is kept: the definition, the
    /// quality and the packaging do not vary across a search, and the quantity
    /// that does is carried by each answer below.
    /// </summary>
    public void Offered(in ProbeOffering offering)
    {
        if (_offered)
        {
            return;
        }

        _offering = offering;
        _offered = true;
    }

    /// <summary>One probe that came back.</summary>
    public void Answered(int quantity, float totalPrice, float chance)
    {
        if (_answers == 0)
        {
            _firstQuantity = quantity;
            _firstPrice = totalPrice;
            _firstChance = chance;
            _best = chance;
        }
        else if (chance > _best)
        {
            _best = chance;
        }

        _lastQuantity = quantity;
        _lastPrice = totalPrice;
        _lastChance = chance;
        _answers++;
    }

    /// <summary>
    /// One probe that did not come back. The first that says anything about
    /// itself is kept whole and the rest are counted: a bisection asks a dozen
    /// times and they fail alike.
    /// </summary>
    /// <param name="type">
    /// The exception's type name, or null where the probe never reached the
    /// game — a customer or a product that had gone is not a thrown exception
    /// and must not read as one.
    /// </param>
    public void Failed(string? type, string? message)
    {
        _failures++;
        _failure ??= Spell(type, message);
    }

    /// <summary>
    /// The whole of it, as one line for the record, or null where no probe was
    /// ever made — which is a search that stopped before it asked anything, and
    /// the row says so with a null rather than with a sentence about nothing.
    /// </summary>
    public string? Summary()
    {
        if (_answers == 0 && _failures == 0)
        {
            return null;
        }

        return string.Join("; ", HandedIn(), CameBack(), WentWrong());
    }

    private string HandedIn() => _offered ? _offering.Describe() : "handed in nothing that was recorded";

    private string CameBack()
    {
        if (_answers == 0)
        {
            return "no price point answered";
        }

        if (_answers == 1)
        {
            return $"1 price point, {Deal(_firstQuantity, _firstPrice)} answered {Chance(_firstChance)}";
        }

        return $"{_answers.ToString(CultureInfo.InvariantCulture)} price points, first "
            + $"{Deal(_firstQuantity, _firstPrice)} answered {Chance(_firstChance)}, last "
            + $"{Deal(_lastQuantity, _lastPrice)} answered {Chance(_lastChance)}, best anywhere "
            + Chance(_best);
    }

    private string WentWrong()
    {
        if (_failures == 0)
        {
            return "nothing threw";
        }

        int tried = _answers + _failures;

        // A failure that named neither a type nor a message is still a fact
        // about the session, and the count is the fact. It reads as one rather
        // than as a sentence that trails off after "first".
        string first = _failure is null ? "with nothing said about why" : "first " + _failure;

        return $"{_failures.ToString(CultureInfo.InvariantCulture)} of "
            + $"{tried.ToString(CultureInfo.InvariantCulture)} probes failed, {first}";
    }

    /// <summary>
    /// A failure as one phrase, or null where it said nothing about itself at
    /// all — which is not a phrase worth keeping in place of a later one that
    /// does say something.
    /// </summary>
    private static string? Spell(string? type, string? message)
    {
        string? named = string.IsNullOrWhiteSpace(type) ? null : type.Trim();
        string? said = string.IsNullOrWhiteSpace(message) ? null : Shortened(message.Trim());

        if (named is null)
        {
            return said;
        }

        return said is null ? named : named + ": " + said;
    }

    private static string Shortened(string message) =>
        message.Length <= MostOfAMessage ? message : message.Substring(0, MostOfAMessage) + "…";

    private static string Deal(int quantity, float totalPrice) =>
        $"{quantity.ToString(CultureInfo.InvariantCulture)} for $"
            + totalPrice.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// The chance as the game gave it, not as a percentage: rounding 0.004 to
    /// 0% is exactly the difference this file exists to record, and the figure
    /// is never clamped for the same reason the row's own chance is not.
    /// </summary>
    private static string Chance(float chance) =>
        chance.ToString("0.#####", CultureInfo.InvariantCulture);
}
