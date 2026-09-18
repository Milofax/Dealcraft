using System;
using System.Collections;
using System.Collections.Generic;
using Dealcraft.Core;
using Il2CppFishNet.Object;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.UI.Phone;
using Il2CppScheduleOne.UI.Phone.ProductManagerApp;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dealcraft.Phone;

/// <summary>
/// Dealcraft's entry on the phone's home screen: a clone of the Product Manager
/// app, re-skinned. The vanilla <c>App&lt;T&gt;</c> component comes with the
/// clone, so the icon, the opening and closing, the orientation and the exit flow
/// are the game's own rather than a reimplementation of them.
/// </summary>
internal sealed class PhoneApp
{
    private const string AppName = "Dealcraft";
    private const string CloneName = "DealcraftApp";

    /// <summary>
    /// A dark icon would vanish into the phone's plate, so a sampled colour is
    /// only used when it is bright enough to read.
    /// </summary>
    private const float MinimumIconLuminance = 0.35f;

    private readonly Func<IReadOnlyList<AutomationSetting>> _automation;

    /// <summary>
    /// Whose machine this is, from the reader every pass already asks. Held as a
    /// question rather than as an answer: it is asked again with every reading of
    /// the settings, because a page built while the session was still coming up
    /// would otherwise keep the answer it got then.
    /// </summary>
    private readonly Func<ServerAuthority> _authority;

    private readonly Func<SettingChange, bool> _write;
    private readonly Action<string> _log;
    private readonly Action<string> _warn;

    /// <summary>
    /// Which scene the pending install belongs to. A later scene supersedes an
    /// install that is still waiting for a phone that will never arrive.
    /// </summary>
    private int _generation;

    /// <summary>
    /// Which opening of the app the pending fit belongs to, so a wait left over
    /// from a screen the player has closed does not go on measuring it.
    /// </summary>
    private int _episode;

    /// <summary>
    /// What the game's own Products app last read as, and the app itself. Held
    /// so the check on the game's list is taken again every time Dealcraft is
    /// opened rather than once while the copy is starting: what this guards
    /// against leaves a row behind whenever it happens, so nothing about when it
    /// happens has to be guessed.
    /// </summary>
    private ProductManagerApp _vanilla;

    private AppCounts _vanillaCounts = AppCounts.Unknown;

    /// <summary>
    /// The check runs on every open, and most opens have nothing new to say
    /// about it.
    /// </summary>
    private readonly SaidOnce _saidAboutVanilla = new();

    // The view is held deliberately: it owns the managed click handlers that
    // Il2Cpp only knows by pointer.
    private AppPageView _view;
    private GameObject _clone;
    private ProductManagerApp _app;
    private bool _wasOpen;

    /// <summary>
    /// The icon of the app this copy was made from. Kept so that an icon the game
    /// generated before the copy was named can be recognised by the sprite it is
    /// still showing.
    /// </summary>
    private Sprite _donorIcon;

    /// <summary>Dealcraft's own mark, rasterised at install.</summary>
    private Sprite _ourIcon;

    /// <param name="automation">
    /// The Automation rows as the preferences file now reads. Asked for again
    /// after every change, rather than the new value being remembered, so what
    /// the app shows is always a reading of the file.
    /// </param>
    /// <param name="write">
    /// Where a changed setting goes: to whoever owns the entry, and to disk.
    /// </param>
    /// <param name="authority">
    /// Whether this machine is the host. The three blocks that act on state the
    /// session shares are the host's to set, so a guest's page is the handover
    /// and a note; see <see cref="AutomationForm"/>.
    /// </param>
    public PhoneApp(
        Func<IReadOnlyList<AutomationSetting>> automation,
        Func<ServerAuthority> authority,
        Func<SettingChange, bool> write,
        Action<string> log,
        Action<string> warn)
    {
        _automation = automation;
        _authority = authority;
        _write = write;
        _log = log;
        _warn = warn;
    }

    /// <summary>
    /// Start installing into the scene that has just come up. Safe to call for
    /// every scene: one without a phone simply times out and says so.
    /// </summary>
    public void InstallIntoCurrentScene()
    {
        _generation++;
        _episode++;
        _view = null;
        _clone = null;
        _app = null;
        _wasOpen = false;
        _donorIcon = null;
        _ourIcon = null;

        // A new scene is a new pair of apps, so what was said about the last
        // one's lists is not an answer about this one's.
        _vanilla = null;
        _vanillaCounts = AppCounts.Unknown;
        _saidAboutVanilla.Forget(Array.Empty<string>());

        // A copy left waiting for a Start that will never come, because its scene
        // went. Forgetting it here is what stops the wrap watching for a pointer
        // that belongs to a dead object.
        ClonedAppStart.Forget();

        int generation = _generation;
        MelonCoroutines.Start(ScenePoll.Until(
            () => Look(generation),
            () => generation == _generation,
            GaveUp));
    }

    /// <summary>
    /// Watch for the app being opened. The clone still runs the vanilla Product
    /// Manager's code, which puts its own detail panel back up when the app comes
    /// on screen, so Dealcraft's content is re-applied on that edge. One property
    /// read per frame, and nothing at all before the app is installed.
    /// </summary>
    public void Tick()
    {
        try
        {
            if (_view == null || _app == null)
            {
                return;
            }

            bool open = _app.isOpen;
            if (open == _wasOpen)
            {
                return;
            }

            _wasOpen = open;
            if (open)
            {
                _view.Reassert();

                // After the re-assertion, not before it. That is the one moment
                // Dealcraft empties a product list, so this reads the game's own
                // app immediately afterwards and would say so if the list that
                // was emptied had been the game's.
                CheckVanillaApp(_vanillaCounts, string.Empty);

                // The app has a size now, or is about to have one: it is on
                // screen and the engine lays it out. The page is placed against
                // that rather than against the nothing it held at install.
                SettleScreen();
            }
        }
        catch (Exception error)
        {
            // Feature.Tick's own contract: never throw into the game's update
            // loop. Reading isOpen touches a component the scene may have taken
            // away, and reasserting walks live game objects, so this is a real
            // path rather than a formality. The edge has been taken either way,
            // so a failure costs this one re-application and not a loop that
            // retries it every frame.
            _warn($"could not put the app's content back on screen: {error.Message}");
        }
    }

    /// <summary>
    /// One look at the scene: whether the app can be copied now, not yet, or not
    /// at all. The copy is made here, in the frame the answer comes out right,
    /// and the rest of the install is handed to <see cref="Finish"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The pointer being <em>empty</em> is the answer that matters. The vanilla
    /// Product Manager claims the app type's singleton in its own
    /// <c>PlayerSingleton.Awake</c>, and a scene is initialised before that has
    /// run — so at scene-init the pointer reads null on a phone that is about to
    /// work perfectly well. Reading failure out of that is what made the app stop
    /// appearing at all: "the pointer is currently null" is a state of the scene,
    /// "the pointer cannot be read or written" is the hazard, and only the second
    /// is a reason to give up.
    /// </para>
    /// <para>
    /// Waiting for the pointer also settles the clone's own <c>Awake</c>: the
    /// pointer is only claimed by an app whose object is active in the hierarchy,
    /// so by the time this returns, the parent the copy is moved under is active
    /// too and the copy wakes where <see cref="TryClone"/> expects it to.
    /// </para>
    /// </remarks>
    private SceneLook Look(int generation)
    {
        if (Object.FindObjectOfType<ProductManagerApp>(true) == null
            || Object.FindObjectOfType<HomeScreen>(true) == null)
        {
            return SceneLook.Absent;
        }

        ProductManagerApp donor;
        try
        {
            // Read here, in the frame the copy is made in, because this is the
            // value that has to go back into the pointer afterwards.
            donor = PlayerSingleton<ProductManagerApp>.instance;
        }
        catch (Exception error)
        {
            _warn($"the app singleton cannot be read ({error.Message}); not cloning the app, "
                + "because the vanilla Product Manager could not be restored afterwards");
            return SceneLook.Settled;
        }

        if (donor == null)
        {
            return SceneLook.NotReady;
        }

        if (!SafeToClone(donor))
        {
            return SceneLook.Settled;
        }

        var before = new DonorApp(donor, donor.AppName, donor.IconLabel, Count(donor));

        if (!TryClone(donor))
        {
            return SceneLook.Settled;
        }

        MelonCoroutines.Start(Finish(generation, before));
        return SceneLook.Settled;
    }

    /// <summary>
    /// What the phone says when the wait runs out. Two different things, because
    /// a scene with no phone in it is ordinary and a phone whose app never claimed
    /// its pointer is not. Said once either way.
    /// </summary>
    private void GaveUp(InstallVerdict verdict)
    {
        if (verdict == InstallVerdict.NeverReady)
        {
            _warn("the phone is in this scene but the Product Manager never claimed the app "
                + "singleton, so the app was not cloned: the vanilla Product Manager could not "
                + "have been put back afterwards");
            return;
        }

        _log("no phone in this scene, so no app to add");
    }

    /// <summary>
    /// The rest of the install, once the copy exists: let it start, check what
    /// starting did, and then make it Dealcraft's.
    /// </summary>
    /// <param name="before">
    /// The vanilla app as it read in the frame the copy was made. Everything here
    /// is a comparison against that reading, so it is taken once and passed on
    /// rather than read again off an app the copy may have changed.
    /// </param>
    private IEnumerator Finish(int generation, DonorApp before)
    {
        // Two frames, so the copy's own Start and the layout coroutines it starts
        // have settled. The name and the icon were in place before any of that ran;
        // see TryClone.
        yield return null;
        yield return null;

        if (generation != _generation)
        {
            yield break;
        }

        // Said twice on purpose. TryClone restores the pointer on the line after
        // the move that runs Awake, which is exact if Unity activates the object
        // there and harmless if it defers it to the frame boundary — this is the
        // line that covers the second case. Writing the pointer the vanilla app
        // should have is idempotent.
        RestoreAppSingleton(before.App);

        if (_clone == null)
        {
            _warn("the cloned app was destroyed during its first frame: this build's "
                + "PlayerSingleton refuses duplicates, and the app cannot be cloned this way");
            yield break;
        }

        // Whether the copy's start-up left the game's own list alone. Checked
        // rather than assumed, because this is the one thing about the clone that
        // can reach outside it — and because a reading of the machine code has
        // already been argued about twice.
        _vanilla = before.App;
        CheckVanillaApp(before.Counts, ClonedAppStart.Window(ClonedAppStart.ThisFrame()));

        int cloneRows = RowCount(_app);
        if (cloneRows > 0)
        {
            _log($"the copy built {cloneRows} vanilla product rows for itself while starting; "
                + "they are cleared so the app shows only what Dealcraft puts there");
        }

        // The copy has had its first frames, so the wrap has nothing left to
        // guard and goes back to being a no-op on every instance.
        ClonedAppStart.Forget();

        RetargetHomeScreenIcon();
        Reskin(before.Name, before.IconLabel);
    }

    /// <summary>
    /// Whether the game's own Products app still holds exactly what the game
    /// holds, and nothing of Dealcraft's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this is the whole guard and the wrap is not.</b> A guard around a
    /// window has to know when the writing happens, and the one before this
    /// asked only whether the vanilla app's rows had grown. They grow on their
    /// own: the vanilla app's own <c>Start</c> subscribes <c>CreateEntry</c> to
    /// <c>ProductManager.onProductDiscovered</c>, and the save's loader fires
    /// that event once per product as it loads, so a copy made during a load
    /// watched the game's list arrive and reported it as duplication. Ticket 50,
    /// and <see cref="ProductListCheck"/> carries the addresses.
    /// </para>
    /// <para>
    /// The count is an end state rather than a window, so nothing about the
    /// copy's start-up has to be guessed: a write leaves a row behind whether it
    /// happened in <c>Start</c>, in a coroutine <c>Start</c> queued, in an
    /// <c>OnEnable</c>, or an hour later. That is why this is taken again every
    /// time the app is opened rather than once while the copy is starting.
    /// </para>
    /// </remarks>
    /// <param name="before">
    /// What both apps held at the earlier moment this is measured against.
    /// </param>
    /// <param name="window">
    /// What the wrap around the copy's <c>Start</c> covered, for a fault to be
    /// read against. Empty where there is no such window to name.
    /// </param>
    private void CheckVanillaApp(AppCounts before, string window)
    {
        AppCounts now = Count(_vanilla);

        Report(
            "product rows",
            "products",
            new ProductListReading(before.Rows, before.Products, now.Rows, now.Products),
            window);

        Report(
            "favourite rows",
            "favourited products",
            new ProductListReading(
                before.Favourites, before.FavouriteProducts, now.Favourites, now.FavouriteProducts),
            window);

        _vanillaCounts = now;
    }

    /// <summary>
    /// Say what one of the two lists did, and do not say it again until it does
    /// something else.
    /// </summary>
    /// <remarks>
    /// A fault's counts are part of what makes it news, so a list that goes on
    /// growing past what the game holds says so again; an unchanged one is said
    /// once. Without that this would write two lines every time the player opens
    /// the app.
    /// </remarks>
    private void Report(string rows, string things, ProductListReading reading, string window)
    {
        ProductListState state = ProductListCheck.Look(reading);
        bool fault = ProductListCheck.IsFault(state);

        string subject = fault
            ? $"{state} {reading.RowsAfter}/{reading.ThingsAfter}"
            : state.ToString();

        if (!_saidAboutVanilla.ShouldSay(rows, subject))
        {
            return;
        }

        string said = ProductListCheck.Sentence(state, reading, rows, things, window);
        if (fault)
        {
            _warn(said);
            return;
        }

        _log(said);
    }

    /// <summary>
    /// What an app holds and what the game holds, read together so the two
    /// belong to the same moment.
    /// </summary>
    private static AppCounts Count(ProductManagerApp app) =>
        new(
            RowCount(app),
            FavouriteCount(app),
            ProductCatalogue.Discovered(),
            ProductCatalogue.Favourited());

    /// <summary>
    /// Put the app type's singleton pointer back at the vanilla app. The copy's
    /// <c>PlayerSingleton.Awake</c> claims it, and leaving it claimed would send
    /// every <c>PlayerSingleton&lt;ProductManagerApp&gt;.Instance</c> read in the
    /// game to Dealcraft's copy.
    /// </summary>
    private void RestoreAppSingleton(ProductManagerApp donor)
    {
        try
        {
            PlayerSingleton<ProductManagerApp>.instance = donor;
        }
        catch (Exception error)
        {
            _warn($"could not put the vanilla Product Manager back in its singleton: {error.Message}");
        }
    }

    /// <summary>
    /// Whether this app can be copied without touching anything networked or
    /// leaving the vanilla app broken. Two conditions:
    /// <list type="bullet">
    /// <item>the app type's singleton pointer can be read <em>and</em> written —
    /// without that, the copy's <c>PlayerSingleton.Awake</c> would leave the
    /// vanilla Product Manager pointing at our clone. <see cref="Look"/> has
    /// already read it, and <paramref name="donor"/> is what it holds; writing
    /// that same value back is a no-op and the only way to find out whether the
    /// pointer can be written before the copy's <c>Awake</c> makes it matter. A
    /// pointer that is merely <em>empty</em> never reaches this method: that is a
    /// scene still coming up, and it is waited for rather than refused.</item>
    /// <item>there is no <c>NetworkBehaviour</c> anywhere in the subtree. The
    /// mod may not introduce a network object of any kind, and a copy of one
    /// would also be a component FishNet never indexed.</item>
    /// </list>
    /// </summary>
    private bool SafeToClone(ProductManagerApp donor)
    {
        try
        {
            PlayerSingleton<ProductManagerApp>.instance = donor;
        }
        catch (Exception error)
        {
            _warn($"the app singleton cannot be written ({error.Message}); not cloning the app, "
                + "because the vanilla Product Manager could not be restored afterwards");
            return false;
        }

        Il2CppArrayBase<NetworkBehaviour> networked =
            donor.gameObject.GetComponentsInChildren<NetworkBehaviour>(true);
        if (networked.Length > 0)
        {
            _warn($"the phone app carries {networked.Length} networked components in this build; "
                + "not cloning it, because the mod must not add a network object of any kind");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Make the copy, name it, and only then let it start.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order matters more than anything else about the clone. The home screen
    /// icon is built by <c>HomeScreen.GenerateAppIcon(App)</c>, which reads
    /// <c>IconLabel</c> and <c>AppIcon</c> off the app and copies them into the
    /// new icon's label and <c>Image.sprite</c> — a snapshot, taken once. Its only
    /// caller is <c>App&lt;T&gt;.GenerateHomeScreenIcon</c>, called from
    /// <c>App&lt;T&gt;.OnStartClient</c>, which <c>PlayerSingleton&lt;T&gt;.Awake</c>
    /// tail-calls. All of that is read out of the shipped machine code; see
    /// <c>docs/native-truth.md</c>.
    /// </para>
    /// <para>
    /// So <c>Awake</c> — and the icon with it — runs <em>inside</em>
    /// <c>Instantiate</c> when the copy lands under an active parent, which is why
    /// the first build put a second "Products" icon on the phone: the fields were
    /// set on the line after, one instruction too late. The copy is therefore made
    /// under a parent that is switched off, named there, and only then moved into
    /// place. Becoming active in the hierarchy is what runs <c>Awake</c>, and by
    /// then the app already says it is Dealcraft.
    /// </para>
    /// </remarks>
    private bool TryClone(ProductManagerApp donor)
    {
        GameObject staging = null;

        try
        {
            _donorIcon = donor.AppIcon;
            Transform parent = donor.transform.parent;

            staging = new GameObject(CloneName + "_Staging");
            staging.SetActive(false);
            staging.transform.SetParent(parent, false);

            Object created = Object.Instantiate(donor.gameObject, staging.transform, false);
            GameObject clone = created.Cast<GameObject>();
            clone.name = CloneName;

            ProductManagerApp app = clone.GetComponent<ProductManagerApp>();
            if (app == null)
            {
                _warn("the cloned app has no app component; abandoning the copy");
                Object.Destroy(clone);
                return false;
            }

            _ourIcon = AppIconArt.CreateMonogram(IconTint(_donorIcon));
            app.AppName = AppName;
            app.IconLabel = AppName;
            app.AppIcon = _ourIcon;

            _clone = clone;
            _app = app;

            // The copy's own Start runs a frame later, when the pointer is the
            // vanilla app's again, and in the owner's first live run something in
            // that start-up wrote a second product list into the vanilla app
            // through it. From here on the pointer is aimed at the copy for
            // exactly the length of that one call; see ClonedAppStart.
            ClonedAppStart.Expect(app, donor);

            // Awake runs inside this move, and it repoints the app type's
            // singleton at the copy. No game code runs between these two lines,
            // so the vanilla pointer is back before anything can read it.
            clone.transform.SetParent(parent, false);
            RestoreAppSingleton(donor);

            _log($"cloned the Product Manager app as '{CloneName}', named before its first frame");
            return true;
        }
        catch (Exception error)
        {
            _warn($"could not clone the app: {error}");
            return false;
        }
        finally
        {
            if (staging != null)
            {
                Object.Destroy(staging);
            }
        }
    }

    /// <summary>
    /// Make sure the icon on the home screen is Dealcraft's, whether or not the
    /// copy's <c>Awake</c> ran late enough to build it that way.
    /// </summary>
    /// <remarks>
    /// The icon is a snapshot, so an icon built before the copy was named keeps
    /// the donor's label and sprite for good. Nothing here guesses at the icon's
    /// hierarchy: the button is the app's own <c>appIconButton</c>, the sprite to
    /// replace is recognised by being the donor's very sprite, and the label is
    /// every <see cref="Text"/> under that button other than the notification
    /// count, which the app hands over by name. The count is the only other label
    /// an icon has.
    /// </remarks>
    private void RetargetHomeScreenIcon()
    {
        Button icon;
        try
        {
            icon = _app.appIconButton;
        }
        catch (Exception error)
        {
            _warn($"could not read the app's home screen icon: {error.Message}");
            return;
        }

        if (icon == null)
        {
            // The vanilla app generates its icon when it starts, so a copy that
            // has not started yet has none. It will get one, and by then it has
            // been named — which is the whole point of the staged clone.
            _log("the app has no home screen icon yet; it will be generated from "
                + "Dealcraft's own name when the app starts");
            return;
        }

        int retargetedSprites = 0;
        foreach (Image image in icon.gameObject.GetComponentsInChildren<Image>(true))
        {
            if (_donorIcon != null && image.sprite != null && image.sprite.Pointer == _donorIcon.Pointer)
            {
                image.sprite = _ourIcon;
                retargetedSprites++;
            }

            if (_ourIcon == null || image.sprite == null || image.sprite.Pointer != _ourIcon.Pointer)
            {
                continue;
            }

            // The monogram is one square mark with no border, so it is drawn whole
            // and in proportion. A nine-slice or a tiled fill is for a sprite that
            // was authored to be stretched, and this one was not.
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
        }

        int relabelled = 0;
        int wrongLabels = 0;
        foreach (Text label in Widgets.TextsOutside(
            icon.gameObject,
            _app.notificationText,
            _app.notificationContainer))
        {
            string current = label.text != null ? label.text.Trim() : string.Empty;
            if (current != AppName)
            {
                wrongLabels++;
            }

            label.text = current == current.ToUpperInvariant() && current.Length > 0
                ? AppName.ToUpperInvariant()
                : AppName;
            relabelled++;
        }

        if (relabelled == 0)
        {
            _warn("the home screen icon carries no label of its own in this build, so the "
                + "app's name on the phone's grid cannot be checked from here");
            return;
        }

        if (retargetedSprites > 0 || wrongLabels > 0)
        {
            _warn("the home screen icon had been generated before the copy was named, so it was "
                + $"retargeted after the fact: {retargetedSprites} sprite(s) and {wrongLabels} "
                + "label(s) still belonged to the app it was copied from");
            return;
        }

        _log($"the home screen icon was generated as Dealcraft's own: its {relabelled} "
            + "label(s) and its sprite already said so");
    }

    private void Reskin(string donorAppName, string donorIconLabel)
    {
        try
        {
            if (!AppLayout.TryRead(_app, _warn, out AppLayout layout))
            {
                return;
            }

            if (Retitle(layout, donorAppName, donorIconLabel) == 0)
            {
                _warn("the app's own header band was not found, so it may still read "
                    + $"'{donorAppName}'; every other label is Dealcraft's");
            }

            _view = new AppPageView(layout, _automation, _authority, _write, _log, _warn);
            _view.Build();

            // The page exists; where it starts is measured. At install the app is
            // closed and its rects hold nothing, so this usually only says so —
            // the wait that produces a real size starts when the app is opened.
            SettleScreen();
        }
        catch (Exception error)
        {
            _warn($"could not build the app's content: {error}");
        }
    }

    /// <summary>
    /// Put the page where it belongs, and keep looking while the app has no size
    /// to place it against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same bounded wait the install uses, for the same reason: a rect that
    /// has not been laid out yet reads zero, and zero is a state of the scene
    /// rather than an app with no header band. A placement taken against it
    /// would put the page at the top of a screen nobody has measured.
    /// </para>
    /// <para>
    /// The wait runs only while the app is open, because that is the only time
    /// the engine has any reason to lay this screen out — a wait that ran with
    /// the phone in the player's pocket would time out and report a fault where
    /// there is none. Closing the app ends it silently; the next open starts a
    /// new one.
    /// </para>
    /// </remarks>
    private void SettleScreen()
    {
        int generation = _generation;
        _episode++;
        int episode = _episode;

        MelonCoroutines.Start(ScenePoll.Until(
            SettleOnce,
            () => generation == _generation && episode == _episode && IsOpen(),
            GaveUpSettling));
    }

    private SceneLook SettleOnce()
    {
        try
        {
            return _view != null ? _view.Settle() : SceneLook.Settled;
        }
        catch (Exception error)
        {
            // Stop rather than keep measuring a screen that cannot be measured:
            // Settled ends the wait, and the page stays where it is.
            _warn($"the app's page could not be placed on the screen: {error.Message}");
            return SceneLook.Settled;
        }
    }

    /// <param name="verdict">
    /// Always <see cref="InstallVerdict.NeverReady"/> here: the page is there to
    /// be looked at from the first look, so the wait can only run out with
    /// something about it unsettled. Which thing it was is the view's to say.
    /// </param>
    private void GaveUpSettling(InstallVerdict verdict)
    {
        string what = _view != null && _view.Unsettled.Length > 0
            ? _view.Unsettled
            : "the app's screen never settled while the app was open";

        _warn(what);
    }

    /// <summary>
    /// Whether the app is on screen. False rather than throwing, so a component
    /// the scene has taken away ends a wait instead of breaking a coroutine.
    /// </summary>
    private bool IsOpen()
    {
        try
        {
            return _app != null && _app.isOpen;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Rename the header band by matching what it currently says against the
    /// donor's own app name. Reading the label's value is exact where guessing its
    /// place in the hierarchy would not be.
    /// </summary>
    private static int Retitle(AppLayout layout, string donorAppName, string donorIconLabel)
    {
        int changed = 0;

        foreach (Text label in Widgets.TextsOutside(layout.AppContainer.gameObject))
        {
            string current = label.text;
            if (string.IsNullOrEmpty(current))
            {
                continue;
            }

            string trimmed = current.Trim();
            if (!Names(trimmed, donorAppName) && !Names(trimmed, donorIconLabel))
            {
                continue;
            }

            label.text = trimmed == trimmed.ToUpperInvariant() ? AppName.ToUpperInvariant() : AppName;
            changed++;
        }

        return changed;
    }

    private static bool Names(string label, string appName) =>
        !string.IsNullOrEmpty(appName)
        && string.Equals(label, appName.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// How many product rows an app holds. Read off the vanilla app before and
    /// after the copy's first frames, so rows of ours landing in the game's own
    /// list say so in the log rather than only on the player's screen, and read
    /// off the copy afterwards, which is where its rows actually land.
    /// </summary>
    private static int RowCount(ProductManagerApp app) => Rows(app, a => a.entries);

    /// <summary>
    /// How many favourite rows an app holds. The second of the two lists the
    /// copy's <c>Start</c> builds, and the reason the owner's session cleared
    /// twenty rows after reporting nineteen.
    /// </summary>
    private static int FavouriteCount(ProductManagerApp app) => Rows(app, a => a.favouriteEntries);

    private static int Rows(
        ProductManagerApp app,
        Func<ProductManagerApp, Il2CppSystem.Collections.Generic.List<ProductEntry>> list)
    {
        try
        {
            Il2CppSystem.Collections.Generic.List<ProductEntry> rows = list(app);
            return rows != null ? rows.Count : ProductListReading.Unread;
        }
        catch (Exception)
        {
            return ProductListReading.Unread;
        }
    }

    /// <summary>
    /// The colour of the icon, taken from the vanilla app icon it will sit beside
    /// when that sprite can be read, so the mark belongs to the same set. White
    /// otherwise, which is what a single-colour glyph wants on a dark plate.
    /// </summary>
    private Color IconTint(Sprite donorIcon)
    {
        if (TryAverageInkColour(donorIcon, out Color sampled))
        {
            float luminance = (0.299f * sampled.r) + (0.587f * sampled.g) + (0.114f * sampled.b);
            if (luminance >= MinimumIconLuminance)
            {
                _log("icon colour sampled from the vanilla app icon");
                return new Color(sampled.r, sampled.g, sampled.b, 1f);
            }
        }

        return Color.white;
    }

    private static bool TryAverageInkColour(Sprite sprite, out Color average)
    {
        average = Color.white;

        try
        {
            if (sprite == null)
            {
                return false;
            }

            Texture2D texture = sprite.texture;
            if (texture == null || !texture.isReadable)
            {
                return false;
            }

            Rect area = sprite.textureRect;
            Il2CppStructArray<Color> pixels = texture.GetPixels(
                (int)area.x,
                (int)area.y,
                (int)area.width,
                (int)area.height);

            float r = 0f;
            float g = 0f;
            float b = 0f;
            float weight = 0f;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                r += pixel.r * pixel.a;
                g += pixel.g * pixel.a;
                b += pixel.b * pixel.a;
                weight += pixel.a;
            }

            if (weight < 1f)
            {
                return false;
            }

            average = new Color(r / weight, g / weight, b / weight, 1f);
            return true;
        }
        catch (Exception)
        {
            // An unreadable or atlassed texture is normal; the fallback covers it.
            return false;
        }
    }

    /// <summary>
    /// The vanilla app as it read in the frame the copy was made: the app itself,
    /// what it called itself, and how much it was holding. Every one of them is a
    /// "before" the install compares its "after" against, so they are read once,
    /// together, and travel together.
    /// </summary>
    private readonly struct DonorApp
    {
        public DonorApp(ProductManagerApp app, string name, string iconLabel, AppCounts counts)
        {
            App = app;
            Name = name;
            IconLabel = iconLabel;
            Counts = counts;
        }

        /// <summary>
        /// The vanilla app, and the very value the app type's singleton held when
        /// it was read — which is what has to go back into that pointer.
        /// </summary>
        public ProductManagerApp App { get; }

        /// <summary>What the app called itself, so the copy can stop saying it.</summary>
        public string Name { get; }

        /// <summary>What its home screen icon called it, for the same reason.</summary>
        public string IconLabel { get; }

        /// <summary>
        /// What it and the game were holding in that same frame.
        /// </summary>
        public AppCounts Counts { get; }
    }

    /// <summary>
    /// What an app held and what the game held, at one moment.
    /// </summary>
    /// <remarks>
    /// The two go together or neither is worth having. Rows on their own cannot
    /// say whether they grew because Dealcraft's copy wrote into them or because
    /// the game discovered more products, and that is the whole of ticket 50.
    /// </remarks>
    private readonly struct AppCounts
    {
        /// <summary>
        /// Nothing counted yet. Not the struct's own default, because four
        /// zeroes are a real answer — an empty game and an app agreeing with it
        /// — and a check nobody made must not read as one that found nothing.
        /// </summary>
        public static AppCounts Unknown => new(
            ProductListReading.Unread,
            ProductListReading.Unread,
            ProductListReading.Unread,
            ProductListReading.Unread);

        public AppCounts(int rows, int favourites, int products, int favouriteProducts)
        {
            Rows = rows;
            Favourites = favourites;
            Products = products;
            FavouriteProducts = favouriteProducts;
        }

        /// <summary>Product rows in the app, or -1 where that could not be read.</summary>
        public int Rows { get; }

        /// <summary>Favourite rows in the app, or -1.</summary>
        public int Favourites { get; }

        /// <summary>Products the game had discovered, or -1.</summary>
        public int Products { get; }

        /// <summary>Products the game held as favourites, or -1.</summary>
        public int FavouriteProducts { get; }
    }
}
