using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;

namespace Europe_LemmyProject.MaritimeSystem.MainMapComponents
{
    /// <summary>
    /// Saves maritime settlements to their respective files
    /// </summary>
    public class MaritimeSettlementsManager : ScriptComponentBehavior
    {
        public SimpleButton SaveFerryPositions;
        public SimpleButton SavePortPositions;
        public SimpleButton SaveLandingAreaPositions;

        protected override void OnEditorVariableChanged(string variableName)
        {
            if (variableName == "SaveFerryPositions")
            {
                Debug.Print("(SaveFerryPositions -- Begin)");
                XmlDocument ferriesDoc = new XmlDocument();
                string filePath = ModuleHelper.GetModuleFullPath("Europe_LemmyProject") + "ModuleData/ELPData/FerryPoints.xml";
                ferriesDoc.Load(filePath);
                List<GameEntity> ferryPoints = new List<GameEntity>();
                base.Scene.GetAllEntitiesWithScriptComponent<FerryScriptComponent>(ref ferryPoints);

                XmlNode settlements = ferriesDoc.ChildNodes[1];
                Debug.Print("(SaveFerryPositions -- Found Ferries: " + ferryPoints.Count + ")");
                // assign to nodes that have already been created
                foreach (XmlNode settlement in settlements)
                {
                    GameEntity entity = ferryPoints.FirstOrDefault(x => x.Name == settlement.Attributes["id"].Value);
                    if (entity != null)
                    {
                        Debug.Print("(SaveFerryPositions -- Updating existing ferry point: " + entity.Name + ")");
                        settlement.Attributes["id"].Value = entity.Name;
                        settlement.Attributes["posX"].Value = entity.GlobalPosition.AsVec2.X.ToString("0.00");
                        settlement.Attributes["posY"].Value = entity.GlobalPosition.AsVec2.Y.ToString("0.00");
                        GameEntity gate = entity.GetChildren().FirstOrDefault(x => x.HasTag("gate_pos"));
                        settlement.Attributes["gate_posX"].Value = gate.GlobalPosition.AsVec2.X.ToString("0.00");
                        settlement.Attributes["gate_posY"].Value = gate.GlobalPosition.AsVec2.Y.ToString("0.00");
                        XmlNode component = settlement.FirstChild.FirstChild;
                        component.Attributes["id"].Value = "comp_" + entity.Name;
                        component.Attributes["destination_id"].Value = entity.GetFirstScriptOfType<FerryScriptComponent>().destination;
                        ferryPoints.Remove(entity);
                        // and remove known GameEntity's from ferryPoints
                    }
                }
                // if ferryPoints still has elements, add them to the doc
                if (ferryPoints.Count > 0)
                {
                    foreach (GameEntity ferry in ferryPoints)
                    {
                        Debug.Print("(SaveFerryPositions -- Adding new ferry point: " + ferry.Name + ")");
                        // Settlement
                        XmlElement settlement = ferriesDoc.CreateElement("Settlement");
                        settlement.SetAttribute("id", ferry.Name);
                        settlement.SetAttribute("name", "Ferry Point");
                        settlement.SetAttribute("posX", ferry.GlobalPosition.AsVec2.X.ToString("0.00"));
                        settlement.SetAttribute("posY", ferry.GlobalPosition.AsVec2.Y.ToString("0.00"));
                        GameEntity gate = ferry.GetChildren().FirstOrDefault(x => x.HasTag("gate_pos"));
                        settlement.SetAttribute("gate_posX", gate.GlobalPosition.AsVec2.X.ToString("0.00"));
                        settlement.SetAttribute("gate_posY", gate.GlobalPosition.AsVec2.Y.ToString("0.00"));
                        settlement.SetAttribute("culture", "Culture.neutral_culture");
                        settlement.SetAttribute("prosperity", "557");
                        settlement.SetAttribute("text", "A ferry point");

                        // Components
                        XmlElement components = ferriesDoc.CreateElement("Components");

                        // FerryTravelPoint
                        XmlElement ferryTravelPoint = ferriesDoc.CreateElement("FerryTravelPoint");
                        ferryTravelPoint.SetAttribute("id", "comp_" + ferry.Name);
                        ferryTravelPoint.SetAttribute("map_icon", "bandit_hideout_b");
                        ferryTravelPoint.SetAttribute("destination_id", ferry.GetFirstScriptOfType<FerryScriptComponent>().destination);
                        ferryTravelPoint.SetAttribute("background_crop_position", "0.0");
                        ferryTravelPoint.SetAttribute("background_mesh", "empire_twn_scene_bg");
                        ferryTravelPoint.SetAttribute("wait_mesh", "wait_hideout_desert");
                        ferryTravelPoint.SetAttribute("gate_rotation", "0.0");
                        ferryTravelPoint.IsEmpty = true;

                        components.AppendChild(ferryTravelPoint);
                        settlement.AppendChild(components);
                        settlements.AppendChild(settlement);
                    }
                }
                ferriesDoc.Save(filePath);
            }
            else if (variableName == "SavePortPositions")
            {
                Debug.Print("(SavePortPositions -- Begin)");
                XmlDocument portsDoc = new XmlDocument();
                string filePath = ModuleHelper.GetModuleFullPath("Europe_LemmyProject") + "ModuleData/ELPData/Ports.xml";
                portsDoc.Load(filePath);

                XmlDocument settlementsDoc = new XmlDocument();
                string filePath2 = ModuleHelper.GetModuleFullPath("Europe_LemmyProject") + "ModuleData/settlements.xml";
                settlementsDoc.Load(filePath2);

                List<GameEntity> portEntities = new List<GameEntity>();
                base.Scene.GetAllEntitiesWithScriptComponent<PortScriptComponent>(ref portEntities);

                XmlNode settlements = portsDoc.ChildNodes[1];
                // assign to nodes that have already been created
                foreach (XmlNode settlement in settlements)
                {
                    GameEntity entity = portEntities.FirstOrDefault(x => x.Name == settlement.Attributes["id"].Value);
                    if (entity != null)
                    {
                        Debug.Print("(SavePortPositions -- Updating existing port: " + entity.Name + ")");
                        settlement.Attributes["id"].Value = entity.Name;
                        settlement.Attributes["posX"].Value = entity.GlobalPosition.AsVec2.X.ToString("0.00");
                        settlement.Attributes["posY"].Value = entity.GlobalPosition.AsVec2.Y.ToString("0.00");
                        GameEntity gate = entity.GetChildren().FirstOrDefault(x => x.HasTag("gate_pos"));
                        settlement.Attributes["gate_posX"].Value = gate.GlobalPosition.AsVec2.X.ToString("0.00");
                        settlement.Attributes["gate_posY"].Value = gate.GlobalPosition.AsVec2.Y.ToString("0.00");
                        XmlNode component = settlement.FirstChild.FirstChild;
                        component.Attributes["id"].Value = "comp_" + entity.Name;
                        string portOf = entity.GetFirstScriptOfType<PortScriptComponent>().portOf;
                        component.Attributes["port_of"].Value = portOf;
                        XmlNode Port = settlement.FirstChild.FirstChild;
                        XmlNodeList SettlementNodes = settlementsDoc.GetElementsByTagName("Settlement");
                        XmlNode SettlementNode = SettlementNodes.Cast<XmlNode>().First(x => x.Attributes["id"].Value == portOf);
                        string name = "Port of " + SettlementNode.Attributes["name"].Value;
                        settlement.Attributes["name"].Value = name;
                        portEntities.Remove(entity);
                        // and remove known GameEntity's from ferryPoints
                    }
                }
                // if portEntities still has elements, add them to the doc
                if (portEntities.Count > 0)
                {
                    for (int i = 0; i < portEntities.Count; i++)
                    {
                        GameEntity portEntity = portEntities[i];
                        Debug.Print("(SavePortPositions -- Adding new port: " + portEntity.Name + ")");
                        // Settlement
                        string portOf = portEntity.GetFirstScriptOfType<PortScriptComponent>().portOf;
                        XmlElement settlement = portsDoc.CreateElement("Settlement");
                        settlement.SetAttribute("id", portEntity.Name);
                        settlement.SetAttribute("name", "Port of " + portOf);
                        settlement.SetAttribute("posX", portEntity.GlobalPosition.AsVec2.X.ToString("0.00"));
                        settlement.SetAttribute("posY", portEntity.GlobalPosition.AsVec2.Y.ToString("0.00"));
                        GameEntity gate = portEntity.GetChildren().FirstOrDefault(x => x.HasTag("gate_pos"));
                        settlement.SetAttribute("gate_posX", gate.GlobalPosition.AsVec2.X.ToString("0.00"));
                        settlement.SetAttribute("gate_posY", gate.GlobalPosition.AsVec2.Y.ToString("0.00"));
                        settlement.SetAttribute("culture", "Culture.neutral_culture");
                        settlement.SetAttribute("prosperity", "557");
                        settlement.SetAttribute("text", "The port of " + portOf);

                        // Components
                        XmlElement components = portsDoc.CreateElement("Components");

                        // FerryTravelPoint
                        XmlElement port = portsDoc.CreateElement("Port");
                        port.SetAttribute("id", "comp_" + portEntity.Name);
                        port.SetAttribute("port_of", portEntity.GetFirstScriptOfType<PortScriptComponent>().portOf);
                        port.SetAttribute("map_icon", "bandit_hideout_b");
                        port.SetAttribute("background_crop_position", "0.0");
                        port.SetAttribute("background_mesh", "empire_twn_scene_bg");
                        port.SetAttribute("wait_mesh", "wait_hideout_desert");
                        port.SetAttribute("gate_rotation", "0.0");
                        port.IsEmpty = true;

                        components.AppendChild(port);
                        settlement.AppendChild(components);
                        settlements.AppendChild(settlement);
                    }
                }
                portsDoc.Save(filePath);
            }
            else if (variableName == "SaveLandingAreaPositions")
            {
                Debug.Print("(SaveLandingAreaPositions -- Begin)");
                XmlDocument landingAreasDoc = new XmlDocument();
                string filePath = ModuleHelper.GetModuleFullPath("Europe_LemmyProject") + "ModuleData/ELPData/LandingAreas.xml";
                landingAreasDoc.Load(filePath);
                List<GameEntity> landingAreaEntities = new List<GameEntity>();
                base.Scene.GetAllEntitiesWithScriptComponent<LandingAreaScriptComponent>(ref landingAreaEntities);

                XmlNode settlements = landingAreasDoc.ChildNodes[1];
                // assign to nodes that have already been created
                foreach (XmlNode settlement in settlements)
                {
                    GameEntity entity = landingAreaEntities.FirstOrDefault(x => x.Name == settlement.Attributes["id"].Value);
                    if (entity != null)
                    {
                        Debug.Print("(SaveLandingAreaPositions -- Updating existing landing area: " + entity.Name + ")");
                        settlement.Attributes["id"].Value = entity.Name;
                        settlement.Attributes["posX"].Value = entity.GlobalPosition.AsVec2.X.ToString("0.00");
                        settlement.Attributes["posY"].Value = entity.GlobalPosition.AsVec2.Y.ToString("0.00");
                        GameEntity gate = entity.GetChildren().FirstOrDefault(x => x.HasTag("gate_pos"));
                        settlement.Attributes["gate_posX"].Value = gate.GlobalPosition.AsVec2.X.ToString("0.00");
                        settlement.Attributes["gate_posY"].Value = gate.GlobalPosition.AsVec2.Y.ToString("0.00");
                        XmlNode component = settlement.FirstChild.FirstChild;
                        component.Attributes["id"].Value = "comp_" + entity.Name;
                        GameEntity landingPos = entity.GetChildren().FirstOrDefault(x => x.HasTag("landing_pos"));
                        component.Attributes["landed_ships_x"].Value = landingPos.GlobalPosition.AsVec2.X.ToString("0.00");
                        component.Attributes["landed_ships_y"].Value = landingPos.GlobalPosition.AsVec2.Y.ToString("0.00");
                        landingAreaEntities.Remove(entity);
                        // and remove known GameEntity's from ferryPoints
                    }
                }
                // if landingAreaEntities still has elements, add them to the doc
                if (landingAreaEntities.Count > 0)
                {
                    for (int i = 0; i < landingAreaEntities.Count; i++)
                    {
                        GameEntity landingAreaEntity = landingAreaEntities[i];
                        Debug.Print("(SaveLandingAreaPositions -- Adding new landing area: " + landingAreaEntity.Name + ")");
                        // Settlement
                        XmlElement settlement = landingAreasDoc.CreateElement("Settlement");
                        settlement.SetAttribute("id", landingAreaEntity.Name);
                        settlement.SetAttribute("name", "Landing Area");
                        settlement.SetAttribute("posX", landingAreaEntity.GlobalPosition.AsVec2.X.ToString("0.00"));
                        settlement.SetAttribute("posY", landingAreaEntity.GlobalPosition.AsVec2.Y.ToString("0.00"));
                        GameEntity gate = landingAreaEntity.GetChildren().FirstOrDefault(x => x.HasTag("gate_pos"));
                        settlement.SetAttribute("gate_posX", gate.GlobalPosition.AsVec2.X.ToString("0.00"));
                        settlement.SetAttribute("gate_posY", gate.GlobalPosition.AsVec2.Y.ToString("0.00"));
                        settlement.SetAttribute("culture", "Culture.neutral_culture");
                        settlement.SetAttribute("prosperity", "557");
                        settlement.SetAttribute("text", "A Landing Area");

                        // Components
                        XmlElement components = landingAreasDoc.CreateElement("Components");

                        // LandingArea
                        XmlElement landingArea = landingAreasDoc.CreateElement("LandingArea");
                        landingArea.SetAttribute("id", "comp_" + landingAreaEntity.Name);
                        landingArea.SetAttribute("map_icon", "bandit_hideout_b");
                        GameEntity landingPos = landingAreaEntity.GetChildren().FirstOrDefault(x => x.HasTag("landing_pos"));
                        landingArea.SetAttribute("landed_ships_x", landingPos.GlobalPosition.AsVec2.X.ToString("0.00"));
                        landingArea.SetAttribute("landed_ships_y", landingPos.GlobalPosition.AsVec2.Y.ToString("0.00"));
                        landingArea.SetAttribute("background_crop_position", "0.0");
                        landingArea.SetAttribute("background_mesh", "empire_twn_scene_bg");
                        landingArea.SetAttribute("wait_mesh", "wait_hideout_desert");
                        landingArea.SetAttribute("gate_rotation", "0.0");
                        landingArea.IsEmpty = true;

                        components.AppendChild(landingArea);
                        settlement.AppendChild(components);
                        settlements.AppendChild(settlement);
                    }
                }
                landingAreasDoc.Save(filePath);
            }
        }

        protected override void OnInit()
        {
            base.OnInit();
            base.SetScriptComponentToTick(this.GetTickRequirement());
        }
        protected override void OnEditorInit()
        {
            Debug.Print("MaritimeSettlementsManager is available");
            base.OnEditorInit();
        }

        public override ScriptComponentBehavior.TickRequirement GetTickRequirement()
        {
            return ScriptComponentBehavior.TickRequirement.Tick;
        }
    }

    /// <summary>
    /// Labels a GameEntity on the campaign map as a Port
    /// </summary>
    public class PortScriptComponent : ScriptComponentBehavior
    {
        public string portOf;

        protected override void OnInit()
        {
            base.OnInit();
            base.SetScriptComponentToTick(this.GetTickRequirement());
        }
        protected override void OnEditorInit()
        {
            base.OnEditorInit();
        }

        public override ScriptComponentBehavior.TickRequirement GetTickRequirement()
        {
            return ScriptComponentBehavior.TickRequirement.Tick;
        }
    }

    /// <summary>
    /// Labels a GameEntity on the campaign map as a LandingArea
    /// </summary>
    public class LandingAreaScriptComponent : ScriptComponentBehavior
    {
        protected override void OnEditorInit()
        {
            base.OnEditorInit();
        }

        public override ScriptComponentBehavior.TickRequirement GetTickRequirement()
        {
            return ScriptComponentBehavior.TickRequirement.Tick;
        }
    }

    /// <summary>
    /// Lables a GameEntity on the campaign map as a FerryPoint
    /// </summary>
    public class FerryScriptComponent : ScriptComponentBehavior
    {
        public string destination;
        protected override void OnInit()
        {
            base.OnInit();
            base.SetScriptComponentToTick(this.GetTickRequirement());
        }
        protected override void OnEditorInit()
        {
            base.OnEditorInit();
        }

        public override ScriptComponentBehavior.TickRequirement GetTickRequirement()
        {
            return ScriptComponentBehavior.TickRequirement.Tick;
        }
    }
}
