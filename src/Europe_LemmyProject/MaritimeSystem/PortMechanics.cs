using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Overlay;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ModuleManager;
using TaleWorlds.ObjectSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;

using SandBox;
using SandBox.View.Map;

using Europe_LemmyProject.General;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using System.Reflection;
using System.Text;
using Europe_LemmyProject.MaritimeSystem.ShipManagementUI;
using static TaleWorlds.CampaignSystem.ComponentInterfaces.SettlementAccessModel;
using System.Data;

namespace Europe_LemmyProject.MaritimeSystem
{
    /// <summary>
    /// Abstract class for defining distances between port Settlement and cost to travel between them.
    /// </summary>
    public abstract class PortTravelModel : GameModel
    {
        public abstract float GetDistance(Settlement fromPort, Settlement toPort);
        public abstract Settlement GetNearestPort(Settlement originPort);
        public abstract Settlement GetNearestPort(Vec2 originPoint);
        public abstract bool IsPort(Settlement settlement);
        public abstract void InitializePortConnections();
        public abstract Tuple<Path, bool> GetPortToPortPath(Settlement port1, Settlement port2);
        public abstract int GetTravelCost(Settlement startPort, Settlement endPort, MobileParty mobileParty);
    }

    /// <summary>
    /// Implements PortTravelModel.
    /// </summary>
    public class DefaultPortTravelModel : PortTravelModel
    {
        private Dictionary<Settlement, List<Settlement>> _connectedPorts;
        public Dictionary<Settlement, List<Settlement>> ConnectedPorts
        {
            get
            {
                return _connectedPorts;
            }
        }

        public DefaultPortTravelModel()
        {
            _connectedPorts = new Dictionary<Settlement, List<Settlement>>();
        }

        public void Deserialize(XmlDocument xmlDoc)
        {
            /**
             *  <SeaRoutes>
                    <Route start="town_Xx" end="town_Yy">
                    .
                    .
                    .
                </SeaRoutes>
             */
            Scene mapScene = ((MapScene)Campaign.Current.MapSceneWrapper).Scene;
            XmlNode ports = xmlDoc.DocumentElement;
            foreach (XmlNode port in ports)
            {
                string start = port.Attributes["start"].Value;
                string end = port.Attributes["end"].Value;
                Settlement startPort = Settlement.FindFirst(x => x.StringId == start);
                Settlement endPort = Settlement.FindFirst(x => x.StringId == end);

                if (!_connectedPorts.ContainsKey(startPort))
                {
                    _connectedPorts.Add(startPort, new List<Settlement> { endPort });
                }
                else
                    _connectedPorts[startPort].Add(endPort);

                if (!_connectedPorts.ContainsKey(endPort))
                {
                    _connectedPorts.Add(endPort, new List<Settlement>() { startPort });
                }
                else
                    _connectedPorts[endPort].Add(startPort);
            }
        }

        public override void InitializePortConnections()
        {
            XmlReaderSettings readerSettings = new XmlReaderSettings();
            readerSettings.IgnoreComments = true;
            string path = ModuleHelper.GetModuleFullPath("Europe_LemmyProject") + "ModuleData/ELPData/PortConnections.xml";
            using (XmlReader reader = XmlReader.Create(path, readerSettings))
            {
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.Load(reader);
                this.Deserialize(xmlDocument);
            }
        }

        public override Tuple<Path, bool> GetPortToPortPath(Settlement port1, Settlement port2)
        {
            string possibleName1 = port1.StringId + "_" + port2.StringId + "_maritime";
            string possibleName2 = port2.StringId + "_" + port1.StringId + "_maritime";
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

        public override bool IsPort(Settlement settlement)
        {
            return _connectedPorts.ContainsKey(settlement);
        }

        public override float GetDistance(Settlement fromPort, Settlement toPort)
        {
            Path path = this.GetPortToPortPath(fromPort, toPort).Item1;
            return path.TotalDistance;
        }

        public override Settlement GetNearestPort(Settlement originSettlement)
        {
            List<Settlement> ports = _connectedPorts.Keys.ToList();
            ports.OrderBy(x => x.Position2D.Distance(originSettlement.Position2D));
            return ports[0];
        }

        public override Settlement GetNearestPort(Vec2 originPoint)
        {
            List<Settlement> ports = _connectedPorts.Keys.ToList();
            ports = ports.OrderBy(x => x.Position2D.Distance(originPoint)).ToList();
            return ports[0];
        }

        public float GetRealDistance(Settlement startPort, Settlement endPort)
        {
            return this.GetDistance(startPort, endPort) * GameMeterToRealKM;
        }

        public float GetRealDistance(Path path)
        {
            return path.TotalDistance * GameMeterToRealKM;
        }

        public int GetBaseTravelCost(float realDistance, MobileParty mobileParty, int totalPassengers, float totalWeight)
        {
            //float realDistance = this.GetRealDistance(path); // kilometers

            float cost = realDistance * totalPassengers * PassengerMile
                       + realDistance * totalWeight * FreightMile;

            //cost *= 0.1f;
            // cost = MBMath.Lerp();
            return (int)cost;
        }

        public float GetTotalDiscount(Settlement start, MobileParty mobileParty, int totalPassengers, float totalWeight)
        {
            // discount for every 50 passengers
            float fiftiesDiscount = (int)(totalPassengers / 50f) * FiftiesDiscount;
            // discount for the starting settlement belonging to your faction
            float factionDiscount = start.MapFaction == mobileParty.MapFaction ? FactionDiscount : 0f;
            // discount for being the port owner
            float portOwnerDiscount = start.OwnerClan == mobileParty.ActualClan ? PortOwnerDiscount : 0f;
            // discount for being a lord
            float lordDiscount = (mobileParty.LeaderHero.MapFaction as Kingdom) != null ? LordDiscount : 0f;
            // discount for being the leader of the port's faction
            float factionLeaderDiscount = start.MapFaction.Leader == mobileParty.LeaderHero ? FactionLeaderDiscount : 0f;
            // discount for leading an army that is the same faction as the port
            float armyPortFactionDiscount = start.MapFaction == mobileParty.MapFaction && mobileParty.Army != null ? 0.2f : 0f;
            float totalDiscount = fiftiesDiscount + factionDiscount + portOwnerDiscount + lordDiscount + factionLeaderDiscount + armyPortFactionDiscount;
            return totalDiscount;
        }

        public override int GetTravelCost(Settlement startPort, Settlement endPort, MobileParty mobileParty)
        {
            // cost = party_size + distance + cargo_weight + trade_skill + town_relation

            float realDistance = this.GetRealDistance(startPort, endPort); // kilometers
            int totalPassengers = mobileParty.MemberRoster.TotalManCount + mobileParty.PrisonRoster.TotalManCount;
            float totalWeight = mobileParty.ItemRoster.TotalWeight;

            if (mobileParty.Army != null)
            {
                foreach (MobileParty party in mobileParty.AttachedParties)
                {
                    totalPassengers += party.MemberRoster.TotalManCount + party.PrisonRoster.TotalManCount;
                    totalWeight += party.ItemRoster.TotalWeight;
                }
            }
            float cost = GetBaseTravelCost(realDistance, mobileParty, totalPassengers, totalWeight);
            float totalDiscount = GetTotalDiscount(startPort, mobileParty, totalPassengers, totalWeight);
            cost *= 0.1f;
            cost -= cost * (totalDiscount);

            // cost = MBMath.Lerp();
            return (int)cost;
        }

        public float FreightMile { get { return 0.03f; } }
        public float PassengerMile { get { return 0.05f; } }
        public float FiftiesDiscount { get { return 0.01f; } }
        public float FactionDiscount { get { return 0.1f; } }
        public float PortOwnerDiscount { get { return 0.2f; } }
        public float LordDiscount { get { return 0.1f; } }
        public float FactionLeaderDiscount { get { return 0.1f; } }
        public float GameMeterToRealKM { get { return 3.031291f; } }
    }

    /// <summary>
    /// The player interacts with this Settlement in order to get back to land via a settlement.
    /// </summary>
    public class Port : SettlementComponent
    {
        private Settlement _portOf;
        public Settlement PortOf { get { return _portOf; } }

        public IFaction MapFaction { get { return _portOf.MapFaction; } }
        protected override void OnInventoryUpdated(ItemRosterElement item, int count)
        {

        }

        public override void Deserialize(MBObjectManager objectManager, XmlNode node)
        {
            base.Deserialize(objectManager, node);
            base.BackgroundCropPosition = float.Parse(node.Attributes["background_crop_position"].Value);
            base.BackgroundMeshName = node.Attributes["background_mesh"].Value;
            base.WaitMeshName = node.Attributes["wait_mesh"].Value;
            _portOf = Settlement.Find(node.Attributes["port_of"].Value);
        }

        public override void OnPartyEntered(MobileParty mobileParty)
        {

        }

        public override void OnInit()
        {

        }
    }
    public class PortCampaignBehavior : CampaignBehaviorBase
    {
        private List<Settlement> _activePorts;
        public List<Settlement> ActivePorts { get { return _activePorts; } }

        private MaritimeManager _maritimeManager;
        private PortMenuManager _portMenuManager;

        private bool _leaveToPort;
        public DefaultPortTravelModel PortTravelModel
        {
            get
            {
                return SandBoxManager.Instance.GameStarter.Models.FirstOrDefault(x => x is PortTravelModel) as DefaultPortTravelModel;
            }
        }

        public static PortCampaignBehavior Instance
        {
            get
            {
                return Campaign.Current.GetCampaignBehavior<PortCampaignBehavior>();
            }
        }

        public List<Settlement> PortTowns
        {
            get
            {
                return this.PortTravelModel.ConnectedPorts.Keys.ToList();
            }
        }

        public static int CoastLineNavmeshID = 15;
        //private CampaignShipTemplate _buySelectedTemplate;

        public override void RegisterEvents()
        {
            //CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnAfterNewGameCreated));
            //CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnAfterNewGameCreated));
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, new Action<MobileParty, Settlement, Hero>(this.OnSettlementEntered));
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, new Action<MobileParty, Settlement>(this.OnSettlementLeft));
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnGameLoaded));
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnNewGameCreated));
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, new Action(this.OnGameLoadFinished));
            CampaignEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(this, new Action(this.OnCharacterCreationIsOver));
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, new Action<Settlement, bool, Hero, Hero, Hero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail>(this.OnSettlementOwnerChanged));
            CampaignEvents.TickEvent.AddNonSerializedListener(this, new Action<float>(this.OnTick));
            CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnAfterSessionLaunched));
        }

        public override void SyncData(IDataStore dataStore)
        {
            //_maritimeManager.SyncData(dataStore);
        }

        public PortCampaignBehavior()
        {
            _activePorts = new List<Settlement>() { };
        }

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            PortTravelModel.InitializePortConnections();
            _maritimeManager = MaritimeManager.Instance;
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            PortTravelModel.InitializePortConnections();
            _maritimeManager = MaritimeManager.Instance;
        }

        private void OnTick(float dt)
        {
            // ensures that the player goes straight from a Port to is associated settlement
            if (Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.SettlementComponent is Port)
            {
                Port port = Settlement.CurrentSettlement.SettlementComponent as Port;
                PlayerEncounter.LeaveSettlement();
                PlayerEncounter.Finish(true);
                SettlementAccessModel.AccessDetails accessDetails;
                Campaign.Current.Models.SettlementAccessModel.CanMainHeroEnterSettlement(port.PortOf, out accessDetails);
                if (port.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
                {
                    MBInformationManager.AddQuickInformation(new TextObject("{=QElCv2DXcP}The enemy surely won't allow me to land here peacfully. I should look for a suitable landing area."), 0, Hero.MainHero.CharacterObject);
                }
                else if (port.PortOf.IsUnderSiege)
                {
                    MBInformationManager.AddQuickInformation(new TextObject("{=ggYEnbhls1}I doubt the besiegers will allow me to cross their lines. Let us turn back."), 0, Hero.MainHero.CharacterObject);
                }
                else if (port.PortOf.Owner != Hero.MainHero && accessDetails.AccessLimitationReason == SettlementAccessModel.AccessLimitationReason.CrimeRating)
                {
                    MBInformationManager.AddQuickInformation(new TextObject("{=cArNEYb9I2}One of your men reminds you that you are a wanted criminal here. Bet to turn back for now."), 0);
                }
                else
                {
                    EncounterManager.StartSettlementEncounter(MobileParty.MainParty, port.PortOf);
                }
            }
        }

        private void OnGameLoadFinished()
        {
            InitializePorts();
            //_maritimeManager.MainPartyWrapper.LoadLandedShips(_activePorts);
        }

        private void OnCharacterCreationIsOver()
        {
            InitializePorts();
        }

        public void InitializePorts()
        {
            _activePorts = new List<Settlement>();
            XmlReaderSettings readerSettings = new XmlReaderSettings();
            readerSettings.IgnoreComments = true;
            string path = ModuleHelper.GetModuleFullPath("Europe_LemmyProject") + "ModuleData/ELPData/Ports.xml";
            using (XmlReader reader = XmlReader.Create(path, readerSettings))
            {
                XmlDocument portsDoc = new XmlDocument();
                portsDoc.Load(reader);
                XmlNode settlementsNode = portsDoc.ChildNodes[1];
                foreach (XmlNode portNode in settlementsNode)
                {
                    Settlement port = (Settlement)Campaign.Current.ObjectManager.CreateObjectFromXmlNode(portNode);
                    port.Party.Visuals.SetMapIconAsDirty();
                    port.OnGameCreated();
                    //port.IsVisible = false;
                    ELPHelpers.AddNameplate(port, 2);
                    _activePorts.Add(port);
                }
            }
        }

        public void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
        {
            if (IsPortSettlement(settlement) && party != null && party.IsMainParty)
            {
                if (MobilePartyWrapper.MainPartyWrapper.CurrentFleet != null && 
                    MobilePartyWrapper.MainPartyWrapper.CurrentFleet.Owner == Hero.MainHero &&
                    MobilePartyWrapper.MainPartyWrapper.CurrentFleet.Ships.Count > 0)
                    MobilePartyWrapper.MainPartyWrapper.GiveCurrentFleetToSettlement(settlement);
            }
        }

        private void OnSettlementLeft(MobileParty party, Settlement settlement)
        {
            // if an army leaves a town (idk about other settlements), at least one member party
            // will have its visuals stuck at the town gate
            // adding a small vector to each party's position will unstick that party's visuals
            if (party != null && party.Army != null && party.Army.LeaderParty != party)
            {
                party.Position2D += new Vec2(0.05f, 0.05f);
            }

            if (party != null && party.IsMainParty && _leaveToPort)
            {
                // then player has left with his own ships from port
                // so spawn him at the entity of the town with the 
                // map_port tag
                GameEntity strat = ((PartyVisual)settlement.Party.Visuals).StrategicEntity;
                List<GameEntity> children = new List<GameEntity>();
                strat.GetChildrenRecursive(ref children);
                GameEntity port = children.FirstOrDefault(x => x.HasTag("map_port"));
                party.Position2D = port.GlobalPosition.AsVec2;
                Patches.MobileParty_OnAiTick_Patch(party);
                _leaveToPort = false;
            }
            if (party != null && party.Army != null && MobileParty.MainParty.AttachedParties.Contains(party) && _leaveToPort)
            {
                GameEntity strat = ((PartyVisual)settlement.Party.Visuals).StrategicEntity;
                List<GameEntity> children = new List<GameEntity>();
                strat.GetChildrenRecursive(ref children);
                GameEntity port = children.FirstOrDefault(x => x.HasTag("map_port"));
                party.Position2D = port.GlobalPosition.AsVec2;
                Patches.MobileParty_OnAiTick_Patch(party);
            }
        }

        private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            Settlement port = _activePorts.FirstOrDefault(x => (x.SettlementComponent is Port) && ((Port)x.SettlementComponent).PortOf == settlement);
            if (port != null )//&&  newOwner != null)
            {
                ChangeOwnerOfSettlementAction.ApplyByDefault(newOwner, port);
                port.Party.Visuals.SetMapIconAsDirty();
            }
        }

        public static bool IsPortSettlement(Settlement settlement)
        {
            return Instance.PortTravelModel.ConnectedPorts.ContainsKey(settlement);
        }
        public static List<Settlement> GetTravelablePortSettlements(Settlement settlement)
        {
            return new List<Settlement>(Instance.PortTravelModel.ConnectedPorts[settlement]);
        }

        public void OnAfterSessionLaunched(CampaignGameStarter starter)
        {
            this._portMenuManager = new PortMenuManager(_maritimeManager);
            this._portMenuManager.InitializeMenus(starter);
        }

        public void SetLeaveToPort(bool leaveToPort)
        {
            _leaveToPort = leaveToPort;
        }
    }

    public class PortMenuManager
    {
        private MaritimeManager _maritimeManager;
        private List<Settlement> _currentPortDestinations;
        private List<Fleet> _availableFleets;
        private Fleet _managedFleet;
        private MaritimePathTravel _travelBuilder;
        private CampaignShipTemplate _buySelectedTemplate;
        private CampaignShip _managedShip;
        private List<CampaignShipTemplate> _purchaseableShipTemplates;

        public PortMenuManager (MaritimeManager maritimeManager)
        {
            this._maritimeManager = maritimeManager;
        }
        
        public void InitializeMenus(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption(
                "town", 
                "go_to_port", 
                "{=RlXjIo4mKS}Go to the port",
                delegate (MenuCallbackArgs args)
                {
                    //args.Tooltip = new TextObject("Meet with the Harbormaster");
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    return PortCampaignBehavior.IsPortSettlement(Settlement.CurrentSettlement);
                },
                delegate (MenuCallbackArgs args) { GameMenu.SwitchToMenu("town_port"); },
                false, 5, false);

            #region port menu
            // root port menu
            starter.AddGameMenu(
                "town_port", 
                "{=ineEV8sgAq}The port is bustling with sailors and ships.", 
                delegate (MenuCallbackArgs args)
                {
                    _currentPortDestinations = PortCampaignBehavior.GetTravelablePortSettlements(Settlement.CurrentSettlement);
                    _currentPortDestinations = _currentPortDestinations.OrderBy(x => PortCampaignBehavior.Instance.PortTravelModel.GetTravelCost(Settlement.CurrentSettlement, x, MobileParty.MainParty)).ToList();
                    _availableFleets = MobilePartyWrapper.MainPartyWrapper.StoredFleets.Where(x => x.Settlement == Settlement.CurrentSettlement).ToList();
                    _managedFleet = MaritimeManager.Instance.MainPartyWrapper.StoredFleets?.FirstOrDefault(x => x.Settlement == Settlement.CurrentSettlement);
                },
                GameOverlays.MenuOverlayType.SettlementWithBoth, GameMenu.MenuFlags.None, null);

            starter.AddGameMenuOption(
                "town_port", 
                "talk_to_harbormaster", 
                "{=HPurm5Munl}Talk to the harbormaster.", 
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
                    args.Tooltip = new TextObject("{=DwQyzuvmiP}Ask about paying for passage to another port.");
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    GameMenu.SwitchToMenu("hired_ships_select_destination");
                },
                false, -1, false);

            starter.AddGameMenuOption(
                "town_port", 
                "talk_to_shipwright", 
                "{=jH5jv95pCU}Talk to the shipwright.", 
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
                    args.Tooltip = new TextObject("{=ujX52u7JMg}Buy, sell and build ships.");
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    GameMenu.SwitchToMenu("shipwright");
                },
                false, -1, false);

            
            starter.AddGameMenuOption(
                "town_port", 
                "port_go_to_select_fleet", 
                "{=Y53KNYuZYL}Manage docked fleets", 
                delegate (MenuCallbackArgs args)
                {
                    if (_availableFleets.Count == 1)
                    {
                        args.Tooltip = new TextObject("{=H2O554VLoL}Manage your fleet at this port");
                    }
                    else if (_availableFleets.Count > 1)
                    {
                        TextObject text = new TextObject("{=IdUeyLfjDL}Manage your {NUM_FLEETS} fleets at this port");
                        text.SetTextVariable("NUM_FLEETS", _availableFleets.Count);
                        args.Tooltip = text;
                    }
                    else
                    {
                        args.Tooltip = new TextObject("{=StqsEz5JgS}You have no fleets at this port");
                        args.IsEnabled = false;
                    }
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    ShipManagementUIManager.Instance.Launch(_availableFleets, args);

                    // GameMenu.SwitchToMenu("port_select_fleet");
                },
                false, -1, false);

            starter.AddGameMenuOption(
                "town_port", 
                "set_sail_cheat", 
                "{=izhtC9ysv9}[Cheat] Set sail from port.", 
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return Game.Current.CheatMode;
                },
                delegate (MenuCallbackArgs args)
                {
                    PortCampaignBehavior.Instance.SetLeaveToPort(true);

                    PlayerEncounter.LeaveSettlement();
                    PlayerEncounter.Finish(true);
                },
                false, -1, false);

            starter.AddGameMenuOption(
                "town_port", 
                "set_sail", 
                "{=mer6pjy1Mn}Set sail", 
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;

                    if (_availableFleets.Count == 0)
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("{=RDav1vM63u}You have no fleets at this port to set sail with.");
                    }
                    else if (_availableFleets.Count == 1)
                    {
                        TextObject explanation;
                        if(!Fleet.CanSetSail(_availableFleets[0], out explanation))
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

                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    // if only one fleet, then set sail with that fleet
                    if (_availableFleets.Count == 1)
                    {
                        Fleet fleet = _availableFleets.First();
                        PortCampaignBehavior.Instance.SetLeaveToPort(true);
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
                                PortCampaignBehavior.Instance.SetLeaveToPort(true);
                                MaritimeManager.Instance.MainPartyWrapper.TakeFleetFromSettlement(fleet);
                                PlayerEncounter.LeaveSettlement();
                                PlayerEncounter.Finish(true);
                            },
                            null
                            )
                        );
                },
                false, -1, false);

            starter.AddGameMenu(
                "set_sail_select_fleet", 
                "{=j89pN06eqg}Choose fleet", 
                delegate (MenuCallbackArgs args)
                {
                
                },
                GameOverlays.MenuOverlayType.SettlementWithBoth, GameMenu.MenuFlags.None, null);

            starter.AddGameMenuOption(
                "town_port", 
                "town_port_return",
                "{=qWAmxyYz}Back to town center", 
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    GameMenu.SwitchToMenu("town");
                },
                true, -1, false);
            #endregion

            #region Port Travel Menu
            // menu for traveling to different ports
            starter.AddGameMenu(
                "harbor_master", 
                "{=xwMAo1cVZP}You meet with the harbormaster who presents you with the following options for travelling between ports.", 
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
                },
                GameOverlays.MenuOverlayType.SettlementWithBoth, GameMenu.MenuFlags.None, null);

            starter.AddGameMenuOption(
                "harbor_master", 
                "hire_ships_for_oneway_passage", 
                "{=rrLe6CwRLo}Hire ships for a one-way passage.", 
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    GameMenu.SwitchToMenu("hired_ships_select_destination");
                },
                false, -1, false);

            // return to port menu
            starter.AddGameMenuOption(
                "harbor_master", 
                "return_to_town",
                "{=INfOcDqf7l}Back to the port", 
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    GameMenu.SwitchToMenu("town_port");
                },
                true, -1, false);

            #region Select Port Destination
            starter.AddGameMenu(
                "hired_ships_select_destination", 
                "{=wZLwMFuFCu}Select your destination. Prices are based on party size, amount of cargo, relation with the town, and, of course, distance to the destination.", 
                delegate (MenuCallbackArgs args)
                {
                    // args.MenuTitle = new TextObject();
                    _travelBuilder = new MaritimePathTravel();
                    if(_currentPortDestinations.Count > 0)
                        args.MenuContext.SetRepeatObjectList(_currentPortDestinations);
                },
                GameOverlays.MenuOverlayType.SettlementWithBoth, GameMenu.MenuFlags.None, null);

            starter.AddGameMenuOption(
                "hired_ships_select_destination",
                "hired_passage",
                "{=!}{PORT_DESTINATION}",
                delegate (MenuCallbackArgs args)
                {
                    Settlement settlement = args.MenuContext.GetCurrentRepeatableObject() as Settlement;
                    if (settlement != null)
                    {
                        TextObject text = new TextObject("{SETTLEMENT_NAME} ({TRAVEL_COST}{GOLD_ICON})");
                        text.SetTextVariable("SETTLEMENT_NAME", settlement.Name);
                        int cost = PortCampaignBehavior.Instance.PortTravelModel.GetTravelCost(Settlement.CurrentSettlement, settlement, MobileParty.MainParty);
                        text.SetTextVariable("TRAVEL_COST", cost);
                        MBTextManager.SetTextVariable("PORT_DESTINATION", text);
                        args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    }
                    else
                    {
                        MBTextManager.SetTextVariable("PORT_DESTINATION", "***ERROR in hired_ships_select_destination***");
                    }
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    Settlement settlement = args.MenuContext.GetSelectedRepeatableObject() as Settlement;

                    _travelBuilder.ToSettlement = settlement;
                    _travelBuilder.FromSettlement = Settlement.CurrentSettlement;
                    _travelBuilder.TravelCost = PortCampaignBehavior.Instance.PortTravelModel.GetTravelCost(_travelBuilder.ToSettlement, _travelBuilder.FromSettlement, MobileParty.MainParty);
                    GameMenu.SwitchToMenu("travel_to_selected_port");
                }, false, -1, true);
            #endregion

            starter.AddGameMenuOption(
                "hired_ships_select_destination", 
                "hired_passage_nevermind",
                "{=INfOcDqf7l}Back to the port", 
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    _travelBuilder = null;
                    GameMenu.SwitchToMenu("town_port");
                }, 
                true, -1, false);

            // travel to port confirmation
            starter.AddGameMenu("travel_to_selected_port", "{=7pNR2Xuwrt}Travel to {TRAVEL_DEST} for {TRAVEL_COST}{GOLD_ICON}?",
                delegate (MenuCallbackArgs args)
                {
                    MBTextManager.SetTextVariable("TRAVEL_COST", _travelBuilder.TravelCost);
                    MBTextManager.SetTextVariable("TRAVEL_DEST", _travelBuilder.ToSettlement.Name);
                }, GameOverlays.MenuOverlayType.SettlementWithBoth, GameMenu.MenuFlags.None, null);

            starter.AddGameMenuOption(
                "travel_to_selected_port", 
                "travel_to_port_affirmative", 
                "{=aeouhelq}Yes", 
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Continue;
                    
                    Settlement settlement = _travelBuilder.ToSettlement;

                    SettlementAccessModel.AccessDetails accessDetails;
                    Campaign.Current.Models.SettlementAccessModel.CanMainHeroEnterSettlement(settlement, out accessDetails);

                    MBStringBuilder sb = default(MBStringBuilder);
                    sb.Initialize();

                    if (settlement.MapFaction.IsAtWarWith(Settlement.CurrentSettlement.MapFaction))
                    {
                        sb.AppendLine(new TextObject("{=EGq8HfN5hU}No one here is willing to sail to a hostile port"));
                    }
                    if (settlement.IsUnderSiege)
                    {
                        sb.AppendLine(new TextObject("{=8Iin6bXmiH}No one here is willing to sail to a town that is under siege"));
                    }
                    if (accessDetails.AccessLevel == (SettlementAccessModel.AccessLevel.NoAccess | SettlementAccessModel.AccessLevel.LimitedAccess))
                    {
                        sb.AppendLine(new TextObject("{=zXjHzHUpxo}You are prohibited entry to this destination"));
                    }
                    if (Hero.MainHero.Gold < _travelBuilder.TravelCost)
                    {
                        sb.AppendLine(new TextObject("{=eKpE17JjB3}Not enough gold", null));
                    }
                    if (sb.Length > 0)
                        args.IsEnabled = false;

                    args.Tooltip = new TextObject(sb.ToStringAndRelease());

                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    // subtract money, exit town and start travel to other port
                    Tuple<Path, bool> activePathTuple = PortCampaignBehavior.Instance.PortTravelModel.GetPortToPortPath(_travelBuilder.FromSettlement, _travelBuilder.ToSettlement);
                    _travelBuilder.PathTracker = new ReversiblePathTracker(activePathTuple.Item1, MobileParty.MainParty.Party.Visuals.GetFrame().GetScale(), activePathTuple.Item2);
                    //Hero.MainHero.ChangeHeroGold(-_maritimeManager._currentMaritimePathTravel.TravelCost);
                    GiveGoldAction.ApplyBetweenCharacters(MobileParty.MainParty.LeaderHero, null, _travelBuilder.TravelCost, false);
                    _maritimeManager.SetMartitimeTravel(_travelBuilder,
                        delegate ()
                        {
                            Settlement fromSettlement = _maritimeManager.CurrentMaritimePathTravel.ToSettlement;
                            Settlement toSettlement = _maritimeManager.CurrentMaritimePathTravel.ToSettlement;
                            AccessDetails accessDetails;
                            Campaign.Current.Models.SettlementAccessModel.CanMainHeroEnterSettlement(fromSettlement, out accessDetails);
                            bool inaccessble = fromSettlement.MapFaction.IsAtWarWith(toSettlement.MapFaction)
                                                || fromSettlement.IsUnderSiege
                                                || accessDetails.AccessLevel == (AccessLevel.NoAccess | AccessLevel.LimitedAccess);

                            MaritimeManager.Instance.RemoveFromSea(MobileParty.MainParty);
                            if (inaccessble)
                            {
                                Vec2 pos = MaritimeManager.Instance.CurrentMaritimePathTravel.ToSettlement.GatePosition;
                                MobileParty.MainParty.Position2D = pos;
                                MobileParty.MainParty.SetMoveModeHold();
                                Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
                            }
                            else
                            {
                                EncounterManager.StartSettlementEncounter(MobileParty.MainParty, fromSettlement);
                            }
                            _maritimeManager.CurrentMaritimePathTravel = null;
                        });
                    PlayerEncounter.LeaveSettlement();
                    PlayerEncounter.Finish(true);
                }, 
                false, -1, false);

            starter.AddGameMenuOption(
                "travel_to_selected_port",
                "travelt_to_port_negative", 
                "{=8OkPHu4f}No", 
                delegate (MenuCallbackArgs args)
                {
                    args.Tooltip = new TextObject("{=ivXfiOske9}Return to destination selection", null);
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    GameMenu.SwitchToMenu("hired_ships_select_destination");
                }, 
                true, -1, false);
            #endregion

            #region Shipwright Menu
            starter.AddGameMenu(
                "shipwright", 
                "{=KQWVBQ8oE3}You are surrounded by the sights and sounds of the ship-making process.", 
                delegate (MenuCallbackArgs args)
                {
                    _purchaseableShipTemplates = CampaignShipTemplate.AllShipTemplates.Where(x =>
                        x.Cultures.Contains(Settlement.CurrentSettlement.Culture)).ToList();
                    CultureObject neutralCulture = MBObjectManager.Instance.GetObjectTypeList<CultureObject>().FirstOrDefault(x => x.StringId == "neutral_culture");
                    if (neutralCulture != null)
                        _purchaseableShipTemplates.AppendList(CampaignShipTemplate.AllShipTemplates.Where(x => x.Cultures.Contains(neutralCulture)).ToList());

                    _purchaseableShipTemplates = _purchaseableShipTemplates.OrderBy(x => x.BaseCost).ToList();
                },
                GameOverlays.MenuOverlayType.SettlementWithBoth, GameMenu.MenuFlags.None, null);

            starter.AddGameMenuOption(
                "shipwright", 
                "shipwright_add_ship_cheat", 
                "{=RCrK6j86cV}[Cheat] Add random ship.", 
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    return Game.Current.CheatMode;
                },
                delegate (MenuCallbackArgs args)
                {
                    CampaignShip random = new CampaignShip(CampaignShipTemplate.AllShipTemplates.GetRandomElement());
                    _managedShip = random;
                    GameMenu.SwitchToMenu("new_ship_pick_fleet");
                },
                false, -1, false);

            starter.AddGameMenuOption(
                "shipwright", 
                "shipwright_buy_ships", 
                "See the ships for sale", 
                delegate (MenuCallbackArgs args)
                {
                    // args.Tooltip = new TextObject("{=5b7r9SOqSQ}See the ships that are available for purchase.");
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    GameMenu.SwitchToMenu("ship_merchant");
                });

            #region Buy Ships
            starter.AddGameMenu(
                "ship_merchant", 
                "{=LlfjyuSnsn}Here are the ships for sale in the harbor today.", 
                delegate (MenuCallbackArgs args)
                {
                    if (_purchaseableShipTemplates.Count > 0)
                        args.MenuContext.SetRepeatObjectList(_purchaseableShipTemplates);
                },
                GameOverlays.MenuOverlayType.SettlementWithBoth, GameMenu.MenuFlags.None, null);

            starter.AddGameMenuOption("ship_merchant", "purchasable_ship", "{PURCHASEABLE_SHIP_ITEM}",
                delegate (MenuCallbackArgs args)
                {
                    CampaignShipTemplate shipTemplate = args.MenuContext.GetCurrentRepeatableObject() as CampaignShipTemplate;
                    if (shipTemplate != null)
                    {
                        TextObject text = new TextObject("{PURCHASABLE_SHIP} ({SHIP_COST}{GOLD_ICON})");
                        text.SetTextVariable("PURCHASABLE_SHIP", shipTemplate.BaseName);
                        text.SetTextVariable("SHIP_COST", shipTemplate.BaseCost);
                        MBTextManager.SetTextVariable("PURCHASEABLE_SHIP_ITEM", text.ToString());
                        args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    }
                    else
                    {
                        MBTextManager.SetTextVariable("PURCHASEABLE_SHIP_ITEM", "***ERROR _buySelectedTemplate is null in purchasable_ship***");
                    }
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    _buySelectedTemplate = args.MenuContext.GetSelectedRepeatableObject() as CampaignShipTemplate;
                    GameMenu.SwitchToMenu("confirm_buy_ship");
                }, false, -1, true);
            
            starter.AddGameMenuOption(
                "ship_merchant",
                "purchasable_ship_return",
                "{=N0MCVBXb}Forget It",
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    GameMenu.SwitchToMenu("shipwright");
                }, 
                true, -1, false);
            #endregion

            starter.AddGameMenu(
                "confirm_buy_ship", 
                "{SELECTED_SHIP_TEXT}",
                delegate (MenuCallbackArgs args)
                {
                    MBStringBuilder sb = default(MBStringBuilder);
                    sb.Initialize();

                    TextObject shipClass = new TextObject("{LABEL1}: {SHIP_CLASS}");
                    TextObject label1 = new TextObject("{=8hc9mPS9Mr}Class");
                    shipClass.SetTextVariable("LABEL1", label1);
                    shipClass.SetTextVariable("SHIP_CLASS", _buySelectedTemplate.BaseName);
                    sb.AppendLine(shipClass);

                    TextObject shipCrew = new TextObject("{LABEL2}: {SHIP_CREW_CAPACITY}");
                    TextObject label2 = new TextObject("{=CFL4v1v3XV}Crew Capacity");
                    shipCrew.SetTextVariable("LABEL2", label2);
                    shipCrew.SetTextVariable("SHIP_CREW_CAPACITY", _buySelectedTemplate.BaseCrewCapacity);
                    sb.AppendLine(shipCrew);

                    TextObject shipTroop = new TextObject("{LABEL3}: {SHIP_TROOP_CAPACITY}");
                    TextObject label3 = new TextObject("{=vwNi091pvN}Troop Capacity");
                    shipTroop.SetTextVariable("LABEL3", label3);
                    shipTroop.SetTextVariable("SHIP_TROOP_CAPACITY", _buySelectedTemplate.BaseTroopCapacity);
                    sb.AppendLine(shipTroop);

                    TextObject shipCargo = new TextObject("{LABEL4}: {SHIP_CARGO_CAPACITY}");
                    TextObject label4 = new TextObject("{=nZdnsiiv3y}Cargo Capacity");
                    shipCargo.SetTextVariable("LABEL4", label4);
                    shipCargo.SetTextVariable("SHIP_CARGO_CAPACITY", _buySelectedTemplate.BaseCargoCapacity);
                    sb.AppendLine(shipCargo);

                    TextObject shipSpeed = new TextObject("{LABEL5}: {SHIP_SPEED}");
                    TextObject label5 = new TextObject("{=6GSXsdeX}Speed");
                    shipSpeed.SetTextVariable("LABEL5", label5);
                    shipSpeed.SetTextVariable("SHIP_SPEED", _buySelectedTemplate.BaseSpeed.ToString("0.00"));
                    sb.AppendLine(shipSpeed);

                    //TextObject shipCost = new TextObject("{LABEL6}: {SHIP_COST}{GOLD_ICON}");
                    //TextObject label6 = new TextObject("{=Nsz666Od5K}Cost");
                    //shipCost.SetTextVariable("LABEL6", label6);
                    //shipCost.SetTextVariable("SHIP_COST", _buySelectedTemplate.BaseCost);
                    //sb.AppendLine(shipCost);

                    TextObject shipBuyConfirm = new TextObject("{=juNTiIthcH}Confirm purchase of {SHIP_CLASS} for {SHIP_COST}{GOLD_ICON}?");
                    shipBuyConfirm.SetTextVariable("SHIP_COST", _buySelectedTemplate.BaseCost);
                    shipBuyConfirm.SetTextVariable("SHIP_CLASS", _buySelectedTemplate.BaseName);
                    sb.AppendLine(shipBuyConfirm);

                    MBTextManager.SetTextVariable("SELECTED_SHIP_TEXT", sb.ToStringAndRelease());
                },
                GameOverlays.MenuOverlayType.SettlementWithBoth, GameMenu.MenuFlags.None, null);

            starter.AddGameMenuOption(
                "confirm_buy_ship", 
                "buy_ship_return",
                "{=XqSmBEPlJm}Yes. Another fine addition to the fleet.",
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    if (Hero.MainHero.Gold < _buySelectedTemplate.BaseCost)
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("{=8zmfY3TjZm}Looks like I don't have enough gold");
                    }
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    CampaignShip newShip = new CampaignShip(_buySelectedTemplate);
                    GiveGoldAction.ApplyBetweenCharacters(MobileParty.MainParty.LeaderHero, null, (int)(newShip.Cost), false);
                    _managedShip = newShip;

                    GameMenu.SwitchToMenu("new_ship_pick_fleet");
                });

            starter.AddGameMenuOption(
                "confirm_buy_ship", 
                "buy_ship_return",
                "{=N0MCVBXb}Forget It",
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    GameMenu.SwitchToMenu("ship_merchant");
                });

            starter.AddGameMenu(
                "new_ship_pick_fleet",
                "{=gcymi6BlCj}Which fleet shall we send her to?", 
                delegate(MenuCallbackArgs args) 
                {
                    args.MenuContext.SetRepeatObjectList(_availableFleets);
                },
                GameOverlays.MenuOverlayType.SettlementWithBoth, GameMenu.MenuFlags.None, null);

            starter.AddGameMenuOption(
                "new_ship_pick_fleet",
                "new_ship_pick_fleet_item",
                "{FLEET_NAME}",
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Continue;
                    Fleet fleet = args.MenuContext.GetCurrentRepeatableObject() as Fleet;
                    if(fleet != null)
                    {
                        MBTextManager.SetTextVariable("FLEET_NAME", fleet.Name);
                        return true;
                    }
                    return false;
                },
                delegate (MenuCallbackArgs args)
                {
                    Fleet fleet = args.MenuContext.GetSelectedRepeatableObject() as Fleet;
                    fleet.AddShip(_managedShip);
                    GameMenu.SwitchToMenu("ship_merchant");
                }, false, -1, true);

            starter.AddGameMenuOption(
                "new_ship_pick_fleet", 
                "new_ship_create_new_fleet", 
                "{=hFSVMu1Ut9}Create a new fleet", 
                delegate (MenuCallbackArgs args) 
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    GameTexts.SetVariable("MAX_LETTER_COUNT", CampaignShip.ShipNameCharLimit);
                    GameTexts.SetVariable("MIN_LETTER_COUNT", 1);
                    InformationManager.ShowTextInquiry(
                        new TextInquiryData(
                            new TextObject("{=jcShFM9hoS}Fleet Name").ToString(), 
                            string.Empty, true, true, 
                            GameTexts.FindText("str_done", null).ToString(), 
                            GameTexts.FindText("str_cancel", null).ToString(),
                            delegate (string str)
                            {
                                MobilePartyWrapper.MainPartyWrapper.CreateNewFleet(new List<CampaignShip>() { _managedShip }, Settlement.CurrentSettlement, out _managedFleet);
                                _managedFleet.Name = new TextObject(str);
                                _availableFleets.Add(_managedFleet);
                                GameMenu.SwitchToMenu("ship_merchant");
                            },
                            null, 
                            false, CampaignShip.IsShipNameValid, "", _managedShip.Name.ToString()));
                },
                false, -1, false);

            starter.AddGameMenuOption(
                "shipwright", 
                "shipwright_return",
                "{=INfOcDqf7l}Back to the port",
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    GameMenu.SwitchToMenu("town_port");
                },
                true, -1, false);
            #endregion

            #region Select Fleet Menu
            starter.AddGameMenu(
                "port_select_fleet",
                "{=SnTAwIcMUi}Your docked fleets",
                delegate (MenuCallbackArgs args) 
                {
                    args.MenuContext.SetRepeatObjectList(_availableFleets);
                }, 
                GameOverlays.MenuOverlayType.SettlementWithBoth, GameMenu.MenuFlags.None, null);

            starter.AddGameMenuOption(
                "port_select_fleet",
                "port_select_fleet_item",
                "{FLEET_NAME}",
                delegate (MenuCallbackArgs args)
                {
                    Fleet fleet = args.MenuContext.GetCurrentRepeatableObject() as Fleet;
                    MBTextManager.SetTextVariable("FLEET_NAME", fleet.Name);
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    return true;
                },
                delegate(MenuCallbackArgs args)
                {
                    _managedFleet = args.MenuContext.GetSelectedRepeatableObject() as Fleet;
                    GameMenu.SwitchToMenu("port_manage_single_fleet");
                },
                false, -1, true);

            starter.AddGameMenuOption(
                "port_select_fleet", 
                "manage_fleets_return",
                "{=INfOcDqf7l}Back to the port",
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    GameMenu.SwitchToMenu("town_port");
                },
                true, -1, false);

            #endregion

            #region Manage Fleet Menu
            starter.AddGameMenu(
                "port_manage_single_fleet", 
                "{MANAGE_SINGLE_FLEET_TEXT}", 
                delegate(MenuCallbackArgs args)
                {
                    MBStringBuilder sb = Fleet.GetFleetInfo(_managedFleet, "port_manage_single_fleet");

                    MBTextManager.SetTextVariable("MANAGE_SINGLE_FLEET_TEXT", sb.ToStringAndRelease());
                },
                GameOverlays.MenuOverlayType.SettlementWithBoth, GameMenu.MenuFlags.None, null);

            starter.AddGameMenuOption(
                "port_manage_single_fleet",
                "port_rename_fleet",
                "{=qco1qQSk3b}Rename",
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Continue;
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    GameTexts.SetVariable("MAX_LETTER_COUNT", CampaignShip.ShipNameCharLimit);
                    GameTexts.SetVariable("MIN_LETTER_COUNT", 1);
                    InformationManager.ShowTextInquiry(
                        new TextInquiryData(
                            new TextObject("{=jcShFM9hoS}Fleet Name").ToString(), string.Empty, true, true, 
                            GameTexts.FindText("str_done", null).ToString(), 
                            GameTexts.FindText("str_cancel", null).ToString(),
                            delegate (string str)
                            {
                                _managedFleet.Name = new TextObject(str);
                                MBTextManager.SetTextVariable("MANAGED_SHIP_NAME", _managedFleet.Name);
                                args.MenuContext.Refresh();
                            }, null, false, CampaignShip.IsShipNameValid, "", _managedFleet.Name.ToString())
                        );
                }, false, -1, false);

            starter.AddGameMenuOption(
                "port_manage_single_fleet",
                "port_fleet_manage_ships",
                "{=Dj96iVLElL}Manage the ships",
                delegate (MenuCallbackArgs args) 
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    return true; 
                },
                delegate (MenuCallbackArgs args)
                {
                    GameMenu.SwitchToMenu("port_select_ship");
                }, false, -1, false);

            starter.AddGameMenuOption(
                "port_manage_single_fleet",
                "manage_fleets_return",
                "{=UgIDUQ4o94}Return to select fleet",
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    GameMenu.SwitchToMenu("port_select_fleet");
                },
                false, -1, false);
            #endregion

            #region Select Ship Menu
            starter.AddGameMenu(
                "port_select_ship",
                "{=V24R3feGz9}The ships in your fleet {FLEET_NAME}",
                delegate (MenuCallbackArgs args)
                {
                    MBTextManager.SetTextVariable("FLEET_NAME", _managedFleet.Name);
                    List<CampaignShip> availableShips = _managedFleet.Ships;
                    args.MenuContext.SetRepeatObjectList(availableShips);
                },
                GameOverlays.MenuOverlayType.SettlementWithBoth, GameMenu.MenuFlags.None, null);

            starter.AddGameMenuOption(
                "port_select_ship",
                "port_select_ship_item",
                "{SHIP_NAME}",
                delegate(MenuCallbackArgs args)
                {
                    CampaignShip ship = args.MenuContext.GetCurrentRepeatableObject() as CampaignShip;
                    MBTextManager.SetTextVariable("SHIP_NAME", ship.Name);
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    return true;
                },
                delegate(MenuCallbackArgs args)
                {
                    CampaignShip ship = args.MenuContext.GetSelectedRepeatableObject() as CampaignShip;
                    _managedShip = ship;
                    GameMenu.SwitchToMenu("port_manage_single_ship");
                },
                false, -1, true);

            starter.AddGameMenuOption(
                "port_select_ship", 
                "manager_ships_return",
                "{=UgIDUQ4o94}Return to manage fleet",
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    GameMenu.SwitchToMenu("port_manage_single_fleet");
                },
                false, -1, false);
            #endregion

            #region Manage Single Ship Menu
            starter.AddGameMenu(
                "port_manage_single_ship", 
                "{MANAGED_SHIP_TEXT}",
                delegate (MenuCallbackArgs args)
                {
                    MBStringBuilder sb = default(MBStringBuilder);
                    sb.Initialize();

                    TextObject shipName = new TextObject("{LABEL1}: {SHIP_NAME}");
                    TextObject label1 = new TextObject("{=T7Cu7H4UsX}Ship Name");
                    shipName.SetTextVariable("LABEL1", label1);
                    shipName.SetTextVariable("SHIP_NAME", _managedShip.Name);
                    sb.AppendLine(shipName);

                    TextObject shipClass = new TextObject("{LABEL2}: {SHIP_CLASS}");
                    TextObject label2 = new TextObject("{=8hc9mPS9Mr}Class");
                    shipClass.SetTextVariable("LABEL2", label2);
                    shipClass.SetTextVariable("SHIP_CLASS", _managedShip.Class);
                    sb.Append(shipClass);

                    TextObject shipCrew = new TextObject("{LABEL3}: {SHIP_CREW_CAPACITY}");
                    TextObject label3 = new TextObject("{=CFL4v1v3XV}Crew Capacity");
                    shipCrew.SetTextVariable("LABEL3", label3);
                    shipCrew.SetTextVariable("SHIP_CREW_CAPACITY", _managedShip.CrewCapacity);
                    sb.AppendLine(shipCrew);

                    TextObject shipTroop = new TextObject("{LABEL4}: {SHIP_TROOP_CAPACITY}");
                    TextObject label4 = new TextObject("{=vwNi091pvN}Troop Capacity");
                    shipTroop.SetTextVariable("LABEL4", label4);
                    shipTroop.SetTextVariable("SHIP_TROOP_CAPACITY", _managedShip.TroopCapacity);
                    sb.AppendLine(shipTroop);

                    TextObject shipCargo = new TextObject("{LABEL5}: {SHIP_CARGO_CAPACITY}");
                    TextObject label5 = new TextObject("{=nZdnsiiv3y}Cargo Capacity");
                    shipCargo.SetTextVariable("LABEL5", label5);
                    shipCargo.SetTextVariable("SHIP_CARGO_CAPACITY", _managedShip.CargoCapacity);
                    sb.AppendLine(shipCargo);

                    TextObject shipSpeed = new TextObject("{LABEL6}: {SHIP_SPEED}");
                    TextObject label6 = new TextObject("{=6GSXsdeX}Speed");
                    shipSpeed.SetTextVariable("LABEL6", label6);
                    shipSpeed.SetTextVariable("SHIP_SPEED", _managedShip.Speed.ToString("0.00"));
                    sb.AppendLine(shipSpeed);

                    MBTextManager.SetTextVariable("MANAGED_SHIP_TEXT", sb.ToStringAndRelease());
                },
                GameOverlays.MenuOverlayType.SettlementWithBoth, GameMenu.MenuFlags.None, null);

            starter.AddGameMenuOption(
                "port_manage_single_ship", 
                "single_ship_rename", 
                "{=qco1qQSk3b}Rename",
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Continue;
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    GameTexts.SetVariable("MAX_LETTER_COUNT", CampaignShip.ShipNameCharLimit);
                    GameTexts.SetVariable("MIN_LETTER_COUNT", 1);
                    InformationManager.ShowTextInquiry(new TextInquiryData(
                        "{=T7Cu7H4UsX}Ship Name", 
                        string.Empty, true, true, 
                        GameTexts.FindText("str_done", null).ToString(),
                        GameTexts.FindText("str_cancel", null).ToString(),
                            delegate (string str)
                            {
                                _managedShip.Name = new TextObject(str, null);
                                MBTextManager.SetTextVariable("MANAGED_SHIP_NAME", _managedShip.Name);
                                args.MenuContext.Refresh();
                            }, null, false, CampaignShip.IsShipNameValid, "", _managedShip.Name.ToString()));
                },
                false, -1, false);

            starter.AddGameMenuOption(
                "port_manage_single_ship", 
                "single_ship_sell", 
                "{=nO4ZTTfFbI}Sell",
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    GameMenu.SwitchToMenu("sell_ship_confirm");
                },
                false, -1, false);
            starter.AddGameMenuOption(
                "port_manage_single_ship",
                "transfer_to_other_fleet",
                "{=3GcjXQvyPI}Transfer to another fleet",
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Continue;
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    GameMenu.SwitchToMenu("port_transfer_ship");
                },
                false, -1, false);


            #region Transfer Ship
            starter.AddGameMenu(
                "port_transfer_ship", 
                "{=4Vm5F55ggE}Select the receiving fleet or create a new one",
                delegate (MenuCallbackArgs args)
                {
                    List<Fleet> otherFleets = _availableFleets.Where(x => x != _managedFleet).ToList();
                    if(otherFleets.Count > 0)
                        args.MenuContext.SetRepeatObjectList(otherFleets);
                },
                GameOverlays.MenuOverlayType.SettlementWithBoth, GameMenu.MenuFlags.None, null);

            starter.AddGameMenuOption(
                "port_transfer_ship",
                "port_transfer_to_fleet_item",
                "{FLEET_NAME}",
                delegate (MenuCallbackArgs args)
                {
                    Fleet fleet = args.MenuContext.GetCurrentRepeatableObject() as Fleet;
                    if (fleet != null)
                    {
                        MBTextManager.SetTextVariable("FLEET_NAME", fleet.Name);
                        return true;
                    }
                    return false;
                },
                delegate (MenuCallbackArgs args)
                {
                    Fleet recipient = args.MenuContext.GetSelectedRepeatableObject() as Fleet;
                    _managedFleet.RemoveShip(_managedShip);
                    recipient.AddShip(_managedShip);
                    if (_managedFleet.Count == 0)
                    {
                        _availableFleets.Remove(_managedFleet);
                        MobilePartyWrapper.MainPartyWrapper.DestroyStoredFleet(_managedFleet);
                        _managedFleet = null;
                        if (_availableFleets.Count > 0)
                            GameMenu.SwitchToMenu("port_select_fleet");
                        else
                            GameMenu.SwitchToMenu("town_port");
                    }
                    else
                    {
                        GameMenu.SwitchToMenu("port_manage_single_fleet");
                    }
                }, 
                false, -1, true);

            starter.AddGameMenuOption(
                "port_transfer_ship",
                "port_transfer_ship_create_new_fleet",
                "{=hFSVMu1Ut9}Create a new fleet",
                delegate (MenuCallbackArgs args)
                {
                    if (_managedShip != null)
                    {
                        MBTextManager.SetTextVariable("MANAGED_SHIP_ITEM", _managedShip.Name);
                        args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    }
                    else
                    {
                        MBTextManager.SetTextVariable("MANAGED_SHIP_ITEM", "***ERROR ship null at port_transfer_ship_create_new_fleet");
                    }
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    // GameMenu.SwitchToMenu("port_manage_single_ship");
                    GameTexts.SetVariable("MAX_LETTER_COUNT", CampaignShip.ShipNameCharLimit);
                    GameTexts.SetVariable("MIN_LETTER_COUNT", 1);
                    InformationManager.ShowTextInquiry(
                        new TextInquiryData(
                            new TextObject("{=SnTAwIcMUi}Fleet Name").ToString(),
                            string.Empty, true, true,
                            GameTexts.FindText("str_done", null).ToString(),
                            GameTexts.FindText("str_cancel", null).ToString(),
                            delegate (string str)
                            {
                                Fleet newFleet;
                                MobilePartyWrapper.MainPartyWrapper.CreateNewFleet(new List<CampaignShip>() { _managedShip }, Settlement.CurrentSettlement, out newFleet);
                                newFleet.Name = new TextObject(str);
                                _availableFleets.Add(newFleet);
                                _managedFleet.RemoveShip(_managedShip);
                                if (_managedFleet.Count == 0)
                                {
                                    _availableFleets.Remove(_managedFleet);
                                    MobilePartyWrapper.MainPartyWrapper.DestroyStoredFleet(_managedFleet);
                                    _managedFleet = newFleet;
                                    GameMenu.SwitchToMenu("port_manage_single_fleet");
                                }
                                else
                                {
                                    GameMenu.SwitchToMenu("port_manage_single_fleet");
                                }
                            }, null, false, CampaignShip.IsShipNameValid, "", _managedShip.Name.ToString()));
                },
                false, -1, false);

            starter.AddGameMenuOption(
                "port_transfer_ship",
                "port_transfer_ship_cancel",
                GameTexts.FindText("str_cancel", null).ToString(),
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    GameMenu.SwitchToMenu("port_manage_single_ship");
                },
                false, -1, false);

            #endregion

            #region Sell Ship Confirm
            starter.AddGameMenu(
                "sell_ship_confirm", 
                "{=IXBdFAKDzw}Sell your ship {MANAGED_SHIP_NAME} for {SHIP_VALUE}{GOLD_ICON}?",
                delegate (MenuCallbackArgs args)
                {
                    MBTextManager.SetTextVariable("MANAGED_SHIP_NAME", _managedShip.Name);
                    MBTextManager.SetTextVariable("SHIP_VALUE", (int)(_managedShip.Cost * 0.9f));
                },
                GameOverlays.MenuOverlayType.SettlementWithBoth, GameMenu.MenuFlags.None, null);
            starter.AddGameMenuOption(
                "sell_ship_confirm", 
                "single_ship_sell",
                "{=Mlh6zpQTB1}Confirm",
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    GiveGoldAction.ApplyBetweenCharacters(null, MobileParty.MainParty.LeaderHero, DefaultShipEconomnyModel.GetShipValue(_managedShip), false);
                    _managedFleet.RemoveShip(_managedShip);
                    if (_managedFleet.Count == 0)
                    {
                        _availableFleets.Remove(_managedFleet);
                        MobilePartyWrapper.MainPartyWrapper.DestroyStoredFleet(_managedFleet);
                        _managedFleet = null;
                        if (_availableFleets.Count > 0)
                            GameMenu.SwitchToMenu("port_select_fleet");
                        else
                            GameMenu.SwitchToMenu("town_port");
                    }
                    else
                    {
                        GameMenu.SwitchToMenu("port_select_ship");
                    }
                },
                false, -1, false);

            starter.AddGameMenuOption(
                "sell_ship_confirm", 
                "single_ship_sell",
                GameTexts.FindText("str_cancel", null).ToString(),
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    GameMenu.SwitchToMenu("port_manage_single_ship");
                },
                false, -1, false);
            #endregion

            starter.AddGameMenuOption(
                "port_manage_single_ship", 
                "single_ship_upgrade",
                "{=y897yqH8uh}Upgrade",
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    args.IsEnabled = false;
                    args.Tooltip = new TextObject("{=3t032DZwQT}Coming Soon (TM)");
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {

                },
                false, -1, false);

            starter.AddGameMenuOption(
                "port_manage_single_ship", 
                "manage_single_ship_return",
                "{=1PhHfHu8aI}Manage another ship",
                delegate (MenuCallbackArgs args)
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                delegate (MenuCallbackArgs args)
                {
                    GameMenu.SwitchToMenu("port_select_ship");
                },
                false, -1, false);
            #endregion
        }
    }
}
