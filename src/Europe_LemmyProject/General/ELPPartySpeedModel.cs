using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

using Europe_LemmyProject.MaritimeSystem;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Library;
using Helpers;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace Europe_LemmyProject.General
{
    public class ELPPartySpeedModel : DefaultPartySpeedCalculatingModel
    {
		private static readonly TextObject _textCargo = new TextObject("{=fSGY71wd}Cargo within capacity", null);

		// Token: 0x040007D9 RID: 2009
		private static readonly TextObject _textOverburdened = new TextObject("{=xgO3cCgR}Overburdened", null);

		// Token: 0x040007DA RID: 2010
		private static readonly TextObject _textOverPartySize = new TextObject("{=bO5gL3FI}Men within party size", null);

		// Token: 0x040007DB RID: 2011
		private static readonly TextObject _textOverPrisonerSize = new TextObject("{=Ix8YjLPD}Men within prisoner size", null);

		// Token: 0x040007DC RID: 2012
		private static readonly TextObject _textCavalry = new TextObject("{=YVGtcLHF}Cavalry", null);

		// Token: 0x040007DD RID: 2013
		private static readonly TextObject _textKhuzaitCavalryBonus = new TextObject("{=yi07dBks}Khuzait Cavalry Bonus", null);

		// Token: 0x040007DE RID: 2014
		private static readonly TextObject _textMountedFootmen = new TextObject("{=5bSWSaPl}Footmen on horses", null);

		// Token: 0x040007DF RID: 2015
		private static readonly TextObject _textWounded = new TextObject("{=aLsVKIRy}Wounded Members", null);

		// Token: 0x040007E0 RID: 2016
		private static readonly TextObject _textPrisoners = new TextObject("{=N6QTvjMf}Prisoners", null);

		// Token: 0x040007E1 RID: 2017
		private static readonly TextObject _textHerd = new TextObject("{=NhAMSaWU}Herd", null);

		// Token: 0x040007E2 RID: 2018
		private static readonly TextObject _textHighMorale = new TextObject("{=aDQcIGfH}High Morale", null);

		// Token: 0x040007E3 RID: 2019
		private static readonly TextObject _textLowMorale = new TextObject("{=ydspCDIy}Low Morale", null);

		// Token: 0x040007E4 RID: 2020
		private static readonly TextObject _textCaravan = new TextObject("{=vvabqi2w}Caravan", null);

		// Token: 0x040007E5 RID: 2021
		private static readonly TextObject _textDisorganized = new TextObject("{=JuwBb2Yg}Disorganized", null);

		// Token: 0x040007E6 RID: 2022
		private static readonly TextObject _movingInForest = new TextObject("{=rTFaZCdY}Forest", null);

		// Token: 0x040007E7 RID: 2023
		private static readonly TextObject _fordEffect = new TextObject("{=NT5fwUuJ}Fording", null);

		// Token: 0x040007E8 RID: 2024
		private static readonly TextObject _night = new TextObject("{=fAxjyMt5}Night", null);

		// Token: 0x040007E9 RID: 2025
		private static readonly TextObject _snow = new TextObject("{=vLjgcdgB}Snow", null);

		// Token: 0x040007EA RID: 2026
		private static readonly TextObject _desert = new TextObject("{=ecUwABe2}Desert", null);

		// Token: 0x040007EB RID: 2027
		private static readonly TextObject _sturgiaSnowBonus = new TextObject("{=0VfEGekD}Sturgia Snow Bonus", null);

		// Token: 0x040007EC RID: 2028
		private static TextObject _culture { get { return GameTexts.FindText("str_culture", null); } }

		// Token: 0x040007ED RID: 2029
		private const float MovingAtForestEffect = -0.3f;

		// Token: 0x040007EE RID: 2030
		private const float MovingAtWaterEffect = -0.3f;

		// Token: 0x040007EF RID: 2031
		private const float MovingAtNightEffect = -0.25f;

		// Token: 0x040007F0 RID: 2032
		private const float MovingOnSnowEffect = -0.1f;

		// Token: 0x040007F1 RID: 2033
		private const float MovingInDesertEffect = -0.1f;

		// Token: 0x040007F2 RID: 2034
		private const float CavalryEffect = 0.4f;

		// Token: 0x040007F3 RID: 2035
		private const float MountedFootMenEffect = 0.2f;

		// Token: 0x040007F4 RID: 2036
		private const float HerdEffect = -0.4f;

		// Token: 0x040007F5 RID: 2037
		private const float WoundedEffect = -0.05f;

		// Token: 0x040007F6 RID: 2038
		private const float CargoEffect = -0.02f;

		// Token: 0x040007F7 RID: 2039
		private const float OverburdenedEffect = -0.4f;

		// Token: 0x040007F8 RID: 2040
		private const float HighMoraleThresold = 70f;

		// Token: 0x040007F9 RID: 2041
		private const float LowMoraleThresold = 30f;

		// Token: 0x040007FA RID: 2042
		private const float HighMoraleEffect = 0.05f;

		// Token: 0x040007FB RID: 2043
		private const float LowMoraleEffect = -0.1f;

		// Token: 0x040007FC RID: 2044
		private const float DisorganizedEffect = -0.4f;
		public override ExplainedNumber CalculateBaseSpeed(MobileParty mobileParty, bool includeDescriptions = false, int additionalTroopOnFootCount = 0, int additionalTroopOnHorseCount = 0)
        {
			// if on land use vanilla speed model
            if (!MaritimeManager.Instance.IsPartyAtSea(mobileParty))
				return base.CalculateBaseSpeed(mobileParty, includeDescriptions, additionalTroopOnFootCount, additionalTroopOnHorseCount);
			// else use our model

			// if party is in army, use army leader's speed
			if (mobileParty.Army != null && mobileParty.Army.LeaderParty.AttachedParties.Contains(mobileParty))
			{
				return this.CalculateBaseSpeed(mobileParty.Army.LeaderParty, includeDescriptions, 0, 0);
			}

			// if party is on tracks, use default speed
            if (mobileParty.IsMainParty && MaritimeManager.Instance.CurrentMaritimePathTravel != null)
            {
				return new ExplainedNumber(10f, includeDescriptions, new TextObject("{=LsJS1T4zsi}Maritime Transport"));
            }

			PartyBase party = mobileParty.Party;
			int totalAvailableMounts = 0;
			float totalWeight = 0f;
			int totalHerdSize = 0;
			int totalTroopCount = mobileParty.MemberRoster.TotalManCount + additionalTroopOnFootCount + additionalTroopOnHorseCount;
			AddCargoStats(mobileParty, ref totalAvailableMounts, ref totalWeight, ref totalHerdSize);
			float totalWeight2 = mobileParty.ItemRoster.TotalWeight;
			int totalInventoryCapacity = (int)Campaign.Current.Models.InventoryCapacityModel.CalculateInventoryCapacity(mobileParty, false, additionalTroopOnFootCount, additionalTroopOnHorseCount, 0, false).ResultNumber;
			int totalTroopsOnHorse = party.NumberOfMenWithHorse + additionalTroopOnHorseCount;
			int totalTroopsOnFoot = party.NumberOfMenWithoutHorse + additionalTroopOnFootCount;
			int totalTroopsWounded = party.MemberRoster.TotalWounded;
			int totalPrisoners = party.PrisonRoster.TotalManCount;
			float morale = mobileParty.Morale;
			if (mobileParty.AttachedParties.Count != 0)
			{
				foreach (MobileParty mobileParty2 in mobileParty.AttachedParties)
				{
					AddCargoStats(mobileParty2, ref totalAvailableMounts, ref totalWeight, ref totalHerdSize);
					totalTroopCount += mobileParty2.MemberRoster.TotalManCount;
					totalWeight2 += mobileParty2.ItemRoster.TotalWeight;
					totalInventoryCapacity += mobileParty2.InventoryCapacity;
					totalTroopsOnHorse += mobileParty2.Party.NumberOfMenWithHorse;
					totalTroopsOnFoot += mobileParty2.Party.NumberOfMenWithoutHorse;
					totalTroopsWounded += mobileParty2.MemberRoster.TotalWounded;
					totalPrisoners += mobileParty2.PrisonRoster.TotalManCount;
				}
			}
			float baseNumber;
            TextObject explanation;
            if (mobileParty.IsMainParty)
            {
				// set base speed to the speed of the slowest ship
				MobilePartyWrapper.MainPartyWrapper.GetCurrentFleetSpeed(out baseNumber, out explanation);
            }
            else
            {
				new MobilePartyWrapper(mobileParty).GetCurrentFleetSpeed(out baseNumber, out explanation);
			}

			ExplainedNumber result = new ExplainedNumber(baseNumber, includeDescriptions, explanation);
			float num12 = MathF.Min(totalWeight2, (float)totalInventoryCapacity);
			if (num12 > 0f)
			{
				float cargoEffect = this.GetCargoEffect(num12, totalInventoryCapacity);
				result.AddFactor(cargoEffect, _textCargo);
			}
			if (totalWeight > (float)totalInventoryCapacity)
			{
				float overBurdenedEffect = this.GetOverBurdenedEffect(totalWeight - (float)totalInventoryCapacity, totalInventoryCapacity);
				result.AddFactor(overBurdenedEffect, _textOverburdened);
				if (mobileParty.HasPerk(DefaultPerks.Athletics.Energetic, false))
				{
					result.AddFactor(overBurdenedEffect * DefaultPerks.Athletics.Energetic.PrimaryBonus * 0.01f, DefaultPerks.Athletics.Energetic.Name);
				}
				if (mobileParty.HasPerk(DefaultPerks.Scouting.Unburdened, false))
				{
					result.AddFactor(overBurdenedEffect * DefaultPerks.Scouting.Unburdened.PrimaryBonus * 0.01f, DefaultPerks.Scouting.Unburdened.Name);
				}
			}
			if (mobileParty.Party.NumberOfAllMembers > mobileParty.Party.PartySizeLimit)
			{
				float overPartySizeEffect = this.GetOverPartySizeEffect(mobileParty);
				result.AddFactor(overPartySizeEffect, _textOverPartySize);
			}
			float woundedModifier = this.GetWoundedModifier(totalTroopCount, totalTroopsWounded, mobileParty);
			result.AddFactor(woundedModifier, _textWounded);
			if (!mobileParty.IsCaravan)
			{
				if (mobileParty.Party.NumberOfPrisoners > mobileParty.Party.PrisonerSizeLimit)
				{
					float overPrisonerSizeEffect = this.GetOverPrisonerSizeEffect(mobileParty);
					result.AddFactor(overPrisonerSizeEffect, _textOverPrisonerSize);
				}
				float sizeModifierPrisoner = GetSizeModifierPrisoner(totalTroopCount, totalPrisoners);
				result.AddFactor(1f / sizeModifierPrisoner - 1f, ELPPartySpeedModel._textPrisoners);
			}
			if (morale > 70f)
			{
				result.AddFactor(0.05f * ((morale - 70f) / 30f), _textHighMorale);
			}
			if (morale < 30f)
			{
				result.AddFactor(-0.1f * (1f - mobileParty.Morale / 30f), _textLowMorale);
			}
			if (mobileParty == MobileParty.MainParty)
			{
				float playerMapMovementSpeedBonusMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerMapMovementSpeedBonusMultiplier();
				if (playerMapMovementSpeedBonusMultiplier > 0f)
				{
					result.AddFactor(playerMapMovementSpeedBonusMultiplier, GameTexts.FindText("str_game_difficulty", null));
				}
			}
			if (mobileParty.IsCaravan)
			{
				result.AddFactor(0.1f, _textCaravan);
			}
			if (mobileParty.IsDisorganized)
			{
				result.AddFactor(-0.4f, _textDisorganized);
			}

			// add speed penalty for having undermanned ships
			if (mobileParty.IsMainParty)
			{
				int troopsCount = mobileParty.MemberRoster.TotalManCount;
				int crewCount = MobilePartyWrapper.MainPartyWrapper.CurrentFleet != null ? MobilePartyWrapper.MainPartyWrapper.CurrentFleet.CrewCapacity : 0;
				if (troopsCount < crewCount)
				{
					float ratio = 1f - ( (float)troopsCount / (float)crewCount);
					result.AddFactor(-ratio, new TextObject("{=7fhW4Xf87Q}Undermanned Ships"));
				}
			}

			result.LimitMin(this.MinimumSpeed);
			return result;
		}

		private float GetWoundedModifier(int totalMenCount, int numWounded, MobileParty party)
		{
			if (numWounded <= totalMenCount / 4)
			{
				return 0f;
			}
			if (totalMenCount == 0)
			{
				return -0.5f;
			}
			float baseNumber = MathF.Max(-0.8f, -0.05f * (float)numWounded / (float)totalMenCount);
			ExplainedNumber explainedNumber = new ExplainedNumber(baseNumber, false, null);
			PerkHelper.AddPerkBonusForParty(DefaultPerks.Medicine.Sledges, party, true, ref explainedNumber);
			
			return explainedNumber.ResultNumber;
		}

		private static float GetSizeModifierPrisoner(int totalMenCount, int totalPrisonerCount)
		{
			return MathF.Pow((10f + (float)totalMenCount + (float)totalPrisonerCount) / (10f + (float)totalMenCount), 0.33f);
		}

		public override ExplainedNumber CalculateFinalSpeed(MobileParty mobileParty, ExplainedNumber finalSpeed)
        {
			// if on land, use vanilla speed model
			if (!MaritimeManager.Instance.IsPartyAtSea(mobileParty))
			{
				if (mobileParty.IsCustomParty && !((CustomPartyComponent)mobileParty.PartyComponent).CustomPartyBaseSpeed.ApproximatelyEqualsTo(0f, 1E-05f))
				{
					finalSpeed = new ExplainedNumber(((CustomPartyComponent)mobileParty.PartyComponent).CustomPartyBaseSpeed, false, null);
				}
				TerrainType faceTerrainType = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(mobileParty.CurrentNavigationFace);
				Hero effectiveScout = mobileParty.EffectiveScout;
				if (faceTerrainType == TerrainType.Forest)
				{
					float num = 0f;
					if (effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.ForestKin))
					{
						for (int i = 0; i < mobileParty.MemberRoster.Count; i++)
						{
							if (mobileParty.MemberRoster.GetCharacterAtIndex(i).IsInfantry)
							{
								num += (float)mobileParty.MemberRoster.GetElementNumber(i);
							}
						}
					}
					float value = (num / (float)mobileParty.MemberRoster.Count > 0.75f) ? -0.15f : -0.3f;
					finalSpeed.AddFactor(value, _movingInForest);
					if (PartyBaseHelper.HasFeat(mobileParty.Party, DefaultCulturalFeats.BattanianForestSpeedFeat))
					{
						float value2 = DefaultCulturalFeats.BattanianForestSpeedFeat.EffectBonus * 0.3f;
						finalSpeed.AddFactor(value2, _culture);
					}
				}
				else if (faceTerrainType == TerrainType.Bridge || faceTerrainType == TerrainType.ShallowRiver)
				{
					finalSpeed.AddFactor(-0.3f, _fordEffect);
				}
				else if (faceTerrainType == TerrainType.Desert || faceTerrainType == TerrainType.Dune)
				{
					if (!PartyBaseHelper.HasFeat(mobileParty.Party, DefaultCulturalFeats.AseraiDesertFeat))
					{
						finalSpeed.AddFactor(-0.1f, _desert);
					}
					if (effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.DesertBorn))
					{
						finalSpeed.AddFactor(DefaultPerks.Scouting.DesertBorn.PrimaryBonus, DefaultPerks.Scouting.DesertBorn.Name);
					}
				}
				else if ((faceTerrainType == TerrainType.Plain || faceTerrainType == TerrainType.Steppe) && effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.Pathfinder))
				{
					finalSpeed.AddFactor(DefaultPerks.Scouting.Pathfinder.PrimaryBonus, DefaultPerks.Scouting.Pathfinder.Name);
				}
				if (Campaign.Current.Models.MapWeatherModel.GetIsSnowTerrainInPos(mobileParty.Position2D.ToVec3(0f)))
				{
					finalSpeed.AddFactor(-0.1f, _snow);
				}
				if (Campaign.Current.IsNight)
				{
					finalSpeed.AddFactor(-0.25f, _night);
					if (effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.NightRunner))
					{
						finalSpeed.AddFactor(DefaultPerks.Scouting.NightRunner.PrimaryBonus, DefaultPerks.Scouting.NightRunner.Name);
					}
				}
				else if (effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.DayTraveler))
				{
					finalSpeed.AddFactor(DefaultPerks.Scouting.DayTraveler.PrimaryBonus, DefaultPerks.Scouting.DayTraveler.Name);
				}
				if (effectiveScout != null)
				{
					PerkHelper.AddEpicPerkBonusForCharacter(DefaultPerks.Scouting.UncannyInsight, effectiveScout.CharacterObject, DefaultSkills.Scouting, true, ref finalSpeed, 200);
					if (effectiveScout.GetPerkValue(DefaultPerks.Scouting.ForcedMarch) && mobileParty.Morale > 75f)
					{
						finalSpeed.AddFactor(DefaultPerks.Scouting.ForcedMarch.PrimaryBonus, DefaultPerks.Scouting.ForcedMarch.Name);
					}
					if (mobileParty.DefaultBehavior == AiBehavior.EngageParty)
					{
						MobileParty targetParty = mobileParty.TargetParty;
						if (targetParty != null && targetParty.MapFaction.IsAtWarWith(mobileParty.MapFaction) && effectiveScout.GetPerkValue(DefaultPerks.Scouting.Tracker))
						{
							finalSpeed.AddFactor(DefaultPerks.Scouting.Tracker.SecondaryBonus, DefaultPerks.Scouting.Tracker.Name);
						}
					}
				}
				Army army = mobileParty.Army;
				if (((army != null) ? army.LeaderParty : null) != null && mobileParty.Army.LeaderParty != mobileParty && mobileParty.AttachedTo != mobileParty.Army.LeaderParty && mobileParty.Army.LeaderParty.HasPerk(DefaultPerks.Tactics.CallToArms, false))
				{
					finalSpeed.AddFactor(DefaultPerks.Tactics.CallToArms.PrimaryBonus, DefaultPerks.Tactics.CallToArms.Name);
				}
				finalSpeed.LimitMin(this.MinimumSpeed);
				return finalSpeed;
			}
			else // else use our model
			{
				if (mobileParty.IsCustomParty && !((CustomPartyComponent)mobileParty.PartyComponent).CustomPartyBaseSpeed.ApproximatelyEqualsTo(0f, 1E-05f))
				{
					finalSpeed = new ExplainedNumber(((CustomPartyComponent)mobileParty.PartyComponent).CustomPartyBaseSpeed, false, null);
				}

				Hero effectiveScout = mobileParty.EffectiveScout;
				if (Campaign.Current.IsNight)
				{
					finalSpeed.AddFactor(-0.25f, _night);
					if (effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.NightRunner))
					{
						finalSpeed.AddFactor(DefaultPerks.Scouting.NightRunner.PrimaryBonus, DefaultPerks.Scouting.NightRunner.Name);
					}
				}
				else if (effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.DayTraveler))
				{
					finalSpeed.AddFactor(DefaultPerks.Scouting.DayTraveler.PrimaryBonus, DefaultPerks.Scouting.DayTraveler.Name);
				}
				if (effectiveScout != null)
				{
					PerkHelper.AddEpicPerkBonusForCharacter(DefaultPerks.Scouting.UncannyInsight, effectiveScout.CharacterObject, DefaultSkills.Scouting, true, ref finalSpeed, 200);
					if (effectiveScout.GetPerkValue(DefaultPerks.Scouting.ForcedMarch) && mobileParty.Morale > 75f)
					{
						finalSpeed.AddFactor(DefaultPerks.Scouting.ForcedMarch.PrimaryBonus, DefaultPerks.Scouting.ForcedMarch.Name);
					}
					if (mobileParty.DefaultBehavior == AiBehavior.EngageParty)
					{
						MobileParty targetParty = mobileParty.TargetParty;
						if (targetParty != null && targetParty.MapFaction.IsAtWarWith(mobileParty.MapFaction) && effectiveScout.GetPerkValue(DefaultPerks.Scouting.Tracker))
						{
							finalSpeed.AddFactor(DefaultPerks.Scouting.Tracker.SecondaryBonus, DefaultPerks.Scouting.Tracker.Name);
						}
					}
				}

				Army army = mobileParty.Army;
				if (((army != null) ? army.LeaderParty : null) != null && mobileParty.Army.LeaderParty != mobileParty && mobileParty.AttachedTo != mobileParty.Army.LeaderParty && mobileParty.Army.LeaderParty.HasPerk(DefaultPerks.Tactics.CallToArms, false))
				{
					finalSpeed.AddFactor(DefaultPerks.Tactics.CallToArms.PrimaryBonus, DefaultPerks.Tactics.CallToArms.Name);
				}
				finalSpeed.LimitMin(this.MinimumSpeed);
				return finalSpeed;
			}
		}

		private static void AddCargoStats(MobileParty mobileParty, ref int numberOfAvailableMounts, ref float totalWeightCarried, ref int herdSize)
		{
			ItemRoster itemRoster = mobileParty.ItemRoster;
			int numberOfPackAnimals = itemRoster.NumberOfPackAnimals;
			int numberOfLivestockAnimals = itemRoster.NumberOfLivestockAnimals;
			herdSize += numberOfPackAnimals + numberOfLivestockAnimals;
			numberOfAvailableMounts += itemRoster.NumberOfMounts;
			totalWeightCarried += itemRoster.TotalWeight;
		}
		private float CalculateBaseSpeedForParty(int menCount)
		{
			return this.BaseSpeed * MathF.Pow(200f / (200f + (float)menCount), 0.4f);
		}
		private float GetCargoEffect(float weightCarried, int partyCapacity)
		{
			return -0.02f * weightCarried / (float)partyCapacity;
		}

		// Token: 0x060015DF RID: 5599 RVA: 0x00067572 File Offset: 0x00065772
		private float GetOverBurdenedEffect(float totalWeightCarried, int partyCapacity)
		{
			return -0.4f * (totalWeightCarried / (float)partyCapacity);
		}

		// Token: 0x060015E0 RID: 5600 RVA: 0x00067580 File Offset: 0x00065780
		private float GetOverPartySizeEffect(MobileParty mobileParty)
		{
			int partySizeLimit = mobileParty.Party.PartySizeLimit;
			int numberOfAllMembers = mobileParty.Party.NumberOfAllMembers;
			return 1f / ((float)numberOfAllMembers / (float)partySizeLimit) - 1f;
		}

		// Token: 0x060015E1 RID: 5601 RVA: 0x000675B8 File Offset: 0x000657B8
		private float GetOverPrisonerSizeEffect(MobileParty mobileParty)
		{
			int prisonerSizeLimit = mobileParty.Party.PrisonerSizeLimit;
			int numberOfPrisoners = mobileParty.Party.NumberOfPrisoners;
			return 1f / ((float)numberOfPrisoners / (float)prisonerSizeLimit) - 1f;
		}
	}
}
