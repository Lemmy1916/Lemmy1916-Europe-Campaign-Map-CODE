using SandBox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Overlay;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ModuleManager;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

using Europe_LemmyProject.General;
using Europe_LemmyProject.MaritimeSystem.ShipManagementUI;
using TaleWorlds.CampaignSystem.Actions;
using System.Reflection;

namespace Europe_LemmyProject.MaritimeSystem
{
    /// <summary>
    /// Settlement type for traveling shor distances over water
    /// </summary>
    public class FerryTravelPoint : SettlementComponent
    {
        public string DestinationId { get; private set; }

        protected override void OnInventoryUpdated(ItemRosterElement item, int count)
        {

        }

        public override void Deserialize(MBObjectManager objectManager, XmlNode node)
        {
            base.Deserialize(objectManager, node);
            base.BackgroundCropPosition = float.Parse(node.Attributes["background_crop_position"].Value);
            base.BackgroundMeshName = node.Attributes["background_mesh"].Value;
            base.WaitMeshName = node.Attributes["wait_mesh"].Value;
            this.DestinationId = node.Attributes["destination_id"].Value;
        }

        public override void OnPartyLeft(MobileParty mobileParty)
        {
            //((PartyVisual)mobileParty.Party.Visuals).SetMapIconAsDirty();
            //((PartyVisual)mobileParty.Party.Visuals).ValidateIsDirty(mobileParty.Party, 0.1f, 0.1f);
        }
    }

    /// <summary>
    /// Defines campaign behavior for FerryTravelPoints.
    /// </summary>
    public class FerryCampaignBehavior : CampaignBehaviorBase
    {
        public static FerryCampaignBehavior Instance { get; private set; }

        private MaritimeManager _maritimeManager;
        private List<Settlement> _activeFerries;
        private bool _doTransfer;
        private FerryGameMenu _gameMenu;

        public List<Settlement> ActiveFerries { get { return this._activeFerries; } }

        public FerryCampaignBehavior()
        {
            Instance = this;
        }
        
        public static Tuple<Path, bool> GetFerryPathTuple(Settlement ferryPoint1, Settlement ferryPoint2)
        {
            string possibleName1 = ferryPoint1.StringId + "_" + ferryPoint2.StringId;
            string possibleName2 = ferryPoint2.StringId + "_" + ferryPoint1.StringId;
            Path path = null;
            bool fromStart = true;
            Path path1 = (Campaign.Current.MapSceneWrapper as MapScene).Scene.GetPathWithName(possibleName1);
            Path path2 = (Campaign.Current.MapSceneWrapper as MapScene).Scene.GetPathWithName(possibleName2);
            if (path1 != null)
            {
                path = path1;
                fromStart = false;
            }
            else if (path2 != null)
            {
                path = path2;
                fromStart = true;
            }

            Debug.Assert(path != null, "Neither " + possibleName1 + " or " + possibleName2 + " are valid path names");
            return new Tuple<Path, bool>(path, fromStart);
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnAfterNewGameCreated));
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnAfterNewGameCreated));
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, new Action(this.OnGameLoadFinished));
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, new Action<MobileParty, Settlement, Hero>(this.OnSettlementEntered));
            CampaignEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(this, new Action(this.OnCharacterCreationIsOver));
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, new Action<MobileParty, Settlement>(this.OnSettlementLeft));
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnCharacterCreationIsOver()
        {
            _maritimeManager = MaritimeManager.Instance;
            this.InitializeFerries();
        }

        private void OnGameLoadFinished()
        {
            _maritimeManager = MaritimeManager.Instance;
            this.InitializeFerries();
            this.LoadStoredShips(_activeFerries);
        }

        private void LoadStoredShips(List<Settlement> ferryPoints)
        {
            List<Fleet> storedFleets = _maritimeManager.MainPartyWrapper.StoredFleets;
            foreach (Settlement newSettlement in ferryPoints)
            {
                Fleet landedFleet = storedFleets?.FirstOrDefault(x => x.Settlement != null && x.Settlement.StringId == newSettlement.StringId);
                if (landedFleet != null)
                {
                    landedFleet.TransferToSettlement(newSettlement);
                }
            }
        }

        private void OnAfterNewGameCreated(CampaignGameStarter starter)
        {
            _gameMenu = new FerryGameMenu();
            _gameMenu.Initialize(starter);
        }

        private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
        {
            if (settlement.SettlementComponent is FerryTravelPoint && _maritimeManager.IsPartyAtSea(party))
            {
                _maritimeManager.RemoveFromSea(party);
                //PlayerEncounter.LeaveSettlement();
                //PlayerEncounter.Finish(true);
                //_maritimeManager.CurrentMaritimePathTravel = null;
            }
        }

        private void OnSettlementLeft(MobileParty party, Settlement settlement)
        {
            FerryTravelPoint ferryPoint = settlement.SettlementComponent as FerryTravelPoint;
            if (ferryPoint != null && _doTransfer)
            {
                if (party != null && party.IsMainParty && _doTransfer)
                {
                    if (!MaritimeManager.Instance.IsPartyAtSea(party)) 
                    {
                        float length= _gameMenu.TravelBuilder.PathTracker.Path.GetTotalLength();
                        Vec2 p1 = _gameMenu.TravelBuilder.PathTracker.Path.GetFrameForDistance(0f).origin.AsVec2;
                        Vec2 p2 = _gameMenu.TravelBuilder.PathTracker.Path.GetFrameForDistance(length).origin.AsVec2;
                        float len1 = settlement.Position2D.Distance(p1);
                        float len2 = settlement.Position2D.Distance(p2);
                        Vec2 pos = len1 < len2 ? p1 : p2;
                        party.Position2D = pos;
                    }

                    Patches.MobileParty_OnAiTick_Patch(party);
                    _doTransfer = false;
                }
                if (party != null && party.Army != null && MobileParty.MainParty.AttachedParties.Contains(party) && _doTransfer)
                {
                    if (!MaritimeManager.Instance.IsPartyAtSea(party))
                    {
                        float length = _gameMenu.TravelBuilder.PathTracker.Path.GetTotalLength();
                        Vec2 p1 = _gameMenu.TravelBuilder.PathTracker.Path.GetFrameForDistance(0f).origin.AsVec2;
                        Vec2 p2 = _gameMenu.TravelBuilder.PathTracker.Path.GetFrameForDistance(length).origin.AsVec2;
                        float len1 = settlement.Position2D.Distance(p1);
                        float len2 = settlement.Position2D.Distance(p2);
                        Vec2 pos = len1 < len2 ? p1 : p2;
                        party.Position2D = pos;
                    }

                    Patches.MobileParty_OnAiTick_Patch(party);
                }
            }
        }

        private void InitializeFerries()
        {
            _activeFerries = new List<Settlement>();
            XmlReaderSettings readerSettings = new XmlReaderSettings();
            readerSettings.IgnoreComments = true;
            string path = ModuleHelper.GetModuleFullPath("Europe_LemmyProject") + "ModuleData/ELPData/FerryPoints.xml";
            using(XmlReader reader = XmlReader.Create(path, readerSettings))
            {
                XmlDocument ferryPointsDoc = new XmlDocument();
                ferryPointsDoc.Load(path);
                XmlNode settlementsNode = ferryPointsDoc.ChildNodes[1];
                foreach (XmlNode ferryNode in settlementsNode)
                {
                    Settlement ferryPoint = (Settlement)Campaign.Current.ObjectManager.CreateObjectFromXmlNode(ferryNode);
                    ferryPoint.Party.Visuals.SetMapIconAsDirty();
                    List<Settlement> l = Settlement.All.Where(x => x.SettlementComponent is FerryTravelPoint).ToList();
                    ferryPoint.OnGameCreated();
                    ELPHelpers.AddNameplate(ferryPoint);
                    _activeFerries.Add(ferryPoint);
                }
            }

            if (
                Settlement.CurrentSettlement != null
                && Settlement.CurrentSettlement.SettlementComponent == null
                && PlayerEncounter.Current != null
                && PlayerEncounter.Current.EncounterSettlementAux != null
                && MobileParty.MainParty != null
                && MobileParty.MainParty.CurrentSettlement == PlayerEncounter.Current.EncounterSettlementAux)
            {
                Settlement newFerry = _activeFerries.FirstOrDefault(x => x.StringId == Settlement.CurrentSettlement.StringId);
                if(newFerry != null)
                {
                    MobileParty.MainParty.CurrentSettlement = newFerry;
                    MobileParty.MainParty.SetMoveGoToSettlement(newFerry);
                    PlayerEncounter.Current.SetupFields(MobileParty.MainParty.Party, newFerry.Party);
                }
            }
        }

        private void SetDoTransfer(bool val)
        {
            _doTransfer = val;
        }

        private class FerryGameMenu
        {
            private MaritimePathTravel _travelBuilder;
            public MaritimePathTravel TravelBuilder { get { return _travelBuilder; } }
            private int _currentPrice;
            List<Fleet> _availableFleets;
            
            public void Initialize(CampaignGameStarter starter)
            {
                // ferry travel menu
                starter.AddGameMenu(
                    "ferry_travel_menu", 
                    "{=G1RDhmezVl}The ferryman will take you across for {TRAVEL_COST}{GOLD_ICON}",
                    new OnInitDelegate(
                        delegate (MenuCallbackArgs args)
                        {
                            // format for ferry path is settlement1.StringId + "_" + settlement2.StringId
                            FerryTravelPoint component = (Settlement.CurrentSettlement.SettlementComponent as FerryTravelPoint);
                            Settlement destination = Settlement.Find(component.DestinationId);
                            _travelBuilder = new MaritimePathTravel();
                            _travelBuilder.FromSettlement = Settlement.CurrentSettlement;
                            _travelBuilder.ToSettlement = destination;
                            Tuple<Path, bool> activePathTuple = GetFerryPathTuple(_travelBuilder.FromSettlement, _travelBuilder.ToSettlement);
                            _travelBuilder.PathTracker = new ReversiblePathTracker(activePathTuple.Item1, MobileParty.MainParty.Party.Visuals.GetFrame().GetScale(), activePathTuple.Item2);

                            float realDistance = PortCampaignBehavior.Instance.PortTravelModel.GetRealDistance(activePathTuple.Item1);

                            int totalPassengers = MobileParty.MainParty.MemberRoster.TotalManCount + MobileParty.MainParty.PrisonRoster.TotalManCount;
                            float totalWeight = MobileParty.MainParty.ItemRoster.TotalWeight;

                            if (MobileParty.MainParty.Army != null)
                            {
                                foreach (MobileParty party in MobileParty.MainParty.AttachedParties)
                                {
                                    totalPassengers += party.MemberRoster.TotalManCount + party.PrisonRoster.TotalManCount;
                                    totalWeight += party.ItemRoster.TotalWeight;
                                }
                            }

                            _currentPrice = PortCampaignBehavior.Instance.PortTravelModel.GetBaseTravelCost(realDistance, MobileParty.MainParty, totalPassengers, totalWeight);

                            MBTextManager.SetTextVariable("TRAVEL_COST", _currentPrice);

                            _availableFleets = MobilePartyWrapper.MainPartyWrapper.GetFleetsAtSettlement(Settlement.CurrentSettlement);
                        }
                    ), GameOverlays.MenuOverlayType.None, GameMenu.MenuFlags.None, null);

                starter.AddGameMenuOption(
                    "ferry_travel_menu", 
                    "ferry_accept",
                    "{=mfSaooHMbp}Pay and cross",
                    delegate (MenuCallbackArgs args)
                    {
                        args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                        if (Hero.MainHero.Gold < _currentPrice)
                        {
                            args.Tooltip = new TextObject("{=Eq3ojL7yBq}Not enough gold", null);
                            args.IsEnabled = false;
                        }
                        return true;
                    },
                    delegate (MenuCallbackArgs args)
                    {
                        // subtract gold and depart to ferry route
                        TaleWorlds.CampaignSystem.Actions.GiveGoldAction.ApplyBetweenCharacters(MobileParty.MainParty.LeaderHero, null, _currentPrice, false);
                        MaritimeManager.Instance.SetMartitimeTravel(_travelBuilder,
                                                            delegate ()
                                                            {
                                                                Vec2 pos = MaritimeManager.Instance.CurrentMaritimePathTravel.ToSettlement.GatePosition;
                                                                MobileParty.MainParty.Position2D = pos;
                                                                MobileParty.MainParty.SetMoveModeHold();
                                                                Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
                                                            });
                        PlayerEncounter.LeaveSettlement();
                        PlayerEncounter.Finish(true);
                    }, true, -1, false);

                starter.AddGameMenuOption(
                    "ferry_travel_menu",
                    "ferry_manage_fleets",
                    "{=3im6aN6sSu}Manage landed fleets",
                    delegate (MenuCallbackArgs args)
                    {
                        args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                        return !MaritimeManager.Instance.IsPartyAtSea(MobileParty.MainParty) && _availableFleets.Count > 0;
                    },
                    delegate (MenuCallbackArgs args)
                    {
                        ShipManagementUIManager.Instance.Launch(_availableFleets, args);
                    },
                    true, -1, false);

                starter.AddGameMenuOption(
                    "ferry_travel_menu",
                    "ferry_set_sail",
                    "{=mer6pjy1Mn}Set sail",
                    delegate (MenuCallbackArgs args)
                    {
                        args.optionLeaveType = GameMenuOption.LeaveType.Leave;

                        if (_availableFleets.Count == 1)
                        {
                            TextObject explanation;
                            if (!Fleet.CanSetSail(_availableFleets[0], out explanation))
                            {
                                args.IsEnabled = false;
                                args.Tooltip = explanation;
                            }
                            else
                            {
                                args.IsEnabled = true;
                                args.Tooltip = explanation;
                            }
                        }

                        return _availableFleets.Count > 0;
                    },
                    delegate (MenuCallbackArgs args)
                    {
                        // if only one fleet, then set sail with that fleet
                        if (_availableFleets.Count == 1)
                        {
                            Fleet fleet = _availableFleets.First();
                            FerryCampaignBehavior.Instance.SetDoTransfer(true);
                            MaritimeManager.Instance.MainPartyWrapper.TakeFleetFromSettlement(fleet);
                            PlayerEncounter.LeaveSettlement();
                            PlayerEncounter.Finish(true);
                            return;
                        }

                        // otherwise choose which fleet to set sail with
                        List<InquiryElement> elements = new List<InquiryElement>();
                        foreach (Fleet fleet in _availableFleets)
                        {
                            TextObject hint;
                            bool isEnabled = Fleet.CanSetSail(fleet, out hint);

                            InquiryElement element =
                                new InquiryElement(
                                    fleet,
                                    fleet.Name.ToString(),
                                    null,
                                    isEnabled,
                                    hint.ToString()
                                );
                            elements.Add(element);
                        }

                        MBInformationManager.ShowMultiSelectionInquiry(
                            new MultiSelectionInquiryData(
                                new TextObject("{=5YaAOJ0PP2}Pick Fleet").ToString(),
                                string.Empty,
                                elements,
                                true,
                                1,
                                new TextObject("{=mer6pjy1Mn}Set sail").ToString(),
                                new TextObject("{=N0MCVBXb}Forget It").ToString(),
                                delegate (List<InquiryElement> list)
                                {
                                    Fleet fleet = list.First().Identifier as Fleet;
                                    FerryCampaignBehavior.Instance.SetDoTransfer(true);
                                    MaritimeManager.Instance.MainPartyWrapper.TakeFleetFromSettlement(fleet);
                                    PlayerEncounter.LeaveSettlement();
                                    PlayerEncounter.Finish(true);
                                },
                                null
                                )
                            );
                    }, false, -1, false);

                starter.AddGameMenuOption("ferry_travel_menu", "ferry_reject", "{=N0MCVBXb}Forget It",
                    delegate (MenuCallbackArgs args)
                    {
                        args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                        return true;
                    },
                    delegate (MenuCallbackArgs args)
                    {
                        MaritimeManager.Instance.CurrentMaritimePathTravel = null;
                        MobileParty.MainParty.Position2D = Settlement.CurrentSettlement.GatePosition;
                        PlayerEncounter.LeaveSettlement();
                        PlayerEncounter.Finish(true);
                    }, true, -1, false);
            }
        }
    }
}
