using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.Localization;

using Europe_LemmyProject.MaritimeSystem;

namespace Europe_LemmyProject.General
{
    public class ELPInventoryCapacityModel : DefaultInventoryCapacityModel
    {
		private const int _itemAverageWeight = 10;
		private const float TroopsFactor = 2f;
		private static readonly TextObject _textTroops = new TextObject("{=5k4dxUEJ}Troops", null);
		private static readonly TextObject _textBase = new TextObject("{=basevalue}Base", null);
		
		public override int GetItemAverageWeight()
		{
			return 10;
		}
		
		public override ExplainedNumber CalculateInventoryCapacity(MobileParty mobileParty, bool includeDescriptions = false, int additionalTroops = 0, int additionalSpareMounts = 0, int additionalPackAnimals = 0, bool includeFollowers = false)
		{
			// if party is not at sea, or party is not main party, use base model
			// since we haven't implemented virtual ships for the ai, they use their default capacity model
			// while the player uses the inventory capacity of his ships
            if (!mobileParty.IsMainParty || !MaritimeManager.Instance.IsPartyAtSea(mobileParty))
            {
				return base.CalculateInventoryCapacity(mobileParty, includeDescriptions, additionalTroops, additionalSpareMounts, additionalPackAnimals, includeFollowers);
            }

			// else use our model
			float baseNumber;
			TextObject explanation;
			MobilePartyWrapper.MainPartyWrapper.GetCurrentFleetInventoryCapacity(out baseNumber, out explanation);
            
			ExplainedNumber result = new ExplainedNumber(baseNumber, includeDescriptions, explanation);
			PartyBase party = mobileParty.Party;
			
			result.LimitMin(10f);
			return result;
		}
	}
}
