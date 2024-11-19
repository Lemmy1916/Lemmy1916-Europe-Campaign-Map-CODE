using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using SandBox;
using SandBox.View.Map;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;
using static TaleWorlds.CampaignSystem.Actions.ChangeOwnerOfSettlementAction;

using Europe_LemmyProject.General;

using HarmonyLib;
using static TaleWorlds.CampaignSystem.ComponentInterfaces.SettlementAccessModel;
using TaleWorlds.CampaignSystem.ComponentInterfaces;

namespace Europe_LemmyProject.MaritimeSystem
{

    /// <summary>
    /// Class for saving the active maritime travel information of the player.
    /// </summary>
    public class MaritimePathTravel
    {
        [SaveableField(0)]
        private string _pathName;
        public string PathName { get { return _pathName; } set { _pathName = value; } }
        [SaveableField(1)]
        private float _distanceTraveled;
        public float DistanceTraveled { get { return _distanceTraveled; } set { _distanceTraveled = value; } }
        [SaveableField(2)]
        private bool _reverseDirection;
        public bool ReverseDirection { get { return _reverseDirection; } set { _reverseDirection = value; } }
        [SaveableField(3)]
        private Settlement _fromSettlement;
        public Settlement FromSettlement { get { return _fromSettlement; } set { _fromSettlement = value; } }
        [SaveableField(4)]
        private Settlement _toSettlement;
        public Settlement ToSettlement { get { return _toSettlement; } set { _toSettlement = value; } }

        private ReversiblePathTracker _pathTracker;
        public ReversiblePathTracker PathTracker { get { return _pathTracker; } set { _pathTracker = value; } }

        private int _travelCost;
        public int TravelCost { get { return _travelCost; } set { _travelCost = value; } }
    }

    /// <summary>
    /// Like the standard PathTracker, but can be started from either end of the path.
    /// </summary>
    public class ReversiblePathTracker
    {
		private Path _path;
		public Path Path { get { return _path; } }
		private Vec3 _initialScale;
		private int _version = -1;
		private bool _reverseDirection;

		public bool ReverseDirection { get { return _reverseDirection; } }
		public float TotalDistanceTraveled { get; set; }

		public bool HasChanged
		{
			get
			{
				return this._path != null && this._version < this._path.GetVersion();
			}
		}

		public bool IsValid
		{
			get
			{
				return this._path != null;
			}
		}

		public bool HasReachedEnd
		{
			get
			{
				return this.TotalDistanceTraveled >= this._path.TotalDistance;
			}
		}

		public float PathTraveledPercentage
		{
			get
			{
				return this.TotalDistanceTraveled / this._path.TotalDistance;
			}
		}

		public MatrixFrame CurrentFrame
		{
			get
			{
				MatrixFrame frameForDistance = MatrixFrame.Identity;
				if (_reverseDirection) 
				{
					frameForDistance = this._path.GetFrameForDistance(this._path.TotalDistance - this.TotalDistanceTraveled);
					frameForDistance.rotation.ApplyScaleLocal(this._initialScale);
                }
                else
                {
					frameForDistance = this._path.GetFrameForDistance(this.TotalDistanceTraveled);
					frameForDistance.rotation.RotateAboutUp(3.1415927f);
					frameForDistance.rotation.ApplyScaleLocal(this._initialScale);
				}
				return frameForDistance;
			}
		}

		public ReversiblePathTracker(Path path, Vec3 initialScaleOfEntity, bool reverseDirection)
		{
			this._path = path;
			this._initialScale = initialScaleOfEntity;
			this._reverseDirection = reverseDirection;
			if (path != null)
			{
				this.UpdateVersion();
			}
			this.Reset();
		}

		public void UpdateVersion()
		{
			this._version = this._path.GetVersion();
		}

		public bool PathExists()
		{
			return this._path != null;
		}

		public void Advance(float deltaDistance)
		{
			this.TotalDistanceTraveled += deltaDistance;
			this.TotalDistanceTraveled = MathF.Min(this.TotalDistanceTraveled, this._path.TotalDistance);
		}

		public float GetPathLength()
		{
			return this._path.TotalDistance;
		}

		public void CurrentFrameAndColor(out MatrixFrame frame, out Vec3 color)
		{
			if (this._reverseDirection)
			{
				this._path.GetFrameAndColorForDistance(this._path.TotalDistance - this.TotalDistanceTraveled, out frame, out color);
				frame.rotation.ApplyScaleLocal(this._initialScale);
			}
            else
            {
				this._path.GetFrameAndColorForDistance(this.TotalDistanceTraveled, out frame, out color);
				frame.rotation.RotateAboutUp(3.1415927f);
				frame.rotation.ApplyScaleLocal(this._initialScale);
			}
		}

		public void Reset()
		{
			this.TotalDistanceTraveled = 0f;
		}
	}

    /// <summary>
    /// Defines a Fleet of CampaignShips
    /// </summary>
    public class Fleet
    {
        #region Statics
        /// <summary>
        /// Get a list of campaign ships whose crew capacity minimally covers the size of the 
        /// MobileParty.
        /// </summary>
        /// <param name="party"></param>
        /// <returns></returns>
        public static List<CampaignShip> GetMinLargestShips(MobileParty party)
        {
            List<CampaignShipTemplate> templates = CampaignShipTemplate.AllShipTemplates;
            int partySize = party.MemberRoster.TotalManCount;
            List<int> availableCrewSizes = CampaignShipTemplate.AllShipTemplates.OrderBy(x => x.BaseCrewCapacity).Select(x => x.BaseCrewCapacity).ToList();
            List<CampaignShip> ships = new List<CampaignShip>();

            for (int i = 0; i < availableCrewSizes.Count; i++)
            {
                int wantedSize = availableCrewSizes[i];
                while (ships.Count < 10 && ships.Sum(x => x.TroopCapacity) < partySize)
                {
                    if(i == 0)
                    {
                        ships.Add(new CampaignShip(templates.First(x => x.BaseCrewCapacity == availableCrewSizes[i])));
                    }
                    else // we have more than 10 ships, so replace some with ones of higher crew capacity
                    {
                        int first = ships.IndexOf(ships.First(x => x.CrewCapacity == availableCrewSizes[i - 1]));
                        ships[first] = new CampaignShip(templates.First(x => x.BaseCrewCapacity == ships[first].CrewCapacity));
                    }
                }
            }

            return ships;
        }

        public static bool CanSetSail(Fleet fleet, out TextObject explanation)
        {
            int diff = MobileParty.MainParty.MemberRoster.TotalManCount - fleet.TroopCapacity;
            if (diff > 0)
            {
                explanation = new TextObject("{=Sn7TJjpjyT}Your fleet does not have enough room for {SHIP_COUNT} of your men");
                explanation.SetTextVariable("SHIP_COUNT", diff);
                return false;
            }
            else
            {
                explanation = new TextObject(GetFleetInfo(fleet, "set_sail").ToStringAndRelease());
                return true;
            }
        }

        public static MBStringBuilder GetFleetInfo(Fleet fleet, string caller)
        {
            MBStringBuilder sb = default(MBStringBuilder);
            sb.Initialize(16, caller);

            TextObject fleetName = new TextObject("{LABEL1} {FLEET_NAME}");
            TextObject label1 = new TextObject("{=hxvo8CFBpw}Your fleet");
            fleetName.SetTextVariable("LABEL1", label1);
            fleetName.SetTextVariable("FLEET_NAME", fleet.Name);
            sb.AppendLine(fleetName);

            TextObject flagShip = new TextObject("{LABEL2} : {FLAGSHIP_NAME}");
            TextObject label2 = new TextObject("{=RO8Iz3FI6h}Flagship");
            flagShip.SetTextVariable("LABEL2", label2);
            flagShip.SetTextVariable("FLAGSHIP_NAME", fleet.Flagship.Name);
            sb.AppendLine(flagShip);

            TextObject shipCount = new TextObject("{LABEL3} : {SHIP_COUNT}");
            TextObject label3 = new TextObject("{=CqbzVqMo2o}Ship Count");
            shipCount.SetTextVariable("LABEL3", label3);
            shipCount.SetTextVariable("SHIP_COUNT", fleet.Count);
            sb.AppendLine(shipCount);

            TextObject fleetCrew = new TextObject("{LABEL4} : {FLEET_CREW}");
            TextObject label4 = new TextObject("{=CFL4v1v3XV}Crew Capactiy");
            fleetCrew.SetTextVariable("LABEL4", label4);
            fleetCrew.SetTextVariable("FLEET_CREW", fleet.CrewCapacity);
            sb.AppendLine(fleetCrew);

            TextObject fleetTroop = new TextObject("{LABEL5} : {FLEET_TROOPS}");
            TextObject label5 = new TextObject("{=vwNi091pvN}Troop Capacity");
            fleetTroop.SetTextVariable("LABEL5", label5);
            fleetTroop.SetTextVariable("FLEET_TROOPS", fleet.TroopCapacity);
            sb.AppendLine(fleetTroop);

            TextObject fleetCargo = new TextObject("{LABEL6} : {CARGO_CAPACITY}");
            TextObject label6 = new TextObject("{=nZdnsiiv3y}Cargo Capacity");
            fleetCargo.SetTextVariable("LABEL6", label6);
            fleetCargo.SetTextVariable("CARGO_CAPACITY", fleet.CargoCapacity);
            sb.AppendLine(fleetCargo);

            TextObject fleetSpeed = new TextObject("{LABEL7} : {FLEET_SPEED}");
            TextObject label7 = new TextObject("{=ms0YKFCx0p}Speed");
            fleetSpeed.SetTextVariable("LABEL7", label7);
            fleetSpeed.SetTextVariable("FLEET_SPEED", fleet.Speed.ToString("0.00"));
            sb.AppendLine(fleetSpeed);

            return sb;
        }
        #endregion

        [SaveableField(0)]
        private Hero _owner;
        public Hero Owner { get { return _owner; } set { _owner = value; } }

        [SaveableField(1)]
        private Settlement _settlement;
        public Settlement Settlement { get { return _settlement; } set { value = _settlement; } }

        [SaveableField(2)]
        private MobileParty _mobileParty;
        public MobileParty MobileParty { get { return _mobileParty; } set { value = _mobileParty; } }

        [SaveableField(3)]
        private List<CampaignShip> _ships;
        public List<CampaignShip> Ships { get { return _ships; } }

        [SaveableField(4)]
        private TextObject _name;
        public TextObject Name 
        { 
            get 
            {
                if (_name == null)
                    _name = GetLocationName();
                return _name; 
            } 
            set 
            { 
                _name = value;
            }
        }

        // Flagship is always the ship at index 0 in _ships
        public CampaignShip Flagship 
        { 
            get 
            { 
                return Ships.Count > 0 ? Ships[0] : null; 
            } 
            set 
            {
                if (Ships.IndexOf(value) != 0)
                {
                    CampaignShip ship = value;
                    _ships.Remove(ship);
                    _ships.Insert(0, ship);
                }
            }
        }

        public int Count
        {
            get { return Ships.Count; }
        }

        public int CrewCapacity
        {
            get
            {
                if (Ships == null)
                    return 0;
                return Ships.Sum(x => x.CrewCapacity);
            }
        }

        public int TroopCapacity
        {
            get
            {
                if (Ships == null)
                    return 0;

                return Ships.Sum(x => x.TroopCapacity);
            }
        }

        public float CargoCapacity
        {
            get
            {
                if (Ships == null)
                    return 0f;

                return Ships.Sum(x => x.CargoCapacity);
            }
        }

        public float Speed
        {
            get
            {
                if (Ships == null)
                    return 0f;

                CampaignShip min = Ships.OrderBy(x => x.Speed * x.Health).FirstOrDefault();
                if (min == null)
                    return 0f;

                return MathF.Max(min.Speed * min.Health / min.MaxHealth, 1f);
            }
        }

        public float Health
        {
            get
            {
                if (Ships == null)
                    return 0f;

                return Ships.Average(x => x.Health);
            }
        }

        public float Value
        {
            get
            {
                if (Ships == null)
                    return 0f;

                return Ships.Sum(x => x.Cost);
            }
        }

        public Fleet(Hero owner, List<CampaignShip> ships, MobileParty mobileParty = null, Settlement settlement = null)
        {
            _owner = owner;
            _ships = ships;
            _mobileParty = mobileParty;
            _settlement = settlement;
        }

        public bool IsFlagship(CampaignShip ship)
        {
            return Ships.IndexOf(ship) == 0;
        }

        public void AddShip(CampaignShip ship)
        {
            _ships.Add(ship);
        }

        public void RemoveShip(CampaignShip ship)
        {
            _ships.Remove(ship);
        }

        public void TransferToMobileParty(MobileParty party)
        {
            _settlement = null;
            _mobileParty = party;
        }

        public void TransferToSettlement(Settlement settlement)
        {
            _mobileParty = null;
            _settlement = settlement;
        }

        public void AddShips(List<CampaignShip> ships)
        {
            _ships.AppendList(ships);
        }

        public TextObject GetLocationName()
        {
            TextObject locationName = new TextObject("***ERROR***");
            TimedFleetDelivery timedDelivery = TimedCampaignActionManager.Instance.Actions.FirstOrDefault(x => x is TimedFleetDelivery) as TimedFleetDelivery;
            if (_settlement != null)
                locationName = _settlement.Name;
            else if (_mobileParty != null)
                locationName = _mobileParty.Name;
            else if (timedDelivery != null)
            {
                TextObject destination = timedDelivery.End.Name;
                locationName = new TextObject("{=jhrISZWUpz}Arriving at {DESTINATION_NAME} in {DAYS_STR}.");
                int days = (int)(timedDelivery.ActionTime - CampaignTime.Now).ToDays;
                TextObject daysStr = TextObject.Empty;
                if (days == 0)
                    daysStr = new TextObject("{=jsbyJ80Ops}less than one day");
                else if (days == 1)
                    daysStr = new TextObject("{=V7KIK61gp7}one day");
                else
                {
                    daysStr = new TextObject("{=Cf4ZIn6ktH}{DAYS} days");
                    daysStr.SetTextVariable("DAYS", days);
                }
                locationName.SetTextVariable("DESTINATION_NAME", destination);
                locationName.SetTextVariable("DAYS_STR", daysStr);
            }

            return locationName;
        }

        public Vec2 GetPosition()
        {
            if (_settlement != null)
                return _settlement.Position2D;
            if (_mobileParty != null)
                return _mobileParty.Position2D;
            return Vec2.Invalid;
        }
    }

    /// <summary>
    /// Wrapper class for MobileParty. Used for keeping track of maritime system additions like Fleets.
    /// </summary>
    public class MobilePartyWrapper
    {
        public static MobilePartyWrapper MainPartyWrapper { get { return MaritimeManager.Instance.MainPartyWrapper; } }

        [SaveableField(0)]
        private MobileParty _owner;
        public MobileParty Owner { get { return _owner; } }

        /*
        [SaveableField(1)]
        private string _shipPrefabName;
        public string ShipPrefabName { get { return _shipPrefabName; } private set { _shipPrefabName = value; } }
        */

        [SaveableField(4)]
        private List<Fleet> _storedFleets;
        public List<Fleet> StoredFleets { get { return this._storedFleets; } private set { _storedFleets = value; } }

        [SaveableField(5)]
        private Fleet _currentFleet;
        public Fleet CurrentFleet { get { return this._currentFleet; } set { this._currentFleet = value;} }

        public MobilePartyWrapper(MobileParty thisParty)
        {
            _owner = thisParty;
            _storedFleets = new List<Fleet>();
        }

        public bool IsAtSea
        {
            get { return MaritimeManager.Instance.IsPartyAtSea(Owner); }
        }
        public int AllShipsCount
        {
            get
            {
                int count = _currentFleet == null ? 0 : _currentFleet.Ships.Count;
                count += StoredFleets == null ? 0 : (from x in StoredFleets select x.Ships.Count).Sum();
                return count;
            }
        }

        public List<Fleet> AllFleets
        {
            get
            {
                List<Fleet> toReturn = new List<Fleet>();
                if (_currentFleet != null)
                    toReturn.Add(_currentFleet);
                if(_storedFleets != null)
                    toReturn.AddRange(_storedFleets);
                return toReturn;
            }
        }

        public void CreateNewFleet(List<CampaignShip> ships, Settlement settlement, out Fleet fleet)
        {
            fleet = new Fleet(Hero.MainHero, ships, null, settlement);
            if (StoredFleets == null)
                StoredFleets = new List<Fleet>();
            StoredFleets.Add(fleet);
        }

        public void CreateNewFleet(List<CampaignShip> ships, MobileParty party)
        {
            Fleet fleet = new Fleet(Hero.MainHero, ships, party, null);
            if (StoredFleets == null)
                StoredFleets = new List<Fleet>();
            StoredFleets.Add(fleet);
        }

        public void DestroyStoredFleet(Fleet fleet)
        {
           _storedFleets.Remove(fleet);
        }

        public void DestroyCurrentFleet()
        {
            _currentFleet = null;
        }

        public void DestroyFleet(Fleet fleet)
        {
            if (fleet == _currentFleet)
                DestroyCurrentFleet();
            else if (StoredFleets.Contains(fleet))
                DestroyStoredFleet(fleet);
            else
            {
                // give some kind of warning
            }
        }

        public void TakeFleetFromSettlement(Fleet fleet)
        {
            _currentFleet = fleet;
            StoredFleets.Remove(fleet);
            fleet.TransferToMobileParty(Owner);
        }

        public void GiveCurrentFleetToSettlement(Settlement settlement)
        {
            _currentFleet.TransferToSettlement(settlement);
            StoredFleets.Add(_currentFleet);
            _currentFleet = null;
        }

        /// <summary>
        /// Merges fleet2 into fleet1
        /// </summary>
        /// <param name="fleet1"></param>
        /// <param name="fleet2"></param>
        public void MergeFleets(Fleet fleet1, Fleet fleet2)
        {
            if (fleet1 == _currentFleet && StoredFleets.Contains(fleet2))
            {

            }
            else if (StoredFleets.Contains(fleet1) && _currentFleet == fleet2)
            {

            }
            else if (StoredFleets.Contains(fleet1) && StoredFleets.Contains(fleet2))
            {

            }
        }

        public List<Fleet> GetFleetsAtSettlement(Settlement settlement)
        {
            return StoredFleets?.Where(x => x.Settlement == settlement).ToList();
        }

        public int NumShipsAtSettlement(Settlement settlement)
        {
            List<Fleet> fleetsAtPort = GetFleetsAtSettlement(settlement);
            return fleetsAtPort.Sum(x => x.Count);
        }

        public ExplainedNumber GetCurrentFleetSpeed(out float speed, out TextObject explanation)
        {
            explanation = TextObject.Empty;
            if (this.Owner == MobileParty.MainParty)
            {
                if (this.CurrentFleet != null)
                {
                    speed = this.CurrentFleet.Speed;
                    explanation = new TextObject("{=txXnRqBKLN}Fleet Base Speed");
                }
                else
                {
                    speed = 10f;
                    explanation = new TextObject("{=PPxazU4HuH}Base Cheat Speed");
                }
            }
            else
            {
                speed = 8f;
                explanation = new TextObject("{=ZjcmEVvz0e}Base AI Fleet Speed");
            }
            return new ExplainedNumber(speed, true, explanation);
        }

        public ExplainedNumber GetCurrentFleetInventoryCapacity(out float capacity, out TextObject explanation)
        {
            explanation = TextObject.Empty;
            if (this.CurrentFleet != null)
            {
                capacity = this.CurrentFleet.CargoCapacity;
                explanation = new TextObject("{=KEClpv1FOw}Fleet Cargo Capacity");
            }
            else
            {
                capacity = 100000f;
                explanation = new TextObject("{=gOnt4m8xLK}Base Cheat Cargo Capacity");
            }
            return new ExplainedNumber(capacity, true, explanation);
        }
    }

    /// <summary>
    /// Class for managing maritime-specific behavior
    /// </summary>
    public class MaritimeManager : CampaignBehaviorBase
    {
        /// <summary>
        /// Defines saveable types and contatiners for the martime mechanic
        /// </summary>
        public class MaritimeSystemTypeDefiner : SaveableCampaignBehaviorTypeDefiner
        {
            public MaritimeSystemTypeDefiner() : base(210317) { }

            protected override void DefineClassTypes()
            {
                base.AddClassDefinition(typeof(MaritimePathTravel), 1);
                base.AddClassDefinition(typeof(MobilePartyWrapper), 2);
                base.AddClassDefinition(typeof(CampaignShip), 3);
                base.AddClassDefinition(typeof(LandingArea), 4);
                base.AddClassDefinition(typeof(Fleet), 5);
            }

            protected override void DefineContainerDefinitions()
            {
                base.ConstructContainerDefinition(typeof(List<CampaignShip>));
                base.ConstructContainerDefinition(typeof(Dictionary<Settlement, List<CampaignShip>>));
                base.ConstructContainerDefinition(typeof(List<Fleet>));
                base.ConstructContainerDefinition(typeof(Dictionary<MobileParty, string>));
            }
        }

        public class MaritimeConceptManager
        {
            public Dictionary<string, Tuple<string,string>> ConceptsWithIds { get; private set; }
            public List<Tuple<string, string>> Concepts 
            { 
                get
                {
                    return ConceptsWithIds.Values.ToList();
                }
            }

            public MaritimeConceptManager()
            {
                ConceptsWithIds = new Dictionary<string, Tuple<string, string>>();
            }

            public void Deserialize()
            {
                XmlReaderSettings readerSettings = new XmlReaderSettings();
                readerSettings.IgnoreComments = true;
                string path = ModuleHelper.GetModuleFullPath("Europe_LemmyProject") + "ModuleData/ELPData/MaritimeConcepts.xml";
                using (XmlReader reader = XmlReader.Create(path, readerSettings))
                {
                    XmlDocument conceptsDoc = new XmlDocument();
                    conceptsDoc.Load(reader);
                    XmlNode concepts = conceptsDoc.ChildNodes[1];
                    foreach (XmlNode concept in concepts)
                    {
                        string id = concept.Attributes["id"].Value;
                        string title = concept.Attributes["title"].Value;
                        string description = concept.Attributes["description"].Value;
                        ConceptsWithIds.Add(id, new Tuple<string, string>(title, description));
                    }
                }
            }
        }

        public static readonly HashSet<TerrainType> ValidLandTypes = new HashSet<TerrainType>()
        {
            TerrainType.Snow,
            TerrainType.Steppe,
            TerrainType.Plain,
            TerrainType.Desert,
            TerrainType.Swamp,
            TerrainType.Dune,
            TerrainType.Bridge,
            TerrainType.Forest,
            TerrainType.ShallowRiver
        };

        public static readonly HashSet<TerrainType> ValidWaterTypes = new HashSet<TerrainType>()
        {
            TerrainType.Water,
            TerrainType.River
        };

        public static readonly int CoastlineNavmeshID = 15;
        // public static int MaxShipCount = 10;
        public static string DefaultMerchantShipPrefab = "elp_ship_icon_c";
        public static string DefaultBanditShipPrefab = "elp_default_ship_icon";
        public static string DefaultMaritimeTravelPrefab = "elp_ship_icon_c";

        public static MaritimeManager Instance { get; private set; }

        private List<MobileParty> _partiesAtSea;
        public List<MobileParty> PartiesAtSea { get { return _partiesAtSea; } }

        private MaritimePathTravel _currentMaritimePathTravel;
        public MaritimePathTravel CurrentMaritimePathTravel { 
            get { return _currentMaritimePathTravel; } 
            set { _currentMaritimePathTravel = value; } }

        private MobilePartyWrapper _mainPartyWrapper;
        public MobilePartyWrapper MainPartyWrapper { get { return _mainPartyWrapper; } }

        private List<MobilePartyWrapper> _aiMobilePartyWrappers;
        private bool _firstTick = false;

        private Dictionary<MobileParty, string> _aiPartyShipPrefabs;
        public Dictionary<MobileParty, string> AiPartyShipPrefabs;

        public List<MobilePartyWrapper> AIMobilePartyWrappers { get { return _aiMobilePartyWrappers; } }

        public MaritimeConceptManager ConceptManager { get; private set; }

        #region Maritime Events and Delegates
        private readonly MbEvent<MobileParty> _onBeforePartyEnterSeaEvent = new MbEvent<MobileParty>();
        public static IMbEvent<MobileParty> OnBeforePartyEnterSeaEvent { get { return Instance._onBeforePartyEnterSeaEvent; } }
        public void OnBeforePartyEnterSea(MobileParty party)
        {
            Instance._onBeforePartyEnterSeaEvent.Invoke(party);
        }

        private readonly MbEvent<MobileParty> _onPartyEnterSeaEvent = new MbEvent<MobileParty>();
        public static IMbEvent<MobileParty> OnPartyEnterSeaEvent { get { return Instance._onPartyEnterSeaEvent; } }
        public void OnPartyEnterSea(MobileParty party)
        {
            Instance._onPartyEnterSeaEvent.Invoke(party);
        }

        private readonly MbEvent<MobileParty> _onBeforePartyLeaveSeaEvent = new MbEvent<MobileParty>();
        public static IMbEvent<MobileParty> OnBeforePartyLeaveSeaEvent { get { return Instance._onBeforePartyLeaveSeaEvent; } }
        public void OnBeforePartyLeaveSea(MobileParty party)
        {
            Instance._onBeforePartyLeaveSeaEvent.Invoke(party);
        }

        private readonly MbEvent<MobileParty> _onPartyLeaveSeaEvent = new MbEvent<MobileParty>();
        public static IMbEvent<MobileParty> OnPartyLeaveSeaEvent { get { return Instance._onPartyLeaveSeaEvent; } }
        public void OnPartyLeaveSea(MobileParty party)
        {
            Instance._onPartyLeaveSeaEvent.Invoke(party);
        }
        
        public delegate void MaritimeTravelEndDelegate();
        public event MaritimeTravelEndDelegate MaritimeTravelEnd;
        #endregion

        #region Static Queries
        /// <summary>
        /// Determines whethere a given Mobileparty is over a navigable water face.
        /// </summary>
        /// <param name="mobileParty"></param>
        /// <returns></returns>
        public static bool IsOverNavigableWater(MobileParty mobileParty)
        {
            TerrainType current = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(mobileParty.CurrentNavigationFace);
            return ValidWaterTypes.Contains(current);
        }

        /// <summary>
        /// Determines whether a given Vec2 is over a navigable water face.
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public static bool IsOverNavigableWater(Vec2 position)
        {
            TerrainType current = Campaign.Current.MapSceneWrapper.GetFaceTerrainType((Campaign.Current.MapSceneWrapper.GetFaceIndex(position)));
            return ValidWaterTypes.Contains(current);
        }

        /// <summary>
        /// Determines whether a given Vec2 is over a given terrain type.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="terrainType"></param>
        /// <returns></returns>
        public static bool IsOverTerrainType(Vec2 position, TerrainType terrainType)
        {
            TerrainType current = Campaign.Current.MapSceneWrapper.GetFaceTerrainType((Campaign.Current.MapSceneWrapper.GetFaceIndex(position)));
            return terrainType == current;
        }

        /// <summary>
        /// Determines whether a given PathFaceRecord is a valid land TerrainType.
        /// </summary>
        /// <param name="faceRecord"></param>
        /// <returns></returns>
        public static bool IsValidLandTerrain(PathFaceRecord faceRecord)
        {
            TerrainType current = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(faceRecord);
            return ValidLandTypes.Contains(current);
        }

        /// <summary>
        /// Determines whether a given TerrainType is a valid land face.
        /// </summary>
        /// <param name="terrainType"></param>
        /// <returns></returns>
        public static bool IsValidLandTerrain(TerrainType terrainType)
        {
            return ValidLandTypes.Contains(terrainType);
        }

        /// <summary>
        /// Determines whether a give PathFaceRecord is a valid navigable water face.
        /// </summary>
        /// <param name="faceRecord"></param>
        /// <returns></returns>
        public static bool IsValidWaterTerrain(PathFaceRecord faceRecord)
        {
            TerrainType current = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(faceRecord);
            return ValidWaterTypes.Contains(current);
        }

        /// <summary>
        /// Determines whether a given TerrainType is a valid navigable water face.
        /// </summary>
        /// <param name="terrainType"></param>
        /// <returns></returns>
        public static bool IsValidWaterTerrain(TerrainType terrainType)
        {
            return ValidWaterTypes.Contains(terrainType);
        }

        /// <summary>
        /// Determines whether a given Vec2 is over a valid land TerrainType.
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public static bool IsOverLand(Vec2 position)
        {
            TerrainType current = Campaign.Current.MapSceneWrapper.GetFaceTerrainType((Campaign.Current.MapSceneWrapper.GetFaceIndex(position)));
            return ValidLandTypes.Contains(current);
        }

        /// <summary>
        /// Determines whether a given MobileParty is over a valid land TerrainType.
        /// </summary>
        /// <param name="mobileParty"></param>
        /// <returns></returns>
        public static bool IsOverLand(MobileParty mobileParty)
        {
            TerrainType current = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(mobileParty.CurrentNavigationFace);
            return ValidLandTypes.Contains(current);
        }
        #endregion

        public MaritimeManager()
        {
            _partiesAtSea = new List<MobileParty>();
            _aiPartyShipPrefabs = new Dictionary<MobileParty, string>();
            Instance = this;
            ConceptManager = new MaritimeConceptManager();
            OnPartyEnterSeaEvent.AddNonSerializedListener(this, new Action<MobileParty>(this.PartyEnterSea));
            OnPartyLeaveSeaEvent.AddNonSerializedListener(this, new Action<MobileParty>(this.PartyLeaveSea));
        }

        [HarmonyPatch]
        public static class Patches
        {
            /// <summary>
            /// Places the player on land after he has been released from captivity while at sea.
            /// </summary>
            [HarmonyPostfix]
            [HarmonyPatch(typeof(PlayerCaptivity), "EndCaptivity")]
            public static void PlayerCaptivity_EndCaptivity_Patch()
            {
                if (MobileParty.MainParty != null
                    && MaritimeManager.Instance.IsPartyAtSea(MobileParty.MainParty))
                {
                    Settlement nearest = Settlement.All
                        .Where(x => x.IsVillage || x.IsTown || x.IsCastle)
                        .OrderBy(x => x.Position2D.Distance(MobileParty.MainParty.Position2D)).FirstOrDefault();

                    Vec2 landPos = Vec2.Invalid;
                    Vec2 current = MobileParty.MainParty.Position2D;
                    Vec2 end = nearest.GatePosition;
                    Vec2 norm = (end - current).Normalized();
                    while (!MaritimeManager.IsOverLand(current))
                    {
                        current += (0.2f * norm);
                    }

                    MobileParty.MainParty.Position2D = current;
                    Europe_LemmyProject.Patches.MobileParty_OnAiTick_Patch(MobileParty.MainParty);
                }
            }
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnGameLoaded));
            CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnSessionStart));
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnNewGameCreated));
            CampaignEvents.TickEvent.AddNonSerializedListener(this, new Action<float>(this.OnTick));
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, new Action<MobileParty, Settlement>(this.OnSettlementLeft));
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, new Action<MobileParty, PartyBase>(this.OnMobilePartyDestroyed));
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, new Action<Settlement, bool, Hero, Hero, Hero, ChangeOwnerOfSettlementDetail>(this.OnSettlementOwnerChanged));
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, new Action<TaleWorlds.CampaignSystem.MapEvents.MapEvent>(this.OnMapEventEnded));
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, new Action(this.OnHourlyTick));

            // might need these if we want each mobile party to have CampaignShips
            // CampaignEvents.MobilePartyCreated.AddNonSerializedListener(this, new Action<MobileParty>(this.OnMobilePartyCreated));
            // CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, new Action<MobileParty, PartyBase>(this.OnMobilePartyCreated));
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData<MaritimePathTravel>("_maritimePathTravel", ref _currentMaritimePathTravel);
            dataStore.SyncData<MobilePartyWrapper>("_mainPartyWrapper", ref _mainPartyWrapper);
            dataStore.SyncData<Dictionary<MobileParty, string>>("_aiPartyShipPrefabs", ref _aiPartyShipPrefabs);
        }

        private void OnNewGameCreated(CampaignGameStarter sarter)
        {
            CampaignShipTemplate.DeserializeAll();
            _mainPartyWrapper = new MobilePartyWrapper(MobileParty.MainParty);
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            ConceptManager.Deserialize();
            CampaignShipTemplate.DeserializeAll();
            if (_mainPartyWrapper == null)
                _mainPartyWrapper = new MobilePartyWrapper(MobileParty.MainParty);
            if (_currentMaritimePathTravel != null)
            {
                Tuple<Path, bool> savedPath = new Tuple<Path, bool>((Campaign.Current.MapSceneWrapper as MapScene).Scene.GetPathWithName(_currentMaritimePathTravel.PathName), _currentMaritimePathTravel.ReverseDirection);
                _currentMaritimePathTravel.PathTracker = new ReversiblePathTracker(savedPath.Item1, Vec3.One, savedPath.Item2);
                _currentMaritimePathTravel.PathTracker.TotalDistanceTraveled = _currentMaritimePathTravel.DistanceTraveled;
                Vec2 vec2 = _currentMaritimePathTravel.PathTracker.CurrentFrame.origin.AsVec2;
                MobileParty.MainParty.Position2D = vec2;
            }
        }

        private void OnSessionStart(CampaignGameStarter starter)
        {
            foreach(CampaignShip ship in _mainPartyWrapper.AllFleets.SelectMany(x => x.Ships))
            {
                if (ship.Name == null)
                    ship.Name = ship.Class;
            }
        }

        private void OnSettlementLeft(MobileParty party, Settlement settlement)
        {
            if (party != null && party.IsMainParty)
            {
                if (CurrentMaritimePathTravel != null)
                {
                    CurrentMaritimePathTravel.PathTracker.Advance(0.01f);
                    MatrixFrame frame = CurrentMaritimePathTravel.PathTracker.CurrentFrame;
                    frame.Rotate(MBMath.PI, Vec3.Up);
                    // army placement still isn't working right
                    if (party.Army != null && party.Army.LeaderParty == party)
                    {
                        foreach (MobileParty mobileParty in party.Army.LeaderPartyAndAttachedParties)
                        {

                            mobileParty.Position2D = frame.origin.AsVec2 + mobileParty.ArmyPositionAdder;
                        }
                    }
                    else
                    {
                        party.Position2D = frame.origin.AsVec2;
                    }
                    party.Party.AverageBearingRotation = frame.rotation.GetEulerAngles().Z;
                    Campaign.Current.SetTimeSpeed(2);
                    Campaign.Current.SetTimeControlModeLock(false);
                }
            }
        }

        private void OnMapEventEnded(TaleWorlds.CampaignSystem.MapEvents.MapEvent mapEvent)
        {
            // if the player is defeated at sea
            if (mapEvent.IsPlayerMapEvent
                && mapEvent.DefeatedSide == mapEvent.PlayerSide
                && IsPartyAtSea(MobileParty.MainParty))
            {
                MobilePartyWrapper.MainPartyWrapper.DestroyCurrentFleet();
            }
        }

        private void OnMobilePartyDestroyed(MobileParty mobileParty, PartyBase partyBase)
        {
            // destroy the player's fleet if he is defeated at sea
            if(mobileParty.IsMainParty 
                && IsPartyAtSea(mobileParty)
                && MobilePartyWrapper.MainPartyWrapper.CurrentFleet != null)
            {
                MobilePartyWrapper.MainPartyWrapper.DestroyCurrentFleet();
            }
        }

        private void OnHourlyTick()
        {
            
        }

        private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero, ChangeOwnerOfSettlementDetail detail)
        {
            // what should happen when the settlement that hold a player's fleet changes to a hostile faction?
        }

        private void OnTick(float dt)
        {
            // necessary to make parties that start in view of main party that are at sea
            // use their boat meshes
            if (!_firstTick)
            {
                foreach (MobileParty mobileParty in Campaign.Current.MobileParties)
                {
                    bool isOverWater = MaritimeManager.IsOverNavigableWater(mobileParty.VisualPosition2DWithoutError);
                    bool isAtSea = MaritimeManager.Instance.PartiesAtSea.Contains(mobileParty);
                    if (isOverWater && !isAtSea) // if over a water face, but not managed by MaritimeManager
                    {
                        _partiesAtSea.Add(mobileParty);
                        if (_aiPartyShipPrefabs.ContainsKey(mobileParty))
                        {
                            UseBoatPrefab(mobileParty, _aiPartyShipPrefabs[mobileParty]);
                        }
                        else
                        {
                            string prefabName = GetShipPrefab(mobileParty);
                            UseBoatPrefab(mobileParty, prefabName);
                        }
                        // recalculates party speed
                        ExplainedNumber num = mobileParty.SpeedExplained;
                    }
                }
                _firstTick = true;
            }

            // if the player is in an army, his ship mesh is taken care of
            bool isPlayerInArmy = MobileParty.MainParty.Army != null && MobileParty.MainParty.Army.LeaderParty != MobileParty.MainParty;
            // if player is following a quest target, let him enter the sea for free
            bool isFollowingQuestTarget = MobileParty.MainParty.TargetParty != null
                                          && MobileParty.MainParty.DefaultBehavior == AiBehavior.EscortParty
                                          && MobileParty.MainParty.TargetParty.IsCurrentlyUsedByAQuest;

            if (isPlayerInArmy || isFollowingQuestTarget)
                return;

            if (_currentMaritimePathTravel != null)
            {
                if (!_currentMaritimePathTravel.PathTracker.HasReachedEnd)
                {
                    MatrixFrame frame = _currentMaritimePathTravel.PathTracker.CurrentFrame;
                    frame.Rotate(MBMath.PI, Vec3.Up);
                    MobileParty mainParty = MobileParty.MainParty;
                    mainParty.Party.AverageBearingRotation = frame.rotation.GetEulerAngles().Z;
                    mainParty.SetMoveGoToPoint(frame.origin.AsVec2);
                    _currentMaritimePathTravel.PathTracker.Advance(mainParty.Speed * dt);
                }
                else
                {
                    CampaignCheats.SetMainPartyAttackable(new List<string>() { "1" });
                    this.MaritimeTravelEnd();
                    MobileParty.MainParty.SetMoveGoToPoint(MobileParty.MainParty.Position2D);
                    Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
                    CurrentMaritimePathTravel = null;
                }
            }
            else
            {
                // test if the player is travelling by arrow keys, if so, then need extra check so he can't
                // move into water

                bool isUp = MapScreen.Instance.SceneLayer.Input.IsGameKeyDown(MapHotKeyCategory.PartyMoveUp);
                bool isDown = MapScreen.Instance.SceneLayer.Input.IsGameKeyDown(MapHotKeyCategory.PartyMoveDown);
                bool isLeft = MapScreen.Instance.SceneLayer.Input.IsGameKeyDown(MapHotKeyCategory.PartyMoveLeft);
                bool isRight = MapScreen.Instance.SceneLayer.Input.IsGameKeyDown(MapHotKeyCategory.PartyMoveRight);
                MobileParty mainParty = MobileParty.MainParty;

                Vec2 bearing = mainParty.Bearing;
                Vec2 forward = mainParty.Position2D + 0.1f * bearing;

                bool isOnLandTrySea = IsOverNavigableWater(forward) &&
                                        !Instance.IsPartyAtSea(mainParty);
                bool isAtSeaTryLand = IsOverLand(forward) &&
                                        Instance.IsPartyAtSea(mainParty);
                if (isOnLandTrySea)
                {
                    Vec2 newPos = mainParty.Position2D - 0.2f * bearing;
                    mainParty.Position2D = newPos;
                    mainParty.SetMoveGoToPoint(newPos);
                    MBInformationManager.AddQuickInformation(new TextObject("{=w3yEN9pg12}I can't walk on water!"), 0, Hero.MainHero.CharacterObject);
                }
                else if (isAtSeaTryLand)
                {
                    //Vec2 newPos = mainParty.Position2D - 0.2f * bearing;
                    //mainParty.Position2D = newPos;
                    //mainParty.SetMoveGoToPoint(newPos);
                    //MBInformationManager.AddQuickInformation(new TextObject("I can't sail on land!"), 0, Hero.MainHero.CharacterObject);
                }
                //}
                //else if (mainParty.TargetParty != null && !mainParty.TargetParty.IsCurrentlyUsedByAQuest && IsPartyAtSea(mainParty.TargetParty) && !IsPartyAtSea(mainParty))
                //{
                //    mainParty.SetMoveGoToPoint(mainParty.Position2D);
                //    MBInformationManager.AddQuickInformation(new TextObject("How can I follow that party into the sea if I have no ships!?"), 0, Hero.MainHero.CharacterObject);
                //}
            }
        }

        public void SetMartitimeTravel(MaritimePathTravel traveler, MaritimeTravelEndDelegate endTravelDelegate = null)
        {
            CampaignCheats.SetMainPartyAttackable(new List<string>() { "0" });
            _currentMaritimePathTravel = traveler;
            if (endTravelDelegate != null)
                MaritimeTravelEnd = endTravelDelegate;
            else
                MaritimeTravelEnd = this.EndMaritimeTravel;
        }

        // default delegate funtions for ending tracked maritime travel
        private void EndMaritimeTravel()
        {
            Settlement settlement = _currentMaritimePathTravel.ToSettlement;
            CurrentMaritimePathTravel = null;
            EncounterManager.StartSettlementEncounter(MobileParty.MainParty, settlement);
        }

        /// <summary>
        /// Gets an appropriate ship prefab for the given mobile party.
        /// </summary>
        /// <param name="mobileParty"></param>
        /// <returns></returns>
        public static string GetShipPrefab(MobileParty mobileParty)
        {
            string prefabName = "";
            if (mobileParty.IsMainParty)
            {
                if (MaritimeManager.Instance.CurrentMaritimePathTravel != null)
                    prefabName = DefaultMaritimeTravelPrefab;
                else
                    prefabName = MobilePartyWrapper.MainPartyWrapper.CurrentFleet != null ? MobilePartyWrapper.MainPartyWrapper.CurrentFleet.Flagship.PrefabName : MaritimeManager.DefaultMerchantShipPrefab;
            }
            else
            {
                if (mobileParty.IsBandit)
                {
                    prefabName = DefaultBanditShipPrefab;
                }
                else
                {
                    CampaignShipTemplate template = CampaignShipTemplate.AllShipTemplates.Where(x => x.Cultures.Contains( mobileParty.Party.Culture)).GetRandomElementInefficiently();
                    prefabName = template != null ? template.BasePrefabName : MaritimeManager.DefaultMerchantShipPrefab;
                }
            }
            return prefabName;
        }

        /// <summary>
        /// Changes the party prefab to be one of our boats.
        /// </summary>
        /// <param name="mobileParty"></param>
        public static void UseBoatPrefab(MobileParty mobileParty, string prefabName = "elp_default_ship_icon")
        {
            List<GameEntity> children = ((PartyVisual)mobileParty.Party.Visuals).StrategicEntity.GetChildren().ToList();
            if (children.Count > 0)
                ((PartyVisual)mobileParty.Party.Visuals).StrategicEntity.RemoveAllChildren();
            Scene scene = ((PartyVisual)mobileParty.Party.Visuals).StrategicEntity.Scene;
            GameEntity gameEntity = GameEntity.Instantiate(scene, prefabName, true);
            MatrixFrame frame = MatrixFrame.Identity;
            frame.rotation.ApplyScaleLocal(1f);
            frame.Rotate(MBMath.HalfPI, Vec3.Up);
            gameEntity.SetFrame(ref frame);
            ((PartyVisual)mobileParty.Party.Visuals).StrategicEntity.AddChild(gameEntity, false);
            DisappearPartyVisuals(mobileParty);
        }

        /// <summary>
        /// Makes the normal party visuals invisible if they exists.
        /// </summary>
        /// <param name="mobileParty"></param>
        public static void DisappearPartyVisuals(MobileParty mobileParty)
        {
            if (((PartyVisual)mobileParty.Party.Visuals).HumanAgentVisuals != null)
            {
                ((PartyVisual)mobileParty.Party.Visuals).HumanAgentVisuals.GetEntity().SetVisibilityExcludeParents(false);
            }
            if (((PartyVisual)mobileParty.Party.Visuals).MountAgentVisuals != null)
            {
                ((PartyVisual)mobileParty.Party.Visuals).MountAgentVisuals.GetEntity().SetVisibilityExcludeParents(false);
            }
            if (((PartyVisual)mobileParty.Party.Visuals).CaravanMountAgentVisuals != null)
            {
                ((PartyVisual)mobileParty.Party.Visuals).CaravanMountAgentVisuals.GetEntity().SetVisibilityExcludeParents(false);
            }
        }

        /// <summary>
        /// Determines whether there is a water navmesh face between the points of a path.
        /// </summary>
        /// <param name="pathPoints">ordered list of points representing a path</param>
        /// <param name="minLength">minumum serach length</param>
        /// <returns></returns>
        public static bool IsWaterBetween(List<Vec2> pathPoints, float minLength)
        {
            bool isWater = false;
            for (int i = 0; i < pathPoints.Count - 1 && !isWater; i++)
            {
                Vec2 first = pathPoints[i];
                Vec2 second = pathPoints[i + 1];
                Vec2 diff = (second - first);
                Vec2 norm = diff.Normalized();
                float len = diff.Length;
                float start = 0f;
                while (start < len && !isWater)
                {
                    isWater = IsOverNavigableWater((start * norm) + first);
                    start += minLength;
                }
            }
            return isWater;
        }

        public static bool IsWaterBetween(PathFaceRecord index1, PathFaceRecord index2, Vec2 startPoint, Vec2 endPoint, float minLength)
        {
            NavigationPath path = new NavigationPath();
            Campaign.Current.MapSceneWrapper.GetPathBetweenAIFaces(index1, index2, startPoint, endPoint, 0.1f, path);
            List<Vec2> points = points = path.PathPoints.Where(x => x.IsNonZero()).ToList();
            points = points.Prepend(startPoint).ToList();
            return IsWaterBetween(points, minLength);
        }

        /// <summary>
        /// Determines whether there is a water navmesh face between the points of a path.
        /// </summary>
        /// <param name="pathPoints">ordered list of points representing a path</param>
        /// <param name="minLength">minumum serach length</param>
        /// <returns></returns>
        public static bool IsLandBetween(List<Vec2> pathPoints, float minLength)
        {
            bool isLand = false;
            for (int i = 0; i < pathPoints.Count - 1 && !isLand; i++)
            {
                Vec2 first = pathPoints[i];
                Vec2 second = pathPoints[i + 1];
                Vec2 diff = (second - first);
                Vec2 norm = diff.Normalized();
                float len = diff.Length;
                float start = 0f;
                while (start < len && !isLand)
                {
                    isLand = IsOverLand((start * norm) + first);
                    start += minLength;
                }
            }
            return isLand;
        }

        public static bool IsLandBetween(PathFaceRecord index1, PathFaceRecord index2, Vec2 startPoint, Vec2 endPoint, float minLength)
        {
            NavigationPath path = new NavigationPath();
            Campaign.Current.MapSceneWrapper.GetPathBetweenAIFaces(index1, index2, startPoint, endPoint, 0.1f, path);
            List<Vec2> points = points = path.PathPoints.Where(x => x.IsNonZero()).ToList();
            points = points.Prepend(startPoint).ToList();
            return IsLandBetween(points, minLength);
        }

        /// <summary>
        /// Determines whether a given MobileParty is 'at sea'.
        /// </summary>
        /// <param name="party"></param>
        /// <returns></returns>
        public bool IsPartyAtSea(MobileParty party)
        {
            return PartiesAtSea.Contains(party);
        }

        /// <summary>
        /// Removes a given MobileParty from being 'at sea'. Updates visuals, speed, and invokes 
        /// OnPartyLeaveSea events.
        /// </summary>
        /// <param name="party"></param>
        public void RemoveFromSea(MobileParty party)
        {
            OnBeforePartyLeaveSea(party);
            // remove ships for ai party
            if (!party.IsMainParty)
                RemoveFromSeaAI(party);

            _partiesAtSea.Remove(party);
            if (_aiPartyShipPrefabs.ContainsKey(party))
            {
                _aiPartyShipPrefabs.Remove(party);
            }

            ((PartyVisual)party.Party.Visuals).SetMapIconAsDirty();
            ExplainedNumber num = party.SpeedExplained;
            OnPartyLeaveSea(party);
        }

        /// <summary>
        /// Adds a given MobileParty to being 'at sea'. Updates visuals, speed, and invokes 
        /// OnPartyEnterSea events.
        /// </summary>
        /// <param name="party"></param>
        public void AddToSea(MobileParty party)
        {
            OnBeforePartyEnterSea(party);
            // add ships for ai party
            if (!party.IsMainParty)
                AddToSeaAI(party);

            _partiesAtSea.Add(party);
            string prefabName = GetShipPrefab(party);
            UseBoatPrefab(party, prefabName);
            if (!_aiPartyShipPrefabs.ContainsKey(party))
            {
                _aiPartyShipPrefabs.Add(party, prefabName);
            }
            else
            {
                _aiPartyShipPrefabs[party] = prefabName;
            }
            // recalculates party speed
            ExplainedNumber num = party.SpeedExplained;
            OnPartyEnterSea(party);
        }

        private void PartyEnterSea(MobileParty mobileParty)
        {
            if (mobileParty.IsMainParty)
            {

            }
        }

        private void PartyLeaveSea(MobileParty mobileParty)
        {
            if (MobileParty.MainParty.TargetParty == mobileParty)
            {

            }
        }

        private void AddToSeaAI(MobileParty party)
        {

        }

        private void RemoveFromSeaAI(MobileParty party)
        {

        }
    }
}
