using Europe_LemmyProject.MaritimeSystem;
using StoryMode;
using StoryMode.GameComponents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Europe_LemmyProject.General
{
    public class ELPGameMenuModel : DefaultEncounterGameMenuModel
    {
        public override string GetEncounterMenu(PartyBase attackerParty, PartyBase defenderParty, out bool startBattle, out bool joinBattle)
        {
            Settlement settlement = base.GetEncounteredPartyBase(attackerParty, defenderParty).Settlement;
            string result = "";
            if(settlement != null && settlement.SettlementComponent is FerryTravelPoint)
            {
                result = "ferry_travel_menu";
                startBattle = false;
                joinBattle = false;
            }
            else if (settlement != null && settlement.SettlementComponent is LandingArea)
            {
                result = "landing_area_menu";
                startBattle = false;
                joinBattle = false;
            }
            else
            {
                if (StoryModeManager.Current != null)
                    result = (Campaign.Current.Models.GetGameModels().First(x => x is StoryModeEncounterGameMenuModel) as StoryModeEncounterGameMenuModel).GetEncounterMenu(attackerParty, defenderParty, out startBattle, out joinBattle); 
                else
                    result = base.GetEncounterMenu(attackerParty, defenderParty, out startBattle, out joinBattle);
            }
            return result;
        }
    }
}
