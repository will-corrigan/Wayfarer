using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace Wayfarer;

/// <summary>Opens the game's own Achievements window at one achievement — client UI navigation, not
/// a server-affecting action (see <see cref="TeleportAction"/>'s doc comment for that distinction),
/// and the same shape as <see cref="DutyFinderAction"/>.
///
/// <para><b>Why this rather than a page of our own.</b> A title's requirement, its progress bar and
/// its reward preview are all already drawn, by Square Enix, in the window the player knows. The
/// only thing that window cannot do is tell you which titles you have <i>not</i> got, which is what
/// the checklist is for — so the two fit together exactly, and redrawing the detail would be
/// redrawing something the game does well.</para></summary>
internal static unsafe class AchievementWindowAction
{
    public static void Execute(uint achievementRowId)
    {
        var agent = AgentAchievement.Instance();
        if (agent != null)
        {
            agent->OpenById(achievementRowId);
        }
    }
}
