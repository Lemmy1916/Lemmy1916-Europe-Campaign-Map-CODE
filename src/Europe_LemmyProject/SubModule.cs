using System.Xml;

using HarmonyLib;

using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

using Europe_LemmyProject.MaritimeSystem;
using Europe_LemmyProject.General;
using Europe_LemmyProject.MaritimeSystem.ShipManagementUI;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.InputSystem;
using System.IO;
using System.Reflection;
using SandBox.View.Map;

namespace Europe_LemmyProject
{
    public class SubModule : MBSubModuleBase
    {
        private bool hasRegisteredNotifications = false;

        public Harmony harmony;
        public static string MainMapName = "modded_main_map";
        public ShipManagementUIManager _shipManagementUIManager;

        public static List<GameKey> MapGameKeys = new List<GameKey>() 
        {
            new GameKey(67, "MapGhostSettlementPhysics", "MapHotKeyCategory",
                InputKey.Z, InputKey.Invalid, GameKeyMainCategories.CampaignMapCategory),
			new GameKey(68, "MapOpenShipManagement", "MapHotKeyCategory",
                InputKey.Period, InputKey.Invalid, GameKeyMainCategories.CampaignMapCategory)
        };

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            
            XmlDocument mapSelect = new XmlDocument();
            mapSelect.Load(ModuleHelper.GetModuleFullPath("Europe_LemmyProject") + "ModuleData/ELPData/MainMapSelect.xml");
            XmlNode mapNode = mapSelect.ChildNodes[1];
            MainMapName = mapNode.Attributes["id"].Value;

            harmony = new Harmony("mod.harmony.elp");
            harmony.PatchAll();

            HotKeyManager.AddAuxiliaryCategory(new ELPHotKeys());

            // Load our strings the hard way since the game won't do it for us
            string text = ModuleHelper.GetModuleFullPath("Europe_LemmyProject") + "ModuleData/ELPData/elp_strings.xml";
            Debug.Print("opening " + text, 0, Debug.DebugColor.White, 17592186044416UL);
            XmlDocument xmlDocument = new XmlDocument();
            StreamReader streamReader = new StreamReader(text);
            string text2 = streamReader.ReadToEnd();
            xmlDocument.LoadXml(text2);
            streamReader.Close();
            GameTextManager current = TaleWorlds.MountAndBlade.Module.CurrentModule.GlobalTextManager;
            MethodInfo dynMethod = current.GetType().GetMethod("LoadFromXML", BindingFlags.NonPublic | BindingFlags.Instance);
            dynMethod.Invoke(current, new object[] { xmlDocument});

            _shipManagementUIManager = new ShipManagementUIManager();
        }

        public override void RegisterSubModuleObjects(bool isSavedCampaign)
        {
            
        }

        public override void OnAfterGameInitializationFinished(Game game, object starterObject)
        {
            
        }

        public override void AfterRegisterSubModuleObjects(bool isSavedGame)
        {
            
        }


        protected override void OnApplicationTick(float dt)
        {
            if (!hasRegisteredNotifications)
            {
                if (MapScreen.Instance != null)
                {
                    MapScreen.Instance.MapNotificationView.RegisterMapNotificationType(typeof(FleetDeliveredMapNotification), typeof(FleetDeliveredMapNotificationVM));
                    hasRegisteredNotifications = true;
                }
            }

            _shipManagementUIManager.Tick(dt);
        }
        
        protected override void InitializeGameStarter(Game game, IGameStarter starter)
        {
            starter.AddModel(new DefaultPortTravelModel());
            CampaignGameStarter campaignGameStarter = starter as CampaignGameStarter;
            if (campaignGameStarter != null)
            {
                // have to remove VillageTradeBoundCampaignBehavior since TW decided that making
                // the game crash for no reason is a good thing
                List<CampaignBehaviorBase> behaviors = Traverse.Create(campaignGameStarter).Field("_campaignBehaviors").GetValue() as List<CampaignBehaviorBase>;
                int index = behaviors.IndexOf(behaviors.First(x => x.GetType().Name.Contains("VillageTradeBoundCampaignBehavior")));
                behaviors.RemoveAt(index);

                campaignGameStarter.AddBehavior(new MaritimeManager());

                campaignGameStarter.AddModel(new ELPGameMenuModel());
                campaignGameStarter.AddModel(new ELPPartySpeedModel());
                campaignGameStarter.AddModel(new ELPInventoryCapacityModel());
                campaignGameStarter.AddBehavior(new PortCampaignBehavior());
                campaignGameStarter.AddBehavior(new FerryCampaignBehavior());
                campaignGameStarter.AddBehavior(new LandingAreaCampaignBehavior());
                campaignGameStarter.AddBehavior(new TimedCampaignActionManager());
                campaignGameStarter.AddBehavior(new BorrowedShipsCampaignBehavior());
                campaignGameStarter.AddBehavior(new ELPVillageTradeBoundCampaignBehavior());
            }
        }

        public override void OnNewGameCreated(Game game, object initializerObject)
        {
            this.OnRegisterTypes();
        }

        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
        }

        public override void OnGameLoaded(Game game, object initializerObject)
        {
            this.OnRegisterTypes();
        }

        private void OnRegisterTypes()
        {
            MBObjectManager.Instance.RegisterType<FerryTravelPoint>("FerryTravelPoint", "Components", 56U, true, false);
            MBObjectManager.Instance.RegisterType<Port>("Port", "Components", 57U, true, false);
            MBObjectManager.Instance.RegisterType<LandingArea>("LandingArea", "Components", 58U, true, false);
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            InformationManager.DisplayMessage(new InformationMessage("Europe Lemmy Project dll loaded", new Color(134, 114, 250)));
        }
    }
}