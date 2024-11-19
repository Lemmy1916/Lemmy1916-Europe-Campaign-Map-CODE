using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Localization;
using TaleWorlds.ModuleManager;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

namespace Europe_LemmyProject.MaritimeSystem
{
    public class ShipEconomyModel
    {

    }

    /// <summary>
    /// Determines prices for buying and selling ships
    /// </summary>
    public class DefaultShipEconomnyModel
    {
        public static int GetShipValue(CampaignShip ship)
        {
            return (int)(ship.Cost * 0.9f);
        }

        public static void SellShip(Hero seller, Hero buyer, CampaignShip ship)
        {
            /*
            GiveGoldAction.ApplyBetweenCharacters(buyer, seller, GetShipValue(ship), false);
            _managedFleet.RemoveShip(_managedShip);
            if (_managedFleet.Count == 0)
            {
                MobilePartyWrapper.MainPartyWrapper.DestroyStoredFleet(_managedFleet);
                _managedFleet = null;
            }
            */
        }
    }

    /// <summary>
    /// Template for creating a CampaignShip
    /// </summary>
    public class CampaignShipTemplate
    {
        public static List<CampaignShipTemplate> AllShipTemplates;
        public string ID { get; private set; }
        public TextObject BaseName { get; set; }
        public float BaseSpeed { get; private set; }
        public float BaseCargoCapacity { get; private set; }
        public int BaseCrewCapacity { get; private set; }
        public int BaseTroopCapacity { get; private set; }
        // public int BaseAnimalCapacity { get; private set; }
        public int BaseCost { get; private set; }
        // base build time in days
        public float BaseBuildTime;
        public Dictionary<ItemObject, int> BaseMaterials { get; private set; }
        public string BasePrefabName { get; private set; }
        public List<CultureObject> Cultures { get; private set; }

        public static CampaignShipTemplate GetById(string id)
        {
            return AllShipTemplates.FirstOrDefault(x => x.ID == id);
        }

        public void Deserialize(XmlNode node)
        {
            ID = node.Attributes["id"].Value;
            BaseName = new TextObject(node.Attributes["base_name"].Value);
            BaseSpeed = float.Parse(node.Attributes["base_speed"].Value);
            BaseCargoCapacity = float.Parse(node.Attributes["base_cargo_capacity"].Value);
            BaseTroopCapacity = int.Parse(node.Attributes["base_troop_capacity"].Value);
            BaseCrewCapacity = int.Parse(node.Attributes["base_crew_count"].Value);
            BaseCost = int.Parse(node.Attributes["base_cost"].Value);
            BaseBuildTime = int.Parse(node.Attributes["base_build_time"].Value);
            BasePrefabName = node.Attributes["base_prefab"].Value;

            BaseMaterials = new Dictionary<ItemObject, int>();
            Cultures = new List<CultureObject>();
            foreach (XmlNode node2 in node.ChildNodes)
            {
                if (node2.Name == "BaseMaterials")
                {
                    foreach (XmlNode material in node2.ChildNodes)
                    {
                        ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(material.Attributes["id"].Value);
                        BaseMaterials.Add(item, int.Parse(material.Attributes["amount"].Value));
                    }
                }
                else if (node2.Name == "Cultures")
                {
                    foreach (XmlNode culture in node2.ChildNodes)
                    {
                        CultureObject newCulture = MBObjectManager.Instance.GetObject<CultureObject>(culture.Attributes["id"].Value);
                        Cultures.Add(newCulture);
                    }
                }
            }
        }

        public static void DeserializeAll()
        {
            AllShipTemplates = new List<CampaignShipTemplate>();
            XmlReaderSettings readerSettings = new XmlReaderSettings();
            readerSettings.IgnoreComments = true;
            string path = ModuleHelper.GetModuleFullPath("Europe_LemmyProject") + "ModuleData/ELPData/CampaignShipTemplates.xml";
            using (XmlReader reader = XmlReader.Create(path, readerSettings)) 
            {
                XmlDocument shipTemplatesDoc = new XmlDocument();
                shipTemplatesDoc.Load(reader);
                XmlNode shipTempaltesNodes = shipTemplatesDoc.ChildNodes[1];
                foreach (XmlNode shipTemplateNode in shipTempaltesNodes)
                {
                    CampaignShipTemplate shipTemplateObj = new CampaignShipTemplate();
                    shipTemplateObj.Deserialize(shipTemplateNode);
                    AllShipTemplates.Add(shipTemplateObj);
                }
            }
        }
    }

    /// <summary>
    /// Represents a player (maybe also ai?) owned ship. Currently just affects party speed, troop 
    /// capacity and inventory storage.
    /// </summary>
    public class CampaignShip
    {
        public static int ShipNameCharLimit = 30;

        public static Tuple<bool, string> IsShipNameValid(string name)
        {
            string item = string.Empty;
            List<TextObject> list = new List<TextObject>();
            if (name.Length > 30 || name.Length < 1)
            {
                TextObject textObject = new TextObject("{=L3ZpRfz58T}Ship name must be between 1 and 30 characters");
                list.Add(textObject);
            }

            bool item2 = list.Count == 0;
            if (list.Count == 1)
            {
                item = list[0].ToString();
            }
            return new Tuple<bool, string>(item2, item);
        }

        [SaveableProperty(0)]
        public TextObject Name { get; set; }
        [SaveableProperty(1)]
        public float Speed { get; private set; }
        [SaveableProperty(2)]
        public float CargoCapacity { get; private set; }
        [SaveableProperty(3)]
        public int TroopCapacity { get; private set; }
        // base build time in days
        [SaveableProperty(4)]
        public float BuildTime { get; private set; }
        [SaveableProperty(5)]
        public ItemRoster Cargo { get; private set; }
        [SaveableProperty(6)]
        public float Health { get; set; }
        [SaveableProperty(7)]
        public float MaxHealth { get; set; }
        [SaveableProperty(8)]
        public string TemplateID { get; set; }
        [SaveableProperty(9)]
        public float Cost { get; set; }
        [SaveableProperty(10)]
        public int CrewCapacity { get; set; }

        [SaveableField(10)]
        private string _prefabName;

        [SaveableField(5)]
        private ItemRoster _inventory;
        public ItemRoster Inventory { get { return _inventory; } set { _inventory = value; } }
        public string PrefabName
        {
            get
            {
                if (_prefabName == null)
                {
                    _prefabName = CampaignShipTemplate.AllShipTemplates.FirstOrDefault(x => x.ID == TemplateID).BasePrefabName;
                }
                return _prefabName;
            }
            set
            {
                if (GameEntity.PrefabExists(value))
                {
                    _prefabName = value;
                }
                else
                {
                    MBDebug.Assert(GameEntity.PrefabExists(value), "Prefab does not exist, using template base prefab name.", "Campaignship.cs", "set_PrefabName", 57);
                    _prefabName = CampaignShipTemplate.AllShipTemplates.FirstOrDefault(x => x.ID == TemplateID).BasePrefabName;
                }
            }
        }

        public CampaignShipTemplate Template
        {
            get
            {
                return CampaignShipTemplate.GetById(this.TemplateID);
            }
        }

        public TextObject Class
        {
            get
            {
                return Template.BaseName;
            }
        }

        public CampaignShip(CampaignShipTemplate template)
        {
            Name = template.BaseName;
            Speed = template.BaseSpeed;
            CargoCapacity = template.BaseCargoCapacity;
            TroopCapacity = template.BaseTroopCapacity;
            CrewCapacity = template.BaseCrewCapacity;
            BuildTime = template.BaseBuildTime;
            Cargo = new ItemRoster();
            Health = 100f;
            MaxHealth = 100f;
            TemplateID = template.ID;
            Cost = template.BaseCost;
            PrefabName = template.BasePrefabName;
        }
    }
}
