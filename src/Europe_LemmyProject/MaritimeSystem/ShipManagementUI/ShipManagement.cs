using System;
using System.Collections.Generic;
using System.Linq;

using Helpers;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

using Europe_LemmyProject.General;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.GameMenus;
using static System.Net.Mime.MediaTypeNames;
using StoryMode.GauntletUI.Tutorial;

namespace Europe_LemmyProject.MaritimeSystem.ShipManagementUI
{
    /// <summary>
    /// Manages the launching of ShipManagementUIGauntlet
    /// </summary>
    public class ShipManagementUIManager
    {
        public static ShipManagementUIManager Instance { get; private set; }
        public Vec2 _zoomTo;
        public ShipManagementUIGauntlet _shipManagementUIGauntlet;
        private MenuCallbackArgs _menuCallbackArgs;

        public ShipManagementUIManager()
        {
            Instance = this;
        }

        public bool IsGoodState
        {
            get
            {
                MapScreen mapScreen = ScreenManager.TopScreen as MapScreen;
                return Game.Current != null &&
                       Game.Current.GameStateManager != null &&
                       Game.Current.GameStateManager.ActiveState != null &&
                       Game.Current.GameStateManager.ActiveState.GetType() == typeof(MapState) &&
                       !Game.Current.GameStateManager.ActiveState.IsMission &&
                       !Game.Current.GameStateManager.ActiveState.IsMenuState &&
                       mapScreen != null &&
                       !mapScreen.GetMapView<MapEncyclopediaView>().IsEncyclopediaOpen &&
                       _shipManagementUIGauntlet == null && 
                       Settlement.CurrentSettlement == null;
            }
        }

        public bool IsGoodInput
        {
            get
            {
                return MapScreen.Instance.Input.IsGameKeyDown(ELPHotKeys.MapOpenShipManagement);
            }
        }

        public void Tick(float dt)
        {
            if (IsGoodState && IsGoodInput)
            {
                Launch();
            }
        }

        public void Launch(List<Fleet> fleets = null, MenuCallbackArgs args = null)
        {
            _zoomTo = Vec2.Invalid;
            _shipManagementUIGauntlet = new ShipManagementUIGauntlet(fleets);
            _menuCallbackArgs = args;
        }

        public void CloseShipManagementUI()
        {
            _shipManagementUIGauntlet = null;
            if (_menuCallbackArgs != null)
                _menuCallbackArgs.MenuContext.Refresh();
            if (_zoomTo.IsValid)
            {
                MapScreen.Instance.FastMoveCameraToPosition(_zoomTo);
                _zoomTo = Vec2.Invalid;
            }
        }
    }

    /// <summary>
    /// GUI for managing fleets and ships. Based on the encyclopedia.
    /// </summary>
    public class ShipManagementUIGauntlet : MapView
    {
        private GauntletLayer _layer;
        private IGauntletMovie _gauntletMovie;
        private ShipManagementVM _datasource;
        private SpriteCategory _spriteCategory;
        private SpriteCategory _spriteCategory2;
        private Game _game;
        private List<Fleet> _fleets;

        public ShipManagementUIGauntlet(List<Fleet> fleets = null)
        {
            if (fleets != null)
                _fleets = fleets;
            else
                _fleets = MobilePartyWrapper.MainPartyWrapper.AllFleets;

            this.CreateLayout();
        }

        protected override void CreateLayout()
        {
            base.CreateLayout();
            SpriteData spriteData = UIResourceManager.SpriteData;
            this._spriteCategory = spriteData.SpriteCategories["ui_encyclopedia"];
            this._spriteCategory2 = spriteData.SpriteCategories["ui_clan"];
            this._spriteCategory.Load(UIResourceManager.ResourceContext, UIResourceManager.UIResourceDepot);
            this._spriteCategory2.Load(UIResourceManager.ResourceContext, UIResourceManager.UIResourceDepot);
            this._layer = new GauntletLayer(550, "GauntletLayer", false);
            this._layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericCampaignPanelsGameKeyCategory"));
            this._datasource = new ShipManagementVM(this.CloseShipManagement, _fleets);
            
            _gauntletMovie = this._layer.LoadMovie("ShipManagement", this._datasource);
            this._layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
            this._layer.IsFocusLayer = true;
            
            ScreenManager.TrySetFocus(this._layer);
            MapScreen.Instance.AddLayer(this._layer);
            this._game = Game.Current;
            this._game.GameStateManager.RegisterActiveStateDisableRequest(this);
            this._game.AfterTick = (Action<float>)Delegate.Combine(_game.AfterTick, new Action<float>(this.OnTick));
        }

        public void OnTick(float dt)
        {
            if (_layer.Input.IsKeyReleased(InputKey.Escape))
            {
                _datasource.ExecuteCloseShipManagement();
            }
        }

        protected override void OnFrameTick(float dt)
        {
            _datasource.OnTick(dt);
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();
        }

        protected override void OnFinalize()
        {
            this._game.AfterTick = (Action<float>)Delegate.Remove(_game.AfterTick, new Action<float>(this.OnTick));
            this._game = null;
            _datasource.OnFinalize();
            _datasource = null;
            base.OnFinalize();
        }

        public void CloseShipManagement()
        {
            this._layer.IsFocusLayer = false;
            this._layer.InputRestrictions.ResetInputRestrictions();
            MapScreen.Instance.RemoveLayer(this._layer);
            ScreenManager.TryLoseFocus(this._layer);
            Game.Current.GameStateManager.UnregisterActiveStateDisableRequest(this);
            this._game.AfterTick = (Action<float>)Delegate.Remove(_game.AfterTick, new Action<float>(this.OnTick));
            this._game = null;
            _datasource.OnFinalize();
            _layer.ReleaseMovie(this._gauntletMovie);
            _datasource = null;
            _spriteCategory.Unload();
            _spriteCategory2.Unload();
            _spriteCategory = null;
            _spriteCategory = null;
            _layer = null;

            ShipManagementUIManager.Instance.CloseShipManagementUI();
        }
    }

    /// <summary>
    /// ViewModel for presenting Fleet and CampaignShip information. Allows things like viewing 
    /// the stats of a Fleet and CampaignShip, renaming a CampaignShip, remotely selling a 
    /// CampaignShip, etc. 
    /// </summary>
    public class ShipManagementVM : ViewModel
    {
        private Action _closeShipManagement;
        private List<Fleet> _activeFleets;

        public Fleet SelectedFleet
        {
            get
            {
                return FleetSelector?.SelectedItem?.Fleet;
            }
        }

        public CampaignShip SelectedCampaignShip
        {
            get
            {
                return ShipSelector?.SelectedItem?.CampaignShip;
            }
        }

        #region Data Sources
        [DataSourceProperty]
        public string ZoomToFleetText
        {
            get
            {
                if (SelectedFleet != null)
                {
                    return "Zoom to fleet";
                }
                return "No Ships";
            }
        }

        private string _fleetLocationName;
        [DataSourceProperty]
        public string FleetLocationName
        {
            get
            {
                return this._fleetLocationName;
            }
            set
            {
                if (value != this._fleetLocationName)
                {
                    this._fleetLocationName = value;
                    base.OnPropertyChangedWithValue(value, "FleetLocationName");
                }
            }
        }

        public static string GetFleetLocationName(Fleet fleet)
        {
            TextObject name = TextObject.Empty;
            TextObject borrowed = TextObject.Empty;
            if (fleet.Settlement != null && fleet.Settlement.SettlementComponent is LandingArea)
            {
                Settlement nearest = SettlementHelper.FindNearestSettlementToPoint(fleet.GetPosition(), x => x.IsTown || x.IsVillage || x.IsCastle);
                if (nearest != null)
                {
                    name = new TextObject("{=ysPIspRlMB}Landing Area near {LOCATION_NAME}");
                    name.SetTextVariable("LOCATION_NAME", nearest.Name);
                }
                else
                    name = new TextObject("***There should be some settlement nearby!***");
            }
            else if (fleet.Settlement != null && fleet.Settlement.SettlementComponent is FerryTravelPoint)
            {
                Settlement nearest = SettlementHelper.FindNearestSettlementToPoint(fleet.GetPosition(), x => x.IsTown || x.IsVillage || x.IsCastle);
                if (nearest != null)
                {
                    name = new TextObject("{=YWq9TxH52f}Ferry Point near {LOCATION_NAME}"); // + nearest.Name.ToString();
                    name.SetTextVariable("LOCATION_NAME", nearest.Name);
                }
                else
                    name = new TextObject("***There should be some settlement nearby!***");
            }
            else
                name = fleet.GetLocationName();

            if (fleet.Owner != Hero.MainHero)
                borrowed = new TextObject("{=LEZAdztoPQ}(Borrowed)");

            TextObject textObject = TextObject.Empty;
            if (borrowed == TextObject.Empty)
                textObject = new TextObject("{FLEET_SELECTOR_NAME}");
            else
                textObject = new TextObject("{FLEET_SELECTOR_NAME} {BORROWED}");

            textObject.SetTextVariable("FLEET_SELECTOR_NAME", name);
            textObject.SetTextVariable("BORROWED", borrowed);
            return textObject.ToString();
        }

        private ShipViewModel _selectedShipView;
        [DataSourceProperty]
        public ShipViewModel ShipVisualModel
        {
            get
            {
                return this._selectedShipView;
            }
            set
            {
                if (value != this._selectedShipView)
                {
                    this._selectedShipView = value;
                    base.OnPropertyChangedWithValue(value, "ShipVisualModel");
                }
            }
        }


        private FleetViewModel _fleetVisualModel;
        [DataSourceProperty]
        public FleetViewModel FleetVisualModel
        {
            get
            {
                return this._fleetVisualModel;
            }
            set
            {
                if (value != this._fleetVisualModel)
                {
                    this._fleetVisualModel = value;
                    base.OnPropertyChangedWithValue(value, "FleetVisualModel");
                }
            }
        }

        private string _pageName = "Ship Management";
        [DataSourceProperty]
        public string PageName
        {
            get
            {
                return this._pageName;
            }
            set
            {
                if (value != this._pageName)
                {
                    this._pageName = value;
                    base.OnPropertyChangedWithValue(value, "PageName");
                }
            }
        }

        private SelectorVM<ManagedFleetItemVM> _fleetSelector;
        [DataSourceProperty]
        public SelectorVM<ManagedFleetItemVM> FleetSelector
        {
            get
            {
                return _fleetSelector;
            }
            set
            {
                if (value != this._fleetSelector)
                {
                    this._fleetSelector = value;
                    base.OnPropertyChangedWithValue(value, "FleetSelector");
                }
            }
        }

        private SelectorVM<ManagedShipItemVM> _shipSelector;
        [DataSourceProperty]
        public SelectorVM<ManagedShipItemVM> ShipSelector
        {
            get
            {
                return _shipSelector;
            }
            set
            {
                if (value != this._shipSelector)
                {
                    this._shipSelector = value;
                    base.OnPropertyChangedWithValue(value, "ShipSelector");
                }
            }
        }

        [DataSourceProperty]
        public bool DoesHaveFleets
        {
            get
            {
                return this._activeFleets != null && this._activeFleets.Count > 0;
            }
        }

        [DataSourceProperty]
        public bool DoesNotHaveFleets
        {
            get
            {
                return !DoesHaveFleets;
            }
        }

        private int _categoryIndex;
        [DataSourceProperty]
        public int CategoryIndex
        {
            get
            {
                return this._categoryIndex;
            }
            set
            {
                if (value != this._categoryIndex)
                {
                    this._categoryIndex = value;
                    base.OnPropertyChangedWithValue(value, "CategoryIndex");
                }
            }
        }

        private bool _playerCanChangeShipName;
        [DataSourceProperty]
        public bool PlayerCanChangeShipName
        {
            get
            {
                return this._playerCanChangeShipName;
            }
            set
            {
                if (value != this._playerCanChangeShipName)
                {
                    this._playerCanChangeShipName = value;
                    base.OnPropertyChangedWithValue(value, "PlayerCanChangeShipName");
                }
            }
        }

        private HintViewModel _changeShipNameHint;
        [DataSourceProperty]
        public HintViewModel ChangeShipNameHint
        {
            get
            {
                return this._changeShipNameHint;
            }
            set
            {
                if (value != this._changeShipNameHint)
                {
                    this._changeShipNameHint = value;
                    base.OnPropertyChangedWithValue(value, "ChangeShipNameHint");
                }
            }
        }

        private MBBindingList<MaritimeConceptVM> _maritimeConcepts;
        [DataSourceProperty]
        public MBBindingList<MaritimeConceptVM> MaritimeConcepts
        {
            get
            {
                return this._maritimeConcepts;
            }
            set
            {
                if (value != this._maritimeConcepts)
                {
                    this._maritimeConcepts = value;
                    base.OnPropertyChangedWithValue(value, "MaritimeConcepts");
                }
            }
        }
        #endregion

        public string DefaultShipPrefabName = "bd_barrel_a";

        public ShipManagementVM(Action closeShipManagement, List<Fleet> activeFleets)
        {
            this._closeShipManagement = closeShipManagement;
            this._activeFleets = activeFleets;

            if (activeFleets.Count != 0)
            {
                this.FleetSelector = new SelectorVM<ManagedFleetItemVM>(0, new Action<SelectorVM<ManagedFleetItemVM>>(this.OnFleetSelection));
                this.ShipSelector = new SelectorVM<ManagedShipItemVM>(0, new Action<SelectorVM<ManagedShipItemVM>>(this.OnShipSelection));
            }

            MaritimeConcepts = new MBBindingList<MaritimeConceptVM>();
            foreach (Tuple<string, string> pair in MaritimeManager.Instance.ConceptManager.Concepts)
            {
                MaritimeConcepts.Add(new MaritimeConceptVM(pair.Item1, pair.Item2));
            }

            this.RefreshValues();
        }

        public override void RefreshValues()
        {
            this.SetFlagshipHint = new BasicTooltipViewModel(this.GetSetFlagshipHint);
            this.TransferShipHint = new BasicTooltipViewModel(this.GetTransferShipHint);
            this.RepairShipHint = new BasicTooltipViewModel(this.GetRepairShipHint);
            this.SellShipHint = new BasicTooltipViewModel(this.GetSellShipHint);
            this.UpgradeShipHint = new BasicTooltipViewModel(this.GetUpgradeShipHint);
            this.ScrapShipHint = new BasicTooltipViewModel(this.GetScrapShipHint);

            this.TransportFleetHint = new BasicTooltipViewModel(this.GetTransportFleetTooltip);
            this.RepairFleetHint = new BasicTooltipViewModel(this.GetRepairFleetTooltip);
            this.SellFleetHint = new BasicTooltipViewModel(this.GetSellFleetTooltip);
            this.MergeFleetHint = new BasicTooltipViewModel(this.GetMergeFleetTooltip);
            this.ScrapFleetHint = new BasicTooltipViewModel(this.GetScrapFleetTooltip);
            this.RenameFleetHint = new BasicTooltipViewModel(this.GetRenameFleetTooltip);

            base.OnPropertyChanged("DoesHaveFleets");
            base.OnPropertyChanged("DoesNotHaveFleets");
            base.RefreshValues();
            if (DoesHaveFleets)
            {
                // refresh selected items, but make sure selected indices are non-negative
                int fleetIndex = FleetSelector.SelectedIndex > -1 ? FleetSelector.SelectedIndex : 0;
                int shipIndex = ShipSelector.SelectedIndex > -1 ? ShipSelector.SelectedIndex : 0;

                this.FleetSelector.ItemList = new MBBindingList<ManagedFleetItemVM>();
                this.ShipSelector.ItemList.Clear();
                if (_activeFleets != null && _activeFleets.Count > 0)
                {
                    foreach (Fleet fleet in _activeFleets)
                    {
                        this.FleetSelector.AddItem(new ManagedFleetItemVM(fleet));
                    }
                    this.FleetSelector.SelectedIndex = fleetIndex < _activeFleets.Count ? fleetIndex : 0;
                    this.FleetSelector.SelectedItem = this.FleetSelector.GetCurrentItem();
                    this.FleetVisualModel = new FleetViewModel(SelectedFleet);

                    this.ShipSelector.SelectedIndex = shipIndex < SelectedFleet.Ships.Count ? shipIndex : 0;
                    this.ShipSelector.SelectedItem = this.ShipSelector.GetCurrentItem();
                    this.ShipVisualModel = new ShipViewModel(SelectedCampaignShip);
                }
                else
                {
                    this.ShipVisualModel = new ShipViewModel();
                    this.ShipVisualModel.PrefabName = DefaultShipPrefabName;
                }
                CategoryIndex = 0;
            }
            else
                CategoryIndex = 1;
        }

        private void OnFleetSelection(SelectorVM<ManagedFleetItemVM> selector)
        {
            if (selector.ItemList.Count > 0)
            {
                FleetVisualModel = new FleetViewModel(SelectedFleet);
                ShipSelector.ItemList.Clear();
                foreach (CampaignShip ship in SelectedFleet.Ships)
                {
                    this.ShipSelector.AddItem(new ManagedShipItemVM(ship, SelectedFleet));
                }
                ShipSelector.SelectedIndex = 0;
                FleetLocationName = GetFleetLocationName(SelectedFleet);
            }
        }

        private void OnShipSelection(SelectorVM<ManagedShipItemVM> selector)
        {
            if (selector.ItemList.Count > 0)
            {
                ShipVisualModel = new ShipViewModel(SelectedCampaignShip);
            }
        }

        #region Ship Actions

        #region Set Flagship
        [DataSourceProperty]
        public string SetFlagshipText
        {
            get
            {
                return new TextObject("{=wOx0pcVh3u}Set As Flagship").ToString();
            }
        }

        private BasicTooltipViewModel _setFlagshipHint;
        [DataSourceProperty]
        public BasicTooltipViewModel SetFlagshipHint
        {
            get
            {
                return this._setFlagshipHint;
            }
            set
            {
                if (value != this._setFlagshipHint)
                {
                    this._setFlagshipHint = value;
                    base.OnPropertyChangedWithValue(value, "SetFlagshipHint");
                }
            }
        }

        [DataSourceProperty]
        public bool IsSetFlagshipEnabled
        {
            get
            {
                try
                {
                    return SelectedFleet.Owner == Hero.MainHero; // player owns the fleet
                }
                catch
                {
                    return false;
                }
            }
        }

        public List<TooltipProperty> GetSetFlagshipHint()
        {
            List<TooltipProperty> tooltips = new List<TooltipProperty>();
            // disabling conditions first
            if (SelectedFleet.Owner != Hero.MainHero)
            {
                TextObject textObject = new TextObject("{=48ZRT5ZRVL}Must own this fleet set its flagship");
                tooltips.Add(new TooltipProperty(textObject.ToString(), "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
            }

            if (tooltips.Count == 0)
            {
                TextObject textObject = new TextObject("{=4ILHItNqwN}Set this ship as the flagship for the current fleet");
                tooltips.Add(new TooltipProperty(textObject.ToString(), "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
            }
            return tooltips;
        }

        public void ExecuteSetFlagship()
        {
            if (!SelectedFleet.IsFlagship(SelectedCampaignShip))
            {
                SelectedFleet.Flagship = SelectedCampaignShip;
                if (MaritimeManager.Instance.IsPartyAtSea(MobileParty.MainParty))
                {
                    string prefabName = MaritimeManager.GetShipPrefab(MobileParty.MainParty);
                    MaritimeManager.UseBoatPrefab(MobileParty.MainParty, prefabName);
                }

                ShipSelector.SelectedIndex = 0;
                RefreshValues();
            }
        }
        #endregion

        #region Transfer Ship
        [DataSourceProperty]
        public string TransferShipText
        {
            get
            {
                return new TextObject("{=acLim8eKQ3}Transfer to other fleet").ToString();
            }
        }

        private BasicTooltipViewModel _transferShipHint;
        [DataSourceProperty]
        public BasicTooltipViewModel TransferShipHint
        {
            get
            {
                return this._transferShipHint;
            }
            set
            {
                if (value != this._transferShipHint)
                {
                    this._transferShipHint = value;
                    base.OnPropertyChangedWithValue(value, "TransferShipHint");
                }
            }
        }

        [DataSourceProperty]
        public bool IsTransferShipEnabled
        {
            get
            {
                try
                {
                    return SelectedFleet.Owner == Hero.MainHero; // player owns the fleet
                }
                catch
                {
                    return false;
                }
            }
        }

        public List<TooltipProperty> GetTransferShipHint()
        {
            List<TooltipProperty> tooltips = new List<TooltipProperty>();
            // disabling conditions first
            if (SelectedFleet.Owner != Hero.MainHero)
            {
                TextObject textObject = new TextObject("{=5SA7iXDmC5}Must own this ship in order to transfer it to antother fleet");
                tooltips.Add(new TooltipProperty(textObject.ToString(), "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
            }

            if (tooltips.Count == 0)
            {
                TextObject textObject = new TextObject("{=CO9pNjWyQj}Transfer this ship to another fleet in this port");
                tooltips.Add(new TooltipProperty(textObject.ToString(), "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
            }
            return tooltips;
        }

        public void ExecuteTransferShip()
        {
            if (IsTransferShipEnabled)
            {
                List<InquiryElement> elements = _activeFleets
                                            .Where(x => x != SelectedFleet)
                                            .Select(x => new InquiryElement(
                                                x,
                                                x.Name.ToString(),
                                                null,
                                                true,
                                                Fleet.GetFleetInfo(x, "ExecuteTransferShip").ToStringAndRelease()
                                                ))
                                            .ToList();

                // add option to create new fleet
                elements.Add(new InquiryElement(
                    null,
                    new TextObject("Create New Fleet").ToString(),
                    null,
                    true,
                    string.Empty));

                MBInformationManager.ShowMultiSelectionInquiry(
                    new MultiSelectionInquiryData(
                        new TextObject("").ToString(),
                        string.Empty,
                        elements,
                        true,
                        1,
                        GameTexts.FindText("str_done", null).ToString(),
                        GameTexts.FindText("str_cancel", null).ToString(),
                        delegate (List<InquiryElement> elements)
                        {
                            SelectedFleet.RemoveShip(SelectedCampaignShip);
                            if (SelectedFleet.Count == 0)
                            {
                                MobilePartyWrapper.MainPartyWrapper.DestroyStoredFleet(SelectedFleet);
                                _activeFleets.Remove(SelectedFleet);
                            }

                            Fleet fleet = elements.First().Identifier as Fleet;
                            if (fleet != null)
                            {
                                fleet.AddShip(SelectedCampaignShip);
                                this.RefreshValues();
                            }
                            else
                            {
                                TransferShipCreateNewFleet();
                            }
                        },
                        null
                        )
                    );
            }
        }

        private void TransferShipCreateNewFleet()
        {
            Fleet fleet;
            MobilePartyWrapper.MainPartyWrapper.CreateNewFleet(
                new List<CampaignShip>() { SelectedCampaignShip }, 
                Settlement.CurrentSettlement, 
                out fleet
                );
            GameTexts.SetVariable("MAX_LETTER_COUNT", CampaignShip.ShipNameCharLimit);
            GameTexts.SetVariable("MIN_LETTER_COUNT", 1);
            InformationManager.ShowTextInquiry(
                new TextInquiryData(
                    new TextObject("New Fleet Name").ToString(),
                    string.Empty,
                    true,
                    true,
                    GameTexts.FindText("str_done", null).ToString(),
                    GameTexts.FindText("str_cancel", null).ToString(),
                    delegate (string str)
                    {
                        fleet.Name = new TextObject(str, null);
                        _activeFleets.Add(fleet);
                        this.RefreshValues();
                    },
                    null,
                    false,
                    CampaignShip.IsShipNameValid,
                    "",
                    SelectedFleet.Name.ToString()
                    )
                );
        }
        #endregion

        #region Repair Ship
        [DataSourceProperty]
        public string RepairShipText
        {
            get
            {
                return new TextObject("{=Mm8NRvpRfj}Repair").ToString();
            }
        }

        private BasicTooltipViewModel _repairShipHint;
        [DataSourceProperty]
        public BasicTooltipViewModel RepairShipHint
        {
            get
            {
                return this._repairShipHint;
            }
            set
            {
                if (value != this._repairShipHint)
                {
                    this._repairShipHint = value;
                    base.OnPropertyChangedWithValue(value, "RepairShipHint");
                }
            }
        }

        [DataSourceProperty]
        public bool IsRepairShipEnabled
        {
            get
            {
                try
                {
                    return SelectedFleet.Settlement == MobileParty.MainParty.CurrentSettlement
                    || SelectedFleet.MobileParty == MobileParty.MainParty;
                }
                catch
                {
                    return false;
                }
                
            }
        }

        public List<TooltipProperty> GetRepairShipHint()
        {
            List<TooltipProperty> tooltips = new List<TooltipProperty>();

            if (!IsRepairShipEnabled)
            {
                TextObject textObject = new TextObject("{=apL4ByXTNt}Must be in the same location as this ship to repair it");
                tooltips.Add(new TooltipProperty(textObject.ToString(), "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
            }

            if(tooltips.Count == 0)
            {
                if (SelectedFleet.Settlement != null)
                {
                    TextObject textObject = new TextObject("{=cPK6QQV4hj}Have this ship repaired by the shipwrights at the port");
                    tooltips.Add(new TooltipProperty(textObject.ToString(), "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
                }
                else if (SelectedFleet.MobileParty != null)
                {
                    TextObject textObject = new TextObject("{=rX8gP9U9ma}Have this ship repaired by your party's engineer");
                    tooltips.Add(new TooltipProperty(textObject.ToString(), "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
                }
            }

            return tooltips;
        }

        public void ExecuteRepairSelectedShip()
        {
            MBInformationManager.AddQuickInformation(new TextObject("{=3t032DZwQT}Coming Soon (TM)..."), 0, null);
            // if party is at sea, rely on engineer to repair the ship
            // maybe require the process to require both gold and wood or other resources
            if (MobilePartyWrapper.MainPartyWrapper.IsAtSea)
            {

            }
            else
            {

            }
        }

        #endregion

        #region Sell Ship
        [DataSourceProperty]
        public string SellShipText
        {
            get
            {
                return new TextObject("{=nO4ZTTfFbI}Sell").ToString();
            }
        }

        private BasicTooltipViewModel _sellShipHint;
        [DataSourceProperty]
        public BasicTooltipViewModel SellShipHint
        {
            get
            {
                return this._sellShipHint;
            }
            set
            {
                if (value != this._sellShipHint)
                {
                    this._sellShipHint = value;
                    base.OnPropertyChangedWithValue(value, "SellShipHint");
                }
            }
        }

        [DataSourceProperty]
        public bool IsSellShipEnabled
        {
            get
            {
                try
                {
                    return SelectedFleet.Owner == Hero.MainHero // player owns the fleet
                           && MobileParty.MainParty.CurrentSettlement != null // player is in a settlement
                           && SelectedFleet.Settlement != null // fleet is in a settlement
                           && SelectedFleet.Settlement == MobileParty.MainParty.CurrentSettlement // fleet and player are in the same settlement
                           && PortCampaignBehavior.IsPortSettlement(SelectedFleet.Settlement); // the settlement is a port settlement
                }
                catch
                {
                    return false;
                }
            }
        }

        public List<TooltipProperty> GetSellShipHint()
        {
            List<TooltipProperty> tooltips = new List<TooltipProperty>();

            if (SelectedFleet.Owner != Hero.MainHero)
            {
                TextObject textObject = new TextObject("{=RrcH49Sebi}Must own this ship in order to sell it");
                tooltips.Add(new TooltipProperty(textObject.ToString(), "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
            }
            if (MobileParty.MainParty.CurrentSettlement == null
                || SelectedFleet.Settlement == null
                || SelectedFleet.Settlement != MobileParty.MainParty.CurrentSettlement
                || !PortCampaignBehavior.IsPortSettlement(SelectedFleet.Settlement))
            {
                TextObject textObject = new TextObject("{=l9FDoTKqcq}Must be in the same Port Town as this ship in order to sell it");
                tooltips.Add(new TooltipProperty(textObject.ToString(), "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
            }

            return tooltips;
        }

        public void ExecuteSellSelectedShip()
        {
            // require the ship to be in a port in order to sell it
            if (SelectedFleet.Settlement != null && PortCampaignBehavior.IsPortSettlement(SelectedFleet.Settlement))
            {
                int value = DefaultShipEconomnyModel.GetShipValue(SelectedCampaignShip);
                TextObject message = new TextObject("Sell " + SelectedCampaignShip.Name + " for " + value + " ?");
                TextObject title = new TextObject("Sell Ship");
                InformationManager.ShowInquiry(
                    new InquiryData(
                        title.ToString(), 
                        message.ToString(), 
                        true, 
                        true, 
                        new TextObject("{=aeouhelq}Yes", null).ToString(), 
                        new TextObject("{=8OkPHu4f}No", null).ToString(),
                        delegate ()
                        {
                            GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, value, false);
                            SelectedFleet.RemoveShip(SelectedCampaignShip);
                            if (SelectedFleet.Count == 0)
                            {
                                MobilePartyWrapper.MainPartyWrapper.DestroyStoredFleet(SelectedFleet);
                                _activeFleets.Remove(SelectedFleet);
                            }
                            RefreshValues();
                        },
                        delegate ()
                        {

                        })
                    );
            }
            else
            {
                MBInformationManager.AddQuickInformation(new TextObject("{=munt1nWgs6}Sell my ships to who? The fleet is not at port."), 0, Hero.MainHero.CharacterObject);
            }
        }

        #endregion

        #region Upgrade Ship Hint
        [DataSourceProperty]
        public string UpgradeShipText
        {
            get
            {
                return new TextObject("{=y897yqH8uh}Upgrade").ToString();
            }
        }

        private BasicTooltipViewModel _upgradeShipHint;
        [DataSourceProperty]
        public BasicTooltipViewModel UpgradeShipHint
        {
            get
            {
                return this._upgradeShipHint;
            }
            set
            {
                if (value != this._upgradeShipHint)
                {
                    this._upgradeShipHint = value;
                    base.OnPropertyChangedWithValue(value, "UpgradeShipHint");
                }
            }
        }

        [DataSourceProperty]
        public bool IsUpgradeShipEnabled
        {
            get
            {
                try
                {
                    return SelectedFleet.Owner == Hero.MainHero // player owns the ship
                       &&
                       (
                           // either the player and ship are in the same settlement
                           (MobileParty.MainParty.CurrentSettlement != null // player is in a settlement
                           && SelectedFleet.Settlement != null // ship is in a settlement
                           && SelectedFleet.Settlement == MobileParty.MainParty.CurrentSettlement // ship and player are in the same settlement
                                                                                                  // && PortCampaignBehavior.IsPortSettlement(SelectedFleet.Settlement) // the settlement is a port settlement
                           )
                           ||
                           MobilePartyWrapper.MainPartyWrapper.CurrentFleet == SelectedFleet // or fleet is with the player
                       );
                }
                catch
                {
                    return false;
                }
            }
        }

        public List<TooltipProperty> GetUpgradeShipHint()
        {
            List<TooltipProperty> tooltips = new List<TooltipProperty>();

            bool ownsShip = SelectedFleet.Owner == Hero.MainHero;
            if (!ownsShip)
            {
                TextObject textObject = new TextObject("{=kSvOJ5x42M}Must own this ship in order to upgrade it");
                tooltips.Add(new TooltipProperty(textObject.ToString(), "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
            }

            bool sameLocation = (MobileParty.MainParty.CurrentSettlement != null // player is in a settlement
                           && SelectedFleet.Settlement != null // ship is in a settlement
                           && SelectedFleet.Settlement == MobileParty.MainParty.CurrentSettlement // ship and player are in the same settlement
                                                                                                  // && PortCampaignBehavior.IsPortSettlement(SelectedFleet.Settlement) // the settlement is a port settlement
                           )
                           ||
                           MobilePartyWrapper.MainPartyWrapper.CurrentFleet == SelectedFleet; // or fleet is with the player;
            if (!sameLocation)
            {
                TextObject textObject = new TextObject("{=X5xtFaUMug}Must be in the same location as this ship in order to upgrade it");
                tooltips.Add(new TooltipProperty(textObject.ToString(), "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
            }

            return tooltips;
        }

        public void ExecuteUpgradeSelectedShip()
        {
            MBInformationManager.AddQuickInformation(new TextObject("{=3t032DZwQT}Coming Soon (TM)..."), 0, null);
            // if party is at sea, rely on engineer to upgrade the ship
            // maybe require the process to require both gold and wood or other resources
            if (MobilePartyWrapper.MainPartyWrapper.IsAtSea)
            {

            }
            else // the upgrade can be done in port
            {

            }
        }
        #endregion

        #region Scrap Ship
        [DataSourceProperty]
        public string ScrapShipText
        {
            get
            {
                return new TextObject("{=3uqIRzlYRd}Scrap").ToString();
            }
        }

        private BasicTooltipViewModel _scrapShipHint;
        [DataSourceProperty]
        public BasicTooltipViewModel ScrapShipHint
        {
            get
            {
                return this._scrapShipHint;
            }
            set
            {
                if (value != this._scrapShipHint)
                {
                    this._scrapShipHint = value;
                    base.OnPropertyChangedWithValue(value, "ScrapShipHint");
                }
            }
        }

        [DataSourceProperty]
        public bool IsScrapShipEnabled
        {
            get
            {
                try
                {
                    return SelectedFleet.Owner == Hero.MainHero // player owns the ship
                           &&
                           (
                               // either the player and ship are in the same settlement
                               (MobileParty.MainParty.CurrentSettlement != null // player is in a settlement
                               && SelectedFleet.Settlement != null // ship is in a settlement
                               && SelectedFleet.Settlement == MobileParty.MainParty.CurrentSettlement // ship and player are in the same settlement
                                                                                                      // && PortCampaignBehavior.IsPortSettlement(SelectedFleet.Settlement) // the settlement is a port settlement
                               )
                               ||
                               MobilePartyWrapper.MainPartyWrapper.CurrentFleet == SelectedFleet // or fleet is with the player
                           );
                }
                catch
                {
                    return false;
                }
            }
        }

        public List<TooltipProperty> GetScrapShipHint()
        {
            List<TooltipProperty> tooltips = new List<TooltipProperty>();

            bool ownsShip = SelectedFleet.Owner == Hero.MainHero;
            if (!ownsShip)
            {
                TextObject textObject = new TextObject("{=SL69VVJXtA}Must own this ship in order to scrap it");
                tooltips.Add(new TooltipProperty(textObject.ToString(), "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
            }

            bool sameLocation = (MobileParty.MainParty.CurrentSettlement != null // player is in a settlement
                           && SelectedFleet.Settlement != null // ship is in a settlement
                           && SelectedFleet.Settlement == MobileParty.MainParty.CurrentSettlement // ship and player are in the same settlement
                                                                                                  // && PortCampaignBehavior.IsPortSettlement(SelectedFleet.Settlement) // the settlement is a port settlement
                           )
                           ||
                           MobilePartyWrapper.MainPartyWrapper.CurrentFleet == SelectedFleet; // or fleet is with the player;
            if (!sameLocation)
            {
                TextObject textObject = new TextObject("{=Sd6X8VnvvM}Must be in the same location as this ship in order to scrap it");
                tooltips.Add(new TooltipProperty(textObject.ToString(), "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
            }

            return tooltips;
        }

        public void ExecuteScrapSelectedShip()
        {
            MBInformationManager.AddQuickInformation(new TextObject("{=3t032DZwQT}Coming Soon (TM)..."), 0, null);
            // if party is at sea, return less resources than if the ship is in port
            // since it would be hard to take a ship apart completely when it is floating
            if (MobilePartyWrapper.MainPartyWrapper.IsAtSea)
            {

            }
            else // the ship can mostly be scrapped in a port
            {

            }
        }
        #endregion

        #endregion

        #region Fleet Actions

        #region Rename Fleet
        [DataSourceProperty]
        public string RenameFleetText
        {
            get
            {
                return new TextObject("{=RrRf8YMA6F}Rename Fleet").ToString();
            }
        }

        [DataSourceProperty]
        public bool IsRenameFleetEnabled
        {
            get
            {
                try
                {
                    return SelectedFleet.Owner == Hero.MainHero;
                }
                catch
                {
                    return false;
                }
            }
        }

        private BasicTooltipViewModel _renameFleetHint;
        [DataSourceProperty]
        public BasicTooltipViewModel RenameFleetHint
        {
            get
            {
                return this._renameFleetHint;
            }
            set
            {
                if (value != this._renameFleetHint)
                {
                    this._renameFleetHint = value;
                    base.OnPropertyChangedWithValue(value, "RenameFleetHint");
                }
            }
        }

        public List<TooltipProperty> GetRenameFleetTooltip()
        {
            List<TooltipProperty> tooltips = new List<TooltipProperty>();
            if (!IsRenameFleetEnabled)
            {
                tooltips.Add(new TooltipProperty("Cannot rename a fleet you do not own.", "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
                return tooltips;
            }
            return tooltips;
        }

        public void ExecuteRenameSelectedFleet()
        {
            if (IsRenameFleetEnabled)
            {
                GameTexts.SetVariable("MAX_LETTER_COUNT", CampaignShip.ShipNameCharLimit);
                GameTexts.SetVariable("MIN_LETTER_COUNT", 1);
                InformationManager.ShowTextInquiry(
                    new TextInquiryData(
                        new TextObject("New Fleet Name").ToString(),
                        string.Empty,
                        true,
                        true,
                        GameTexts.FindText("str_done", null).ToString(),
                        GameTexts.FindText("str_cancel", null).ToString(),
                        delegate (string str)
                        {
                            SelectedFleet.Name = new TextObject(str, null);
                            //MBTextManager.SetTextVariable("MANAGED_SHIP_NAME", SelectedCampaignShip.Name);
                            this.RefreshValues();
                        },
                        null,
                        false,
                        CampaignShip.IsShipNameValid,
                        "",
                        SelectedFleet.Name.ToString()
                        )
                    );
            }
        }
        #endregion

        #region Merge Fleet
        [DataSourceProperty]
        public string MergeFleetText
        {
            get
            {
                return new TextObject("Merge Fleet").ToString();
            }
        }

        private BasicTooltipViewModel _mergeFleetHint;
        [DataSourceProperty]
        public BasicTooltipViewModel MergeFleetHint
        {
            get
            {
                return this._mergeFleetHint;
            }
            set
            {
                if (value != this._mergeFleetHint)
                {
                    this._mergeFleetHint = value;
                    base.OnPropertyChangedWithValue(value, "MergeFleetHint");
                }
            }
        }

        [DataSourceProperty]
        public bool IsMergeFleetEnabled
        {
            get
            {
                try
                {
                    return SelectedFleet.Owner == Hero.MainHero
                       && MobileParty.MainParty.CurrentSettlement != null
                       && SelectedFleet.Settlement == MobileParty.MainParty.CurrentSettlement
                       && _activeFleets.Any(x => x != SelectedFleet && x.Settlement == SelectedFleet.Settlement);
                }
                catch
                {
                    return false;
                }
            }
        }

        public List<TooltipProperty> GetMergeFleetTooltip()
        {
            List<TooltipProperty> tooltips = new List<TooltipProperty>();
            if (SelectedFleet.Owner != Hero.MainHero)
            {
                TextObject textObject = new TextObject("{=5p9LR1ULm7}Must own this fleet in order to merge it");
                tooltips.Add(new TooltipProperty(textObject.ToString(), "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
            }
            if (MobileParty.MainParty.CurrentSettlement == null)
            {
                TextObject textObject = new TextObject("{=Sfmy41fp8d}Must be in a settlement to merge fleets");
                tooltips.Add(new TooltipProperty(textObject.ToString(), "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
            }
            if (SelectedFleet.Settlement != MobileParty.MainParty.CurrentSettlement)
            {
                TextObject textObject = new TextObject("{=5RBPGvFSjN}Must be in same settlement as this fleet to merge with another");
                tooltips.Add(new TooltipProperty(textObject.ToString(), "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
            }
            if (!_activeFleets.Any(x => x != SelectedFleet && x.Settlement == SelectedFleet.Settlement))
            {
                TextObject textObject = new TextObject("{=qW2qW6sKWl}There must be another fleet in this settlement to merge with this fleet");
                tooltips.Add(new TooltipProperty(textObject.ToString(), "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
            }
            return tooltips;
        }

        public void ExecuteMergeSelectedFleet()
        {
            if (IsMergeFleetEnabled)
            {
                List<InquiryElement> elements = _activeFleets
                                            .Where(x => x != SelectedFleet)
                                            .Select(x => new InquiryElement(
                                                x,
                                                x.Name.ToString(),
                                                null,
                                                true,
                                                Fleet.GetFleetInfo(x, "ExecuteMergeSelectedFleet").ToStringAndRelease()
                                                ))
                                            .ToList();

                MBInformationManager.ShowMultiSelectionInquiry(
                    new MultiSelectionInquiryData(
                        new TextObject("").ToString(),
                        string.Empty,
                        elements,
                        true,
                        1,
                        GameTexts.FindText("str_done", null).ToString(),
                        GameTexts.FindText("str_cancel", null).ToString(),
                        delegate (List<InquiryElement> elements)
                        {
                            Fleet fleet = elements.First().Identifier as Fleet;
                            MobilePartyWrapper.MainPartyWrapper.MergeFleets(fleet, SelectedFleet);
                            MobilePartyWrapper.MainPartyWrapper.DestroyFleet(fleet);
                            this.RefreshValues();
                        },
                        null
                        )
                    );
            }
        }
        #endregion

        #region Repair Fleet
        [DataSourceProperty]
        public string RepairFleetText
        {
            get
            {
                return new TextObject("{=mzK6zk2NLU}Repair All Ships").ToString();
            }
        }

        private BasicTooltipViewModel _repairFleetHint;
        [DataSourceProperty]
        public BasicTooltipViewModel RepairFleetHint
        {
            get
            {
                return this._repairFleetHint;
            }
            set
            {
                if (value != this._repairFleetHint)
                {
                    this._repairFleetHint = value;
                    base.OnPropertyChangedWithValue(value, "RepairFleetHint");
                }
            }
        }

        public List<TooltipProperty> GetRepairFleetTooltip()
        {
            List<TooltipProperty> tooltips = new List<TooltipProperty>();
            TextObject textObject = new TextObject("{=GUOvTUqX8u}Repair all ships in this fleet");
            tooltips.Add(new TooltipProperty(textObject.ToString(), "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
            return tooltips;
        }

        public void ExecuteRepairSelectedFleet()
        {
            MBInformationManager.AddQuickInformation(new TextObject("{=3t032DZwQT}Coming Soon (TM)..."), 0, null);
            // repair each ship in the selected fleet
        }
        #endregion

        #region Sell Fleet
        [DataSourceProperty]
        public string SellFleetText
        {
            get
            {
                return new TextObject("{=yq7R21qTS0}Sell Fleet").ToString();
            }
        }

        private BasicTooltipViewModel _sellFleetHint;
        [DataSourceProperty]
        public BasicTooltipViewModel SellFleetHint
        {
            get
            {
                return this._sellFleetHint;
            }
            set
            {
                if (value != this._sellFleetHint)
                {
                    this._sellFleetHint = value;
                    base.OnPropertyChangedWithValue(value, "SellFleetHint");
                }
            }
        }

        [DataSourceProperty]
        public bool IsSellFleetEnabled
        {
            get
            {
                try
                {
                    return SelectedFleet.Owner == Hero.MainHero // player owns the fleet
                       && MobileParty.MainParty.CurrentSettlement != null // player is in a settlement
                       && SelectedFleet.Settlement != null // fleet is in a settlement
                       && SelectedFleet.Settlement == MobileParty.MainParty.CurrentSettlement // fleet and player are in the same settlement
                       && PortCampaignBehavior.IsPortSettlement(SelectedFleet.Settlement); // the settlement is a port settlement
                }
                catch
                {
                    return false;
                }
            }
        }

        public List<TooltipProperty> GetSellFleetTooltip()
        {
            List<TooltipProperty> tooltips = new List<TooltipProperty>();
            // first check for disabling conditions
            if (SelectedFleet.Owner != Hero.MainHero)
            {
                TextObject textObject = new TextObject("{=eriA5gzua5}Must own this fleet in order to sell it");
                tooltips.Add(new TooltipProperty(textObject.ToString(), "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
            }
            if (MobileParty.MainParty.CurrentSettlement == null 
                || SelectedFleet.Settlement == null
                || SelectedFleet.Settlement != MobileParty.MainParty.CurrentSettlement
                || !PortCampaignBehavior.IsPortSettlement(SelectedFleet.Settlement))
            {
                TextObject textObject = new TextObject("{=L1joeYXR3W}Must be in the same Port Town as the fleet in order to sell it");
                tooltips.Add(new TooltipProperty(textObject.ToString(), "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
            }

            // if no disabling conditions are met, add the positive tooltip hint
            if (tooltips.Count == 0)
            {
                TextObject textObject = new TextObject("{=rHTm1pcQm6}Sell every ship in this fleet");
                tooltips.Add(new TooltipProperty(textObject.ToString(), "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
            }

            return tooltips;
        }

        public void ExecuteSellSelectedFleet()
        {
            // sell each ship in the selected fleet and destroy the fleet
            if (IsSellFleetEnabled)
            {
                int value = SelectedFleet.Ships.Sum(ship => DefaultShipEconomnyModel.GetShipValue(ship));
                TextObject message = new TextObject("Sell current fleet for " + value + " ?");
                TextObject title = new TextObject("Sell Ship");
                InformationManager.ShowInquiry(
                    new InquiryData(
                        title.ToString(), 
                        message.ToString(), 
                        true, 
                        true, 
                        new TextObject("{=aeouhelq}Yes", null).ToString(), 
                        new TextObject("{=8OkPHu4f}No", null).ToString(),
                        delegate ()
                        {
                            GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, value, false);
                            List<CampaignShip> ships = SelectedFleet.Ships;
                            for (int i = 0; i < ships.Count; i++)
                            {
                                SelectedFleet.RemoveShip(SelectedCampaignShip);
                            }
                            MobilePartyWrapper.MainPartyWrapper.DestroyStoredFleet(SelectedFleet);
                            _activeFleets.Remove(SelectedFleet);
                            RefreshValues();
                        },
                        delegate ()
                        {

                        }
                    )
                );
            }
        }
        #endregion

        #region Scrap Fleet
        [DataSourceProperty]
        public string ScrapFleetText
        {
            get
            {
                return "Scrap Entire Fleet";
            }
        }

        private BasicTooltipViewModel _scrapFleetHint;
        [DataSourceProperty]
        public BasicTooltipViewModel ScrapFleetHint
        {
            get
            {
                return this._scrapFleetHint;
            }
            set
            {
                if (value != this._scrapFleetHint)
                {
                    this._scrapFleetHint = value;
                    base.OnPropertyChangedWithValue(value, "ScrapFleetHint");
                }
            }
        }

        public List<TooltipProperty> GetScrapFleetTooltip()
        {
            List<TooltipProperty> tooltips = new List<TooltipProperty>();
            tooltips.Add(new TooltipProperty("Scrap this fleet for component materials.", "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
            return tooltips;
        }

        public void ExecuteScrapSelectedFleet()
        {
            MBInformationManager.AddQuickInformation(new TextObject("Coming Soon (TM)..."), 0, null);
            // scrap all ships in the fleet and destroy the fleet
        }
        #endregion

        #region Transport Fleet
        [DataSourceProperty]
        public string TransportFleetText
        {
            get
            {
                return "Transport Fleet";
            }
        }

        private BasicTooltipViewModel _transportFleetHint;
        [DataSourceProperty]
        public BasicTooltipViewModel TransportFleetHint
        {
            get
            {
                return this._transportFleetHint;
            }
            set
            {
                if (value != this._transportFleetHint)
                {
                    this._transportFleetHint = value;
                    base.OnPropertyChangedWithValue(value, "TransportFleetHint");
                }
            }
        }

        [DataSourceProperty]
        public bool IsTransportFleetEnabled
        {
            get
            {
                try
                {
                    return SelectedFleet.Owner == Hero.MainHero; // player owns the fleet
                }
                catch
                {
                    return false;
                }
            }
        }

        public List<TooltipProperty> GetTransportFleetTooltip()
        {
            List<TooltipProperty> tooltips = new List<TooltipProperty>();
            // disabling conditions first
            if (SelectedFleet.Owner != Hero.MainHero)
            {
                TextObject textObject = new TextObject("{=MgilEO5siX}Must own this fleet in order to transport it");
                tooltips.Add(new TooltipProperty(textObject.ToString(), "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
            }

            if (tooltips.Count == 0)
            {
                TextObject textObject = new TextObject("{=MlPRmr2q4y}Transport this fleet to another nearby port");
                tooltips.Add(new TooltipProperty(textObject.ToString(), "", 0, false, TooltipProperty.TooltipPropertyFlags.None));
            }
            return tooltips;
        }

        public void ExecuteTransportSelectedFleet()
        {
            if (SelectedFleet != null && SelectedFleet.Settlement != null)
            {
                List<InquiryElement> portElements = new List<InquiryElement>();
                List<Settlement> ports = new List<Settlement>();
                // get list of ports that the current fleet is stored at
                if (PortCampaignBehavior.IsPortSettlement(SelectedFleet.Settlement))
                    ports = PortCampaignBehavior.Instance.PortTravelModel.ConnectedPorts[SelectedFleet.Settlement];
                else if (SelectedFleet.Settlement.SettlementComponent is LandingArea || 
                         SelectedFleet.Settlement.SettlementComponent is FerryTravelPoint)
                {
                    Settlement nearestPort = SettlementHelper.FindNearestSettlement(x => PortCampaignBehavior.Instance.PortTravelModel.ConnectedPorts.ContainsKey(x), SelectedFleet.Settlement);
                    ports.Add(nearestPort);
                    ports.AppendList(PortCampaignBehavior.Instance.PortTravelModel.ConnectedPorts[nearestPort]);
                }

                ports.RemoveAll(x => x.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction));
                ports = ports.OrderBy(x => (SelectedFleet.Settlement.Position2D - x.Position2D).Length).ToList();
                portElements = ports.Select(x => new InquiryElement(x, x.Name.ToString(), null, true, "")).ToList();

                MBInformationManager.ShowMultiSelectionInquiry(
                    new MultiSelectionInquiryData(
                        new TextObject("{=5YaAOJ0PP2}Select Destination").ToString(),
                        string.Empty, 
                        portElements, 
                        true, 
                        1, 
                        GameTexts.FindText("str_done", null).ToString(), 
                        GameTexts.FindText("str_cancel", null).ToString(), 
                        new Action<List<InquiryElement>>(OnTransportSelectionOver), 
                        null,
                        ""), 
                    false);
            }
            else
            {
                MBInformationManager.AddQuickInformation(new TextObject("{=ej04a9TtVs}Fleet must be in a settlement to be transported"), 0, null);
            }
        }

        public void OnTransportSelectionOver(List<InquiryElement> elements)
        {
            Settlement end = elements.First().Identifier as Settlement;
            Settlement start = SelectedFleet.Settlement;
            if(!(start.SettlementComponent is Town)) 
            {
                start = SettlementHelper.FindNearestSettlement(x => PortCampaignBehavior.Instance.PortTravelModel.ConnectedPorts.ContainsKey(x),
                    SelectedFleet.Settlement);
            }
            float distance = 0f;
            if (end == start)
                distance = (start.Position2D - SelectedFleet.Settlement.Position2D).Length;
            else
                distance = PortCampaignBehavior.Instance.PortTravelModel.GetDistance(end, start);
            float realDistance = distance * PortCampaignBehavior.Instance.PortTravelModel.GameMeterToRealKM;
            float days = distance / 100f;
            
            int cost = (int)(realDistance * PortCampaignBehavior.Instance.PortTravelModel.PassengerMile * SelectedFleet.CrewCapacity);

            TextObject title = new TextObject("{=rUqMQ8gz0n}Transport Fleet");
            TextObject message = new TextObject("{=SZvZZ97zge}Transport this fleet to {DESTINATION} for {COST}{GOLD_ICON}?");
            message.SetTextVariable("DESTINATION", end.Name);
            message.SetTextVariable("COST", cost);

            InformationManager.ShowInquiry(
                new InquiryData(
                    title.ToString(), 
                    message.ToString(), 
                    true, 
                    true, 
                    new TextObject("{=aeouhelq}Yes", null).ToString(), 
                    new TextObject("{=8OkPHu4f}No", null).ToString(),
                    delegate ()
                    {
                        GiveGoldAction.ApplyBetweenCharacters(MobileParty.MainParty.LeaderHero, null, cost, false);
                        LandingArea landingArea = SelectedFleet.Settlement.SettlementComponent as LandingArea;
                        if (landingArea != null)
                            landingArea.SetOccupied(false);

                        SelectedFleet.TransferToSettlement(null);
                        // calculate a reasonable transport time
                        float transportTime = Game.Current.CheatMode ? 0.1f : days;
                        CampaignTime arrivalTime = CampaignTime.DaysFromNow(transportTime);
                        TimedCampaignActionManager.Instance.AddAction(new TimedFleetDelivery(start, end, SelectedFleet, arrivalTime));
                        string daysStr = string.Empty;// (int)days == 0 ? "less than one day" : (int)days == 1 ? (int)days + " day" : (int)days + " days";
                        if ((int)days == 0)
                            daysStr = new TextObject("{=jsbyJ80Ops}less than one day").ToString();
                        else if ((int)days == 1)
                            daysStr = new TextObject("{=V7KIK61gp7}one day").ToString();
                        else if ((int)days > 1)
                        {
                            TextObject message3 = new TextObject("{=Cf4ZIn6ktH}{DAYS} days");
                            message3.SetTextVariable("DAYS", (int)days);
                            daysStr = message3.ToString();
                        }
                        else
                            daysStr = new TextObject("***Error in OnTransportSelectionOver").ToString();

                        TextObject message2 = new TextObject("{=YUWjD6uCMQ}Your fleet will arrive at {DESTINATION} in {DAYS_STRING}");
                        message2.SetTextVariable("DESTINATION", end.Name);
                        message2.SetTextVariable("DAYS_STRING", daysStr);
                        MBInformationManager.AddQuickInformation(message2);
                        this.RefreshValues();
                    },
                    null));
        }
        #endregion

        #endregion

        #region Ship Name Container

        [DataSourceProperty]
        public bool IsRenameShipEnabled
        {
            get
            {
                try
                {
                    return SelectedFleet.Owner == Hero.MainHero; // player owns the fleet that the ship is in
                }
                catch
                {
                    return false;
                }
            }
        }

        public void ExecuteChangeShipName()
        {
            GameTexts.SetVariable("MAX_LETTER_COUNT", CampaignShip.ShipNameCharLimit);
            GameTexts.SetVariable("MIN_LETTER_COUNT", 1);
            InformationManager.ShowTextInquiry(
                new TextInquiryData(
                    new TextObject("{=T7Cu7H4UsX}Ship Name").ToString(), 
                    string.Empty, 
                    true, 
                    true, 
                    GameTexts.FindText("str_done", null).ToString(),
                    GameTexts.FindText("str_cancel", null).ToString(),
                    delegate (string str)
                    {
                        SelectedCampaignShip.Name = new TextObject(str, null);
                        MBTextManager.SetTextVariable("MANAGED_SHIP_NAME", SelectedCampaignShip.Name);
                        this.RefreshValues();
                    }, 
                    null, 
                    false, 
                    CampaignShip.IsShipNameValid, 
                    "", 
                    SelectedCampaignShip.Name.ToString())
                );
        }
        #endregion

        public override void OnFinalize()
        {
            base.OnFinalize();
        }

        public void ExecuteCloseShipManagement()
        {
            _closeShipManagement();
        }

        public void ExecuteZoomToSettlement()
        {
            if(SelectedFleet != null)
                ShipManagementUIManager.Instance._zoomTo = SelectedFleet.GetPosition();
            ExecuteCloseShipManagement();
        }

        public void OnTick(float dt)
        {

        }
    }

    /// <summary>
    /// ViewModel class for selected Fleet.
    /// </summary>
    public class ManagedFleetItemVM : SelectorItemVM
    {
        public Fleet Fleet { get; private set; }

        public ManagedFleetItemVM(Fleet fleet) : base(fleet.Name.ToString())
        {
            Fleet = fleet;
            this.RefreshValues();
        }
    }

    /// <summary>
    /// ViewModel class for selected Ship.
    /// </summary>
    public class ManagedShipItemVM : SelectorItemVM
    {
       public CampaignShip CampaignShip { get; private set; }

        public ManagedShipItemVM(CampaignShip campaignShip, Fleet fleet) : base(ShipSelectorName(campaignShip, fleet))
        {
            CampaignShip = campaignShip;
            this.RefreshValues();
        }

        public static string ShipSelectorName(CampaignShip ship, Fleet fleet)
        {
            TextObject textObject = TextObject.Empty;
            TextObject flagship = TextObject.Empty;
            textObject.SetTextVariable("SHIP_NAME", ship.Name);
            if (fleet.Ships.IndexOf(ship) == 0)
            {
                flagship = new TextObject("{=XclQz3BtRR}(Flagship)");
                textObject = new TextObject("{SHIP_NAME} {FLAGSHIP}");
                textObject.SetTextVariable("FLAGSHIP", flagship);
                textObject.SetTextVariable("SHIP_NAME", ship.Name);
            }
            else 
            {
                textObject = new TextObject("{SHIP_NAME}");
                textObject.SetTextVariable("SHIP_NAME", ship.Name);
            }

            return textObject.ToString();
        }
    }

    /// <summary>
    /// ViewModel for CampaignShips.
    /// </summary>
    public class ShipViewModel : ViewModel
    {
        private CampaignShip _ship;

        public ShipViewModel()
        {
            
        }

        public ShipViewModel(CampaignShip ship)
        {
            _ship = ship;
            PrefabName = ship.PrefabName;
        }
        
        public void Tick(float dt)
        {
            
        }

        [DataSourceProperty]
        public string ShipSpeed
        {
            get
            {
                return _ship != null ? _ship.Speed.ToString("0.0") : "***error***";
            }
        }

        [DataSourceProperty]
        public string ShipTroopCapacity
        {
            get
            {
                return _ship != null ? _ship.TroopCapacity.ToString() : "***error***";
            }
        }

        [DataSourceProperty]
        public string ShipCrewCapacity
        {
            get
            {
                return _ship != null ? _ship.CrewCapacity.ToString() : "***error***";
            }
        }

        [DataSourceProperty]
        public string ShipCargoCapacity
        {
            get
            {
                return _ship != null ? _ship.CargoCapacity.ToString("0.00") : "***error***";
            }
        }

        [DataSourceProperty]
        public string ShipName
        {
            get
            {
                return _ship != null ? _ship.Name.ToString() : "***error***";
            }
        }

        [DataSourceProperty]
        public string ShipClass
        {
            get
            {
                return  _ship != null ? _ship.Class.ToString() : "***error***";
            }
        }

        private string _prefabName;
        [DataSourceProperty]
        public string PrefabName
        {
            get
            {
                return this._prefabName;
            }
            set
            {
                this._prefabName = value;
                base.OnPropertyChangedWithValue(value, "PrefabName");
            }
        }

        private float _initialPanRotation;
        [DataSourceProperty]
        public float InitialPanRotation
        {
            get
            {
                return this._initialPanRotation;
            }
            set
            {
                if (value != this._initialPanRotation)
                {
                    this._initialPanRotation = value;
                    base.OnPropertyChangedWithValue(value, "InitialPanRotation");
                }
            }
        }

        public override void OnFinalize()
        {
            base.OnFinalize();
            this.PrefabName = "";
        }
    }

    /// <summary>
    /// ViewModel for Fleets.
    /// </summary>
    public class FleetViewModel : ViewModel
    {
        private Fleet _fleet;

        public FleetViewModel(Fleet fleet)
        {
            _fleet = fleet;
        }

        #region Data Sources
        [DataSourceProperty]
        public string FleetSpeed
        {
            get
            {
                return _fleet.Speed.ToString("0.00");
            }
        }

        [DataSourceProperty]
        public string FleetTroopCapacity
        {
            get
            {
                return _fleet.TroopCapacity.ToString();
            }
        }

        [DataSourceProperty]
        public string FleetCrewCapacity
        {
            get
            {
                return _fleet.CrewCapacity.ToString();
            }
        }

        [DataSourceProperty]
        public string FleetCargoCapacity
        {
            get
            {
                return _fleet.CargoCapacity.ToString("0.00");
            }
        }

        [DataSourceProperty]
        public string SlowestShip
        {
            get
            {
                return _fleet.Ships.OrderBy(x => x.Speed).First().Name.ToString();
            }
        }

        [DataSourceProperty]
        public string MostDamagedShip
        {
            get
            {
                return _fleet.Ships.OrderBy(x => x.Health).First().Name.ToString();
            }
        }

        [DataSourceProperty]
        public string NumShips
        {
            get
            {
                return _fleet.Ships.Count.ToString();
            }
        }
        #endregion
    }

    public class MaritimeConceptVM : ViewModel
    {
        public MaritimeConceptVM(string title, string description)
        {
            _title = title;
            _description = description;
        }

        private string _title;
        [DataSourceProperty]
        public string Title { get { return _title; } }

        private string _description;
        [DataSourceProperty]
        public string Description { get { return _description; } }
    }
}
