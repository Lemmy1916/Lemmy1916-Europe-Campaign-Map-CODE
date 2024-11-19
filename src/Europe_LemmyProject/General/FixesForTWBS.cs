using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace Europe_LemmyProject.General
{
	/// <summary>
	/// This behavior makes sure a village has a TradeBound settlement. The vanilla search distance 
	/// is 150f, which means that sometimes TradeBound can be set to null if no friendly Town is 
	/// within this radius.
	/// </summary>
	public class ELPVillageTradeBoundCampaignBehavior : CampaignBehaviorBase
	{
		// Token: 0x06003AC4 RID: 15044 RVA: 0x00114D94 File Offset: 0x00112F94
		public override void RegisterEvents()
		{
			CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnNewGameCreated));
			CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, new Action<Settlement, bool, Hero, Hero, Hero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail>(this.OnSettlementOwnerChanged));
			CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnGameLoaded));
			CampaignEvents.WarDeclared.AddNonSerializedListener(this, new Action<IFaction, IFaction>(this.WarDeclared));
			CampaignEvents.MakePeace.AddNonSerializedListener(this, new Action<IFaction, IFaction>(this.OnMakePeace));
		}

		// Token: 0x06003AC5 RID: 15045 RVA: 0x00114E14 File Offset: 0x00113014
		public override void SyncData(IDataStore dataStore)
		{
		}

		// Token: 0x06003AC6 RID: 15046 RVA: 0x00114E16 File Offset: 0x00113016
		private void ClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification = true)
		{
			this.UpdateTradeBounds();
		}

		// Token: 0x06003AC7 RID: 15047 RVA: 0x00114E1E File Offset: 0x0011301E
		private void OnGameLoaded(CampaignGameStarter obj)
		{
			this.UpdateTradeBounds();
		}

		// Token: 0x06003AC8 RID: 15048 RVA: 0x00114E26 File Offset: 0x00113026
		private void OnMakePeace(IFaction faction1, IFaction faction2)
		{
			this.UpdateTradeBounds();
		}

		// Token: 0x06003AC9 RID: 15049 RVA: 0x00114E2E File Offset: 0x0011302E
		private void WarDeclared(IFaction faction1, IFaction faction2)
		{
			this.UpdateTradeBounds();
		}

		// Token: 0x06003ACA RID: 15050 RVA: 0x00114E36 File Offset: 0x00113036
		private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
		{
			this.UpdateTradeBounds();
		}

		// Token: 0x06003ACB RID: 15051 RVA: 0x00114E3E File Offset: 0x0011303E
		public void OnNewGameCreated(CampaignGameStarter campaignGameStarter)
		{
			this.UpdateTradeBounds();
		}

		// Token: 0x06003ACC RID: 15052 RVA: 0x00114E48 File Offset: 0x00113048
		private void UpdateTradeBounds()
		{
			foreach (Town town in Town.AllCastles)
			{
				foreach (Village village in town.Villages)
				{
					this.TryToAssignTradeBoundForVillage(village);
				}
			}
		}

		// Token: 0x06003ACD RID: 15053 RVA: 0x00114ED4 File Offset: 0x001130D4
		private void TryToAssignTradeBoundForVillage(Village village)
		{
			Settlement settlement = SettlementHelper.FindNearestSettlement(
				(Settlement x) => x.IsTown && 
				x.Town.MapFaction == village.Settlement.MapFaction && 
				Campaign.Current.Models.MapDistanceModel.GetDistance(x, village.Settlement) <= 150f,
				village.Settlement);

            if(settlement == null)
			{
				settlement = SettlementHelper.FindNearestSettlement(
					(Settlement x) => x.IsTown && 
					x.Town.MapFaction != village.Settlement.MapFaction && 
					!x.Town.MapFaction.IsAtWarWith(village.Settlement.MapFaction) && 
					Campaign.Current.Models.MapDistanceModel.GetDistance(x, village.Settlement) <= 150f,
					village.Settlement);
			}

			if(settlement == null)
            {
				settlement = SettlementHelper.FindNearestSettlement(
					(Settlement x) => x.IsTown &&
					x.Town.MapFaction != village.Settlement.MapFaction &&
					!x.Town.MapFaction.IsAtWarWith(village.Settlement.MapFaction),
					village.Settlement);
			}
			PropertyInfo TradeBound = village.GetType().GetProperty("TradeBound", BindingFlags.Instance | BindingFlags.Public);
			TradeBound.SetValue(village, settlement);
		}

		// Token: 0x040011E8 RID: 4584
		public const float TradeBoundDistanceLimit = 150f;
	}
}
