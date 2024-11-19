using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ModuleManager;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;
using TaleWorlds.Engine;
using SandBox.View.Map;
using TaleWorlds.ObjectSystem;
using TaleWorlds.Core;

using Europe_LemmyProject.General;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Overlay;
using static TaleWorlds.MountAndBlade.CompressionInfo;
using Europe_LemmyProject.MaritimeSystem.ShipManagementUI;

namespace Europe_LemmyProject.MaritimeSystem
{
    public class LandingArea : SettlementComponent
    {
        [SaveableProperty(0)]
        public Vec2 LandedShipsPoint { get; set; }

        [SaveableProperty(1)]
        public Vec2 GatePosition { get; set; }

        protected override void OnInventoryUpdated(ItemRosterElement item, int count)
        {

        }

        public override void Deserialize(MBObjectManager objectManager, XmlNode node)
        {
            base.Deserialize(objectManager, node);
            base.BackgroundCropPosition = float.Parse(node.Attributes["background_crop_position"].Value);
            base.BackgroundMeshName = node.Attributes["background_mesh"].Value;
            base.WaitMeshName = node.Attributes["wait_mesh"].Value;
            float landedX = float.Parse(node.Attributes["landed_ships_x"].Value);
            float landedY = float.Parse(node.Attributes["landed_ships_y"].Value);
            LandedShipsPoint = new Vec2(landedX, landedY);
        }


        public override void OnInit()
        {
            base.OnInit();
            GatePosition = base.Settlement.GatePosition;
        }

        /// <summary>
        /// Sets the visibility of the children of the landing area which are collected under the 
        /// child with the 'occupied_props' tag.
        /// </summary>
        /// <param name="doOccupy"></param>
        public void SetOccupied(bool doOccupy)
        {
            List<GameEntity> children = new List<GameEntity>();
            (base.Settlement.Party.Visuals as PartyVisual).StrategicEntity.GetChildrenRecursive(ref children);
            GameEntity props = children.First(x => x.HasTag("occupied_props"));
            props.SetVisibilityExcludeParents(doOccupy);
            children.Clear();
            props.GetChildrenRecursive(ref children);
            foreach (GameEntity entity in children)
            {
                entity.SetVisibilityExcludeParents(doOccupy);
            }
        }

        public void SwitchLandAndSeaPoints()
        {
            // switch the gate and landing positions
            Vec2 oldGate = Settlement.CurrentSettlement.GatePosition;
            Vec2 oldLandPos = this.LandedShipsPoint;
            var _gatePosition = base.Settlement.GetType().GetField("_gatePosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            _gatePosition.SetValue(base.Settlement, oldLandPos);
            this.LandedShipsPoint = oldGate;
        }

        public override void OnPartyEntered(MobileParty mobileParty)
        {
            if (mobileParty != null && mobileParty.IsMainParty)
            {
                // add ships to the Settlement
                if (MaritimeManager.Instance.IsPartyAtSea(mobileParty))
                {
                    //MaritimeManager.Instance.MainPartyWrapper.GiveCurrentFleetToSettlement(base.Settlement);
                    //SetOccupied(true);

                    // LandedShipsPoint is the point where the player will be teleported to
                    // MobileParty.MainParty.Position2D = this.LandedShipsPoint;

                    //SwitchLandAndSeaPoints();
                }
                else // try to take fleet from current landing area
                {
                   
                }
            }
        }

        public override void OnPartyLeft(MobileParty mobileParty)
        {

        }

    }

    public class LandingAreaCampaignBehavior : CampaignBehaviorBase
    {
        public static LandingAreaCampaignBehavior Instance { get; private set; }

        private MaritimeManager _maritimeManager;
        private bool _firstTick = false;
        private bool _doTransfer;

        public LandingAreaCampaignBehavior()
        {
            Instance = this;
        }

        [SaveableField(0)]
        private List<Settlement> _allLandingAreas;

        public List<Settlement> AllLandingAreas { get { return _allLandingAreas; } }

        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnAfterNewGameCreated));
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnAfterNewGameCreated));
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, new Action(this.OnGameLoadFinished));
            CampaignEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(this, new Action(this.OnCharacterCreationIsOver));
            CampaignEvents.TickEvent.AddNonSerializedListener(this, new Action<float>(this.OnTick));
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, new Action<MobileParty, Settlement>(this.OnSettlementLeft));

        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData<List<Settlement>>("_allLandingAreas", ref _allLandingAreas);
        }

        private void OnTick(float dt)
        {
            if (!_firstTick)
            {
                foreach(Settlement landingArea in _allLandingAreas)
                {
                    bool arePropsVisible = MobilePartyWrapper.MainPartyWrapper.GetFleetsAtSettlement(landingArea).Count > 0;
                    (landingArea.SettlementComponent as LandingArea).SetOccupied(arePropsVisible);
                }
                _firstTick = true;
            }
        }

        private void OnGameLoadFinished()
        {
            _maritimeManager = MaritimeManager.Instance;
            InitializeLandingAreas();
            this.LoadStoredShips(_allLandingAreas);
        }

        private void LoadStoredShips(List<Settlement> landingAreas)
        {
            List<Fleet> storedFleets = _maritimeManager.MainPartyWrapper.StoredFleets;
            foreach (Settlement newSettlement in landingAreas)
            {
                Fleet landedFleet = storedFleets?.FirstOrDefault(x => x.Settlement != null && x.Settlement.StringId == newSettlement.StringId);
                if (landedFleet != null)
                {
                    // if a landing area has stored ships, make is visible
                    LandingArea landingArea = newSettlement.SettlementComponent as LandingArea;
                    landingArea.SetOccupied(true);
                    landedFleet.TransferToSettlement(newSettlement);
                }
            }
        }

        private void OnCharacterCreationIsOver()
        {
            _maritimeManager = MaritimeManager.Instance;
            InitializeLandingAreas();
        }

        private void InitializeLandingAreas()
        {
            _allLandingAreas = new List<Settlement>();
            XmlReaderSettings readerSettings = new XmlReaderSettings();
            readerSettings.IgnoreComments = true;
            string path = ModuleHelper.GetModuleFullPath("Europe_LemmyProject") + "ModuleData/ELPData/LandingAreas.xml";
            using (XmlReader reader = XmlReader.Create(path, readerSettings))
            {
                XmlDocument landingAreasDoc = new XmlDocument();
                landingAreasDoc.Load(path);
                XmlNode settlementsNode = landingAreasDoc.ChildNodes[1];
                foreach (XmlNode landingAreaNode in settlementsNode)
                {
                    Settlement landingAreaSettlement = (Settlement)Campaign.Current.ObjectManager.CreateObjectFromXmlNode(landingAreaNode);
                    landingAreaSettlement.Party.Visuals.SetMapIconAsDirty();
                    landingAreaSettlement.OnGameCreated();
                    ELPHelpers.AddNameplate(landingAreaSettlement);
                    _allLandingAreas.Add(landingAreaSettlement);
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
                Settlement newLandingArea = _allLandingAreas.FirstOrDefault(x => x.StringId == Settlement.CurrentSettlement.StringId);
                if (newLandingArea != null)
                {
                    MobileParty.MainParty.CurrentSettlement = newLandingArea;
                    MobileParty.MainParty.SetMoveGoToSettlement(newLandingArea);
                    PlayerEncounter.Current.SetupFields(MobileParty.MainParty.Party, newLandingArea.Party);
                }
            }
        }

        private void OnAfterNewGameCreated(CampaignGameStarter starter)
        {
            LandingAreaGameMenu gameMenu = new LandingAreaGameMenu();
            gameMenu.InitializeMenu(starter);
        }

        private void OnSettlementLeft(MobileParty party, Settlement settlement)
        {
            LandingArea landingArea = settlement.SettlementComponent as LandingArea;
            if(landingArea != null && _doTransfer)
            {
                if (party != null && party.IsMainParty && _doTransfer)
                {
                    // then player has left with his own ships from port
                    // so spawn him at the entity of the town with the 
                    // map_port tag
                    if (MaritimeManager.Instance.IsPartyAtSea(party))
                        party.Position2D = landingArea.LandedShipsPoint;
                    else
                        party.Position2D = landingArea.GatePosition;

                    Patches.MobileParty_OnAiTick_Patch(party);
                    _doTransfer = false;
                }
                if (party != null && party.Army != null && MobileParty.MainParty.AttachedParties.Contains(party) && _doTransfer)
                {
                    if (MaritimeManager.Instance.IsPartyAtSea(party))
                        party.Position2D = landingArea.LandedShipsPoint;
                    else
                        party.Position2D = landingArea.GatePosition;

                    Patches.MobileParty_OnAiTick_Patch(party);
                }
            }
        }

        public void SetDoTransfer(bool val)
        {
            _doTransfer = val;
        }

        private class LandingAreaGameMenu
        {
            private List<Fleet> _availableFleets;
            public void InitializeMenu(CampaignGameStarter starter)
            {
                starter.AddGameMenu(
                    "landing_area_menu", 
                    "{LANDING_AREA_TEXT}",
                    new OnInitDelegate(
                        delegate (MenuCallbackArgs args)
                        {
                            _availableFleets = MobilePartyWrapper.MainPartyWrapper.GetFleetsAtSettlement(Settlement.CurrentSettlement);
                            TextObject text = TextObject.Empty;
                            if (MaritimeManager.Instance.IsPartyAtSea(MobileParty.MainParty))
                            {
                                text = new TextObject("{=rvR4uIlKFX}You come upon a suitable landing area for landing your fleet");
                            }
                            else
                            {
                                if (false)
                                    text = new TextObject("{=436PZSKkv5}You arrive at an occupied landing area");
                                else
                                    text = new TextObject("{=qWcFPy2JBs}You arrive at a vacant landing area");
                            }
                            MBTextManager.SetTextVariable("LANDING_AREA_TEXT", text);
                        }
                    ), GameOverlays.MenuOverlayType.None, GameMenu.MenuFlags.None, null);

                #region From Sea
                // player enters the landing area from sea
                starter.AddGameMenuOption(
                    "landing_area_menu", 
                    "landing_area_land_ships",
                    "{=1iUbm1nEGZ}Land your fleet",
                    delegate (MenuCallbackArgs args)
                    {
                        args.optionLeaveType = GameMenuOption.LeaveType.Continue;
                        return MaritimeManager.Instance.IsPartyAtSea(MobileParty.MainParty);
                    },
                    delegate (MenuCallbackArgs args)
                    {
                        Settlement current = Settlement.CurrentSettlement;
                        LandingArea landingArea = current.SettlementComponent as LandingArea;
                        // add ships to the Settlement
                        if (MobilePartyWrapper.MainPartyWrapper.CurrentFleet != null)
                        {
                            MaritimeManager.Instance.MainPartyWrapper.GiveCurrentFleetToSettlement(current);
                            landingArea.SetOccupied(true);
                        }


                        LandingAreaCampaignBehavior.Instance.SetDoTransfer(true);
                        PlayerEncounter.LeaveSettlement();
                        PlayerEncounter.Finish(true);
                    }, true, -1, false);

                starter.AddGameMenuOption(
                    "landing_area_menu", 
                    "landing_area_return",
                    "{=jZJQ9254Uw}Cancel the landing",
                    delegate (MenuCallbackArgs args)
                    {
                        args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                        return MaritimeManager.Instance.IsPartyAtSea(MobileParty.MainParty); ;
                    },
                    delegate (MenuCallbackArgs args)
                    {
                        LandingArea landingArea = Settlement.CurrentSettlement.SettlementComponent as LandingArea;
                    // gate position is at sea
                        MobileParty.MainParty.Position2D = Settlement.CurrentSettlement.GatePosition;
                        PlayerEncounter.LeaveSettlement();
                        PlayerEncounter.Finish(true);
                    }, true, -1, false);
                #endregion

                #region From Land
                starter.AddGameMenuOption(
                    "landing_area_menu",
                    "landing_area_manage_fleets",
                    "{=3im6aN6sSu}Manage landed fleets",
                    delegate (MenuCallbackArgs args)
                    {
                        args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                        return !MaritimeManager.Instance.IsPartyAtSea(MobileParty.MainParty) && _availableFleets.Count > 0;
                    },
                    delegate(MenuCallbackArgs args)
                    {
                        ShipManagementUIManager.Instance.Launch(_availableFleets, args);
                    }, 
                    true, -1, false);

                // player enters the landing area from land
                starter.AddGameMenuOption(
                    "landing_area_menu", 
                    "landing_area_set_sail",
                    "{=52p2YbiaVX}Set sail",
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

                        return !MaritimeManager.Instance.IsPartyAtSea(MobileParty.MainParty) && _availableFleets.Count > 0;
                    },
                    delegate (MenuCallbackArgs args)
                    {
                        // if only one fleet, then set sail with that fleet
                        if (_availableFleets.Count == 1)
                        {
                            Fleet fleet = _availableFleets.First();
                            LandingAreaCampaignBehavior.Instance.SetDoTransfer(true);
                            MaritimeManager.Instance.MainPartyWrapper.TakeFleetFromSettlement(fleet);
                            PlayerEncounter.LeaveSettlement();
                            PlayerEncounter.Finish(true);
                            return;
                        }
                        else
                        {
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
                                        LandingAreaCampaignBehavior.Instance.SetDoTransfer(true);
                                        MaritimeManager.Instance.MainPartyWrapper.TakeFleetFromSettlement(fleet);
                                        PlayerEncounter.LeaveSettlement();
                                        PlayerEncounter.Finish(true);
                                    },
                                    null
                                    )
                                );
                        }

                        /**
                        Settlement current = Settlement.CurrentSettlement;
                        LandingArea landingArea = current.SettlementComponent as LandingArea;
                        Fleet storedFleet = MobilePartyWrapper.MainPartyWrapper.GetFleetsAtSettlement(current)[0];
                        if (storedFleet == null)
                        {
                        //mobileParty.Position2D = this.LandedShipsPoint;
                            TextObject explanation = new TextObject("{=TODO}Nothing to do at a vacant landing area...");
                            MBInformationManager.AddQuickInformation(explanation, 0, Hero.MainHero.CharacterObject, "");
                        }
                        else if (MobileParty.MainParty.MemberRoster.TotalManCount <= storedFleet.TroopCapacity)
                        {
                            MobilePartyWrapper.MainPartyWrapper.TakeFleetFromSettlement(storedFleet);
                            landingArea.SetOccupied(false);

                        // LandedShipsPoint is the point where the player will be teleported to
                            MobileParty.MainParty.Position2D = landingArea.GatePosition;

                        //SwitchLandAndSeaPoints();
                        }
                        else // if party is over troop capacity of fleet
                        {
                            int diff = MobileParty.MainParty.MemberRoster.TotalManCount - storedFleet.TroopCapacity;
                            TextObject explanation = new TextObject("{=TODO}My fleet does not have enough room for {TROOP_DIFF} of my men");
                            explanation.SetTextVariable("TROOP_DIFF", diff);
                            MBInformationManager.AddQuickInformation(explanation, 0, Hero.MainHero.CharacterObject, "");
                        }

                        PlayerEncounter.LeaveSettlement();
                        PlayerEncounter.Finish(true);

                    // if player has attached parties, teleport them to the player's position
                        if (MobileParty.MainParty.AttachedParties.Count > 0)
                        {
                            foreach (MobileParty party in MobileParty.MainParty.AttachedParties)
                            {
                                party.Position2D = MobileParty.MainParty.Position2D;
                            }
                        }
                        */
                    }, true, -1, false);

                starter.AddGameMenuOption(
                    "landing_area_menu", 
                    "landing_area_return",
                    "{=NB28ZVMP4Y}Stay on land",
                    delegate (MenuCallbackArgs args)
                    {
                        args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                        return !MaritimeManager.Instance.IsPartyAtSea(MobileParty.MainParty);
                    },
                    delegate (MenuCallbackArgs args)
                    {
                        LandingArea landingArea = Settlement.CurrentSettlement.SettlementComponent as LandingArea;
                    // gate position is at sea
                        MobileParty.MainParty.Position2D = landingArea.LandedShipsPoint;
                        PlayerEncounter.LeaveSettlement();
                        PlayerEncounter.Finish(true);
                    }, true, -1, false);
                #endregion
            }
        }
    }
}
