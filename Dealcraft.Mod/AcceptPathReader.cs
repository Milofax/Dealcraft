using System;
using Dealcraft.Core;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Phone;
using Il2CppScheduleOne.UI.Phone.Messages;
using Il2CppScheduleOne.Variables;

namespace Dealcraft;

/// <summary>
/// Whether everything accepting a deal walks through exists right now.
/// </summary>
/// <remarks>
/// Each piece is one the game reaches for without looking on its way from the
/// reply to a finished contract: the reply's message, the journal entry and the
/// quest's line on the screen, the notification. Thin on purpose; what the answer
/// means is <see cref="LifecycleGate"/>'s.
/// </remarks>
internal static class AcceptPathReader
{
    /// <summary>
    /// The missing pieces as <see cref="AcceptPath.Gap"/> spells them. A reading
    /// that throws names the path itself, which stops the pass.
    /// </summary>
    public static string Read()
    {
        try
        {
            return AcceptPath.Gap(
                ("local player", Player.Local != null),
                ("phone", PlayerSingleton<Il2CppScheduleOne.UI.Phone.Phone>.InstanceExists),
                ("journal", PlayerSingleton<JournalApp>.InstanceExists),
                ("messages app", PlayerSingleton<MessagesApp>.InstanceExists),
                ("HUD", Singleton<HUD>.InstanceExists),
                ("notifications", Singleton<NotificationsManager>.InstanceExists),
                ("coroutine service", Singleton<CoroutineService>.InstanceExists),
                ("quest manager", NetworkSingleton<QuestManager>.InstanceExists),
                ("messaging manager", NetworkSingleton<MessagingManager>.InstanceExists),
                ("variable database", NetworkSingleton<VariableDatabase>.InstanceExists),
                ("clock", NetworkSingleton<TimeManager>.InstanceExists));
        }
        catch (Exception)
        {
            return "readable accept path";
        }
    }
}
