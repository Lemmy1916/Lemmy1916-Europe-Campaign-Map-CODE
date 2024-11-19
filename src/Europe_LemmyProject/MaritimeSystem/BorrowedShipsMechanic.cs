using HarmonyLib;
using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace Europe_LemmyProject.MaritimeSystem
{
    /// <summary>
    /// Idea is to give the player some free, borrowed ships if he has to enter the water while on 
    /// an escort mission (and  maybe some other cases?). Basically give ships for free, but they 
    /// are "returned" whent the mission is either completed or abandoned (maybe also if the 
    /// player gets too far away from the target party).
    /// </summary>
    public class BorrowedShipsCampaignBehavior : CampaignBehaviorBase
    {

        private MaritimeManager _maritimeManager;
        public override void RegisterEvents()
        {
            CampaignEvents.OnQuestCompletedEvent.AddNonSerializedListener(this, new Action<QuestBase, QuestBase.QuestCompleteDetails>(this.OnQuestCompleted));
        }

        public BorrowedShipsCampaignBehavior()
        {
            _maritimeManager = MaritimeManager.Instance;
            MaritimeManager.OnBeforePartyEnterSeaEvent.AddNonSerializedListener(this, new Action<MobileParty>(this.OnBeforePartyEnterSea));
            MaritimeManager.OnPartyEnterSeaEvent.AddNonSerializedListener(this, new Action<MobileParty>(this.OnPartyEnterSeaEvent));
            MaritimeManager.OnBeforePartyLeaveSeaEvent.AddNonSerializedListener(this, new Action<MobileParty>(this.OnBeforePartyLeaveSea));
            MaritimeManager.OnPartyLeaveSeaEvent.AddNonSerializedListener(this, new Action<MobileParty>(this.OnPartyLeaveSeaEvent));
        }

        public override void SyncData(IDataStore dataStore)
        {
            
        }

        private void OnQuestCompleted(QuestBase questBase, QuestBase.QuestCompleteDetails questCompleteDetails)
        {
            // start second part of the borrowed ships quest
            BorrowedShipsQuest borrowedShipsQuest = Campaign.Current.QuestManager.Quests
            .FirstOrDefault
            (x => 
                x.IsOngoing && 
                x is BorrowedShipsQuest
            ) as BorrowedShipsQuest;

            if (borrowedShipsQuest != null && borrowedShipsQuest.TargetQuest == questBase)
            {
                borrowedShipsQuest.StartSecondPhase();
            }
        }

        

        private void OnBeforePartyEnterSea(MobileParty mobileParty)
        {
            
        }
        private void OnPartyEnterSeaEvent(MobileParty mobileParty)
        {
            if (mobileParty.IsMainParty)
            {
                MobileParty targetParty = mobileParty.TargetParty != null && mobileParty.TargetParty.IsCurrentlyUsedByAQuest ? mobileParty.TargetParty : null;
                if(targetParty != null)
                {
                    QuestBase targetQuest = Campaign.Current.QuestManager.Quests.FirstOrDefault(x => x.QuestGiver == targetParty.Owner);
                    if (targetQuest != null)
                    {
                        List<CampaignShip> ships = Fleet.GetMinLargestShips(mobileParty);
                        Fleet fleet = new Fleet(targetQuest.QuestGiver, ships, mobileParty, null);
                        MobilePartyWrapper.MainPartyWrapper.CurrentFleet = fleet;
                        ExplainedNumber speed = mobileParty.SpeedExplained;
                        new BorrowedShipsQuest(fleet, targetQuest, BorrowedShipsQuest.BorrowedShipsQuestReason.Quest, targetQuest.QuestGiver).StartQuest();
                    }
                }
                else if (mobileParty.Army != null && mobileParty.Army.LeaderParty != mobileParty)
                {
                    List<CampaignShip> ships = Fleet.GetMinLargestShips(mobileParty);
                    Fleet fleet = new Fleet(mobileParty.Army.LeaderParty.LeaderHero, ships, mobileParty, null);
                    MobilePartyWrapper.MainPartyWrapper.CurrentFleet = fleet;
                    ExplainedNumber speed = mobileParty.SpeedExplained;
                    new BorrowedShipsQuest(fleet, null, BorrowedShipsQuest.BorrowedShipsQuestReason.Army, mobileParty.Army.LeaderParty.LeaderHero).StartQuest();
                }
            }
        }

        private void OnBeforePartyLeaveSea(MobileParty mobileParty)
        {
            if (mobileParty.IsMainParty)
            {
                Fleet currentFleet = MobilePartyWrapper.MainPartyWrapper.CurrentFleet;
                MobileParty targetParty = mobileParty.TargetParty;
                if (currentFleet != null && currentFleet.Owner == Hero.MainHero)
                {
                    Settlement nearestSettlement = Settlement.All
                        .Where(x => PortCampaignBehavior.Instance.PortTowns.Contains(x) || x.SettlementComponent is FerryTravelPoint)
                        .MinBy(x => x.Position2D.Distance(mobileParty.Position2D));

                    MobilePartyWrapper.MainPartyWrapper.GiveCurrentFleetToSettlement(nearestSettlement);
                    TextObject message = TextObject.Empty;
                    if (nearestSettlement.SettlementComponent is Town)
                    {
                        message = new TextObject("{=HZnYOZEL1v}Your current fleet has been stored at {SETTLEMENT_NAME}");
                    }
                    else if (nearestSettlement.SettlementComponent is FerryTravelPoint)
                        message = new TextObject("{=DpWY5omszx}Your current fleet has been stored at a Ferry Point near {SETTLEMENT_NAME}");
                    else
                        message = new TextObject("***Error***");

                    message.SetTextVariable("SETTLEMENT_NAME", nearestSettlement.Name);
                    MBInformationManager.AddQuickInformation(message);
                }
            }
        }
        private void OnPartyLeaveSeaEvent(MobileParty mobileParty)
        {
            if (mobileParty.IsMainParty)
            {
                BorrowedShipsQuest borrowedShipsQuest = Campaign.Current.QuestManager.Quests.FirstOrDefault(x =>
                x.IsOngoing &&
                x is BorrowedShipsQuest) as BorrowedShipsQuest;
                if (borrowedShipsQuest != null 
                    && borrowedShipsQuest.BorrowedFleet == MobilePartyWrapper.MainPartyWrapper.CurrentFleet)
                {
                    borrowedShipsQuest.CompleteQuestWithSuccess();
                }
            }
        }

        public class BorrowedShipsQuest : QuestBase
        {
            public enum BorrowedShipsQuestReason
            {
                Quest,
                Army,
                Count
            }

            public static int ReturnDays = 2;

            [SaveableField(0)]
            private Fleet _borrowedFleet;
            public Fleet BorrowedFleet { get { return _borrowedFleet; } }
            [SaveableField(1)]
            private QuestBase _tartgetQuest;
            public QuestBase TargetQuest { get { return _tartgetQuest; } }
            [SaveableField(3)]
            private bool _isTimeHidden;
            [SaveableField(4)]
            private BorrowedShipsQuestReason _borrowType;
            public BorrowedShipsQuestReason BorrowType { get { return this._borrowType; } }

            public BorrowedShipsQuest(Fleet fleet, QuestBase targetQuest, BorrowedShipsQuestReason borrowType, Hero questGiver) : base("return_ships_quest_" + CampaignTime.Now.ToMilliseconds.ToString(), questGiver, CampaignTime.Never, 0)
            {
                _borrowedFleet = fleet;
                _isTimeHidden = true;
                _tartgetQuest = targetQuest;
                _borrowType = borrowType;
                switch (_borrowType)
                {
                    case BorrowedShipsQuestReason.Quest:
                        base.AddLog(this.PlayerStartQuestFromQuestText, false);
                        break;
                    case BorrowedShipsQuestReason.Army:
                        base.AddLog(this.PlayerStartQuestFromArmyText, false);
                        break;
                    default:
                        base.AddLog(new TextObject("***Error***"), false);
                        break;
                }
                
            }

            protected override void RegisterEvents()
            {
                CampaignEvents.MapEventEnded.AddNonSerializedListener(this, new Action<TaleWorlds.CampaignSystem.MapEvents.MapEvent>(this.OnMapEventEnded));
                CampaignEvents.OnPartyJoinedArmyEvent.AddNonSerializedListener(this, new Action<MobileParty>(this.OnPartyJoinArmy));
                CampaignEvents.PartyRemovedFromArmyEvent.AddNonSerializedListener(this, new Action<MobileParty>(this.OnPartyRemovedFromArmy));
            }

            private void OnMapEventEnded(TaleWorlds.CampaignSystem.MapEvents.MapEvent mapEvent)
            {
                BorrowedShipsQuest borrowedShipsQuest = Campaign.Current.QuestManager.Quests.FirstOrDefault(x =>
                    x.IsOngoing &&
                    x is BorrowedShipsQuest) as BorrowedShipsQuest;
                // if the player is defeated at sea with a borrowed fleet, fail the borrowed ships quest
                if (borrowedShipsQuest != null
                    && mapEvent.DefeatedSide == mapEvent.PlayerSide)
                {
                    base.AddLog(FailByLoseShipsText, false);
                    if (this.QuestGiver.MapFaction.MapFaction != Hero.MainHero.MapFaction)
                    {
                        ChangeCrimeRatingAction.Apply(QuestGiver.MapFaction, -15, true);
                    }
                    this.RelationshipChangeWithQuestGiver = -15;
                    base.CompleteQuestWithFail(null);
                }
            }

            private void OnPartyJoinArmy(MobileParty mobileParty)
            {
                if (mobileParty.IsMainParty)
                {
                    BorrowedShipsQuest borrowedShipsQuest = Campaign.Current.QuestManager.Quests
                    .FirstOrDefault
                    (x =>
                        x.IsOngoing &&
                        x is BorrowedShipsQuest
                    ) as BorrowedShipsQuest;
                    if (borrowedShipsQuest != null
                        && borrowedShipsQuest.BorrowType == BorrowedShipsQuest.BorrowedShipsQuestReason.Army
                        && mobileParty.Army != null
                        && mobileParty.Army.LeaderParty.LeaderHero == borrowedShipsQuest.QuestGiver)
                    {
                        borrowedShipsQuest.RejoinArmy();
                    }
                }
            }

            private void OnPartyRemovedFromArmy(MobileParty mobileParty)
            {
                if (mobileParty.IsMainParty)
                {
                    BorrowedShipsQuest borrowedShipsQuest = Campaign.Current.QuestManager.Quests
                    .FirstOrDefault
                    (x =>
                        x.IsOngoing &&
                        x is BorrowedShipsQuest
                    ) as BorrowedShipsQuest;
                    if (borrowedShipsQuest != null
                       && borrowedShipsQuest.BorrowType == BorrowedShipsQuest.BorrowedShipsQuestReason.Army
                       && mobileParty.Army != null
                       && mobileParty.Army.LeaderParty.LeaderHero == borrowedShipsQuest.QuestGiver)
                    {
                        borrowedShipsQuest.StartSecondPhase();
                    }
                    mobileParty.Position2D += new TaleWorlds.Library.Vec2(0.1f, 0.1f);
                }
            }

            public override bool IsSpecialQuest
            {
                get 
                { 
                    return true; 
                }
            }

            public override TextObject Title
            {
                get
                {
                    return new TextObject("{=G1RDhmezVl}Return Borrowed Ships");
                }
            }

            private TextObject PlayerStartQuestFromQuestText
            {
                get
                {
                    TextObject textObject = new TextObject("{=mfSaooHMbp}You have borrowed a fleet of ships in order to complete your quest for {QUEST_GIVER.LINK}. Once that quest is completed, you must return to land in order for the ships to make their way back to their owner.");
                    StringHelpers.SetCharacterProperties("QUEST_GIVER", base.QuestGiver.CharacterObject, textObject, false);
                    return textObject;
                }
            }

            private TextObject PlayerStartQuestFromArmyText
            {
                get
                {
                    TextObject textObject = new TextObject("{=Eq3ojL7yBq}You have borrowed a fleet of ships in order to follow the army of {QUEST_GIVER.LINK}. Once you have left the army, you must return to land in order for the ships to make their way back to their owner.");
                    StringHelpers.SetCharacterProperties("QUEST_GIVER", this.QuestGiver.CharacterObject, textObject, false);
                    return textObject;
                }
            }

            private TextObject PlayerReturnShipsQuestLogText
            {
                get
                {
                    TextObject textObject = new TextObject("{=HZnYOZEL1v}You have completed your quest for {QUEST_GIVER.LINK}. Now the owner wants his boats back. Simply return to land within {DAYS_COUNT}.");
                    StringHelpers.SetCharacterProperties("QUEST_GIVER", this.QuestGiver.CharacterObject, textObject, false);
                    textObject.SetTextVariable("DAYS_COUNT", ReturnDays);
                    return textObject;
                }
            }

            private TextObject PlayerReturnShipsForArmyText
            {
                get
                {
                    TextObject textObject = new TextObject("{=DpWY5omszx}You have left the army of {QUEST_GIVER.LINK} with the ships he lent you. Either return to land or rejoin the army within {DAYS_COUNT} days.");
                    StringHelpers.SetCharacterProperties("QUEST_GIVER", this.QuestGiver.CharacterObject, textObject, false);
                    textObject.SetTextVariable("DAYS_COUNT", ReturnDays);
                    return textObject;
                }
            }

            private TextObject PlayerRejoinArmyText
            {
                get
                {
                    TextObject textObject = new TextObject("{=EPbNTwZaiX}You have rejoined the army of {QUEST_GIVER.LINK} with the ships he lent you.");
                    StringHelpers.SetCharacterProperties("QUEST_GIVER", this.QuestGiver.CharacterObject, textObject, false);
                    return textObject;
                }
            }

            public void RejoinArmy()
            {
                base.AddLog(PlayerRejoinArmyText, false);
                //base.RemoveLog(base.JournalEntries.Last());
                base.ChangeQuestDueTime(CampaignTime.Never);
                _isTimeHidden = true;
            }

            public void StartSecondPhase()
            {
                switch (_borrowType)
                {
                    case BorrowedShipsQuestReason.Quest:
                        base.AddLog(PlayerReturnShipsQuestLogText, false);
                        break;
                    case BorrowedShipsQuestReason.Army:
                        base.AddLog(PlayerReturnShipsForArmyText, false);
                        break;
                    default:
                        break;
                }
                base.ChangeQuestDueTime(CampaignTime.DaysFromNow(2));
                _isTimeHidden = false;
            }

            private TextObject SuccessQuestSolutionLogText
            {
                get
                {
                    TextObject textObject = new TextObject("{=7rOIhINhUX}You have successfully returned the ships you borrowed from {QUEST_GIVER.LINK}.");
                    StringHelpers.SetCharacterProperties("QUEST_GIVER", this.QuestGiver.CharacterObject, textObject, false);
                    return textObject;
                }
            }

            protected override void OnCompleteWithSuccess()
            {
                base.AddLog(SuccessQuestSolutionLogText, false);
                MobilePartyWrapper.MainPartyWrapper.DestroyCurrentFleet();
            }

            private TextObject FailedQuestLogText
            {
                get
                {
                    TextObject textObject = new TextObject("{=EoG0sKAdtd}You have stolen the boats you borrowed from {QUEST_GIVER.LINK}. This has degraded his trust in you.");
                    StringHelpers.SetCharacterProperties("QUEST_GIVER", base.QuestGiver.CharacterObject, textObject, false);
                    return textObject;
                }
            }

            private TextObject FailByLoseShipsText
            {
                get
                {
                    TextObject textObject = new TextObject("{=D4fESsLIAH}You have lost the ships you borrowed from {QUEST_GIVER.LINK}. This has degraded his trust in you.");
                    StringHelpers.SetCharacterProperties("QUEST_GIVER", base.QuestGiver.CharacterObject, textObject, false);
                    return textObject;
                }
            }

            public override void OnFailed()
            {
                
            }

            protected override void OnBeforeTimedOut(ref bool completeWithSuccess, ref bool doNotResolveTheQuest)
            {
                completeWithSuccess = false;
                doNotResolveTheQuest = false;
            }

            protected override void OnTimedOut()
            {
                base.AddLog(FailedQuestLogText, false);
                if (this.QuestGiver.MapFaction.MapFaction != Hero.MainHero.MapFaction)
                {
                    ChangeCrimeRatingAction.Apply(QuestGiver.MapFaction, -10, true);
                }
                this.RelationshipChangeWithQuestGiver = -10;
            }

            public override bool IsRemainingTimeHidden
            {
                get
                {
                    return _isTimeHidden;
                }
            }

            public void HideTime(bool doHide)
            {
                _isTimeHidden = doHide;
            }

            protected override void InitializeQuestOnGameLoad()
            {
                
            }

            protected override void SetDialogs()
            {
                
            }
        }

        public class BorrowedShipsMechanicTypeDefiner : SaveableTypeDefiner
        {
            public BorrowedShipsMechanicTypeDefiner() : base(136468)
            {

            }
            protected override void DefineClassTypes()
            {
                base.AddClassDefinition(typeof(BorrowedShipsQuest), 1, null);
            }
            protected override void DefineEnumTypes()
            {
                base.AddEnumDefinition(typeof(BorrowedShipsQuest.BorrowedShipsQuestReason), 2, null);
            }
        }
    }
}
