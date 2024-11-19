using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Europe_LemmyProject.General
{
    public static class ELPCheats
    {
        [CommandLineFunctionality.CommandLineArgumentFunction("main_party_teleport", "campaign")]
        public static string MainPartyTeleport(List<string> strings)
        {
            if (!CampaignCheats.CheckCheatUsage(ref CampaignCheats.ErrorType))
            {
                return CampaignCheats.ErrorType;
            }
            if (CampaignCheats.CheckHelp(strings) || CampaignCheats.CheckParameters(strings, 0) || CampaignCheats.CheckParameters(strings, 1))
            {
                return "Format is \"campaign.main_party_teleport [xPos] [yPos]\".";
            }
            float xPos = 0;
            float yPos = 0;
            if (!float.TryParse(strings[0], out xPos) || !float.TryParse(strings[1], out yPos))
            {
                return "Please enter integers";
            }
            Vec2 toPos = new Vec2(xPos, yPos);
            MobileParty.MainParty.Position2D = toPos;
            return "Success";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("clear_current_fleet_main_party", "campaign")]
        public static string RemoveCurrentShipsMainParty(List<string> strings)
        {
            if (!CampaignCheats.CheckCheatUsage(ref CampaignCheats.ErrorType))
            {
                return CampaignCheats.ErrorType;
            }
            MaritimeSystem.MaritimeManager.Instance.MainPartyWrapper.CurrentFleet.Ships.Clear();
            return "Success";
        }
        [CommandLineFunctionality.CommandLineArgumentFunction("clear_stored_fleets_main_party", "campaign")]
        public static string RemoveStoredShipsMainParty(List<string> strings)
        {
            if (!CampaignCheats.CheckCheatUsage(ref CampaignCheats.ErrorType))
            {
                return CampaignCheats.ErrorType;
            }
            MaritimeSystem.MaritimeManager.Instance.MainPartyWrapper.StoredFleets.Clear();
            return "Success";
        }
    }
}
