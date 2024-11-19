using SandBox;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;


namespace Europe_LemmyProject.General
{
    // Token: 0x02000004 RID: 4
    public class BetterSettlementPositionScript : ScriptComponentBehavior
	{
		// Token: 0x17000001 RID: 1
		// (get) Token: 0x06000003 RID: 3 RVA: 0x00002058 File Offset: 0x00000258
		private string SettlementsXmlPath
		{
			get
			{
				return ModuleHelper.GetModuleFullPath(ModuleName) + "ModuleData/settlements.xml";
			}
		}

		// Token: 0x17000002 RID: 2
		// (get) Token: 0x06000004 RID: 4 RVA: 0x0000206E File Offset: 0x0000026E
		private string SettlementsDistanceCacheFilePath
		{
			get
			{
				return ModuleHelper.GetModuleFullPath(ModuleName) + "ModuleData/settlements_distance_cache.bin";
			}
		}

		public string ModuleName = "SandBox";

		// Token: 0x06000005 RID: 5 RVA: 0x00002084 File Offset: 0x00000284
		protected override void OnEditorVariableChanged(string variableName)
		{
			base.OnEditorVariableChanged(variableName);
			if (variableName == "SavePositions")
			{
				this.SaveSettlementPositions();
			}
			if (variableName == "ComputeAndSaveSettlementDistanceCache")
			{
				this.SaveSettlementDistanceCache();
			}
			if (variableName == "CheckPositions")
			{
				this.CheckSettlementPositions();
			}
		}

		// Token: 0x06000006 RID: 6 RVA: 0x000020D1 File Offset: 0x000002D1
		protected override void OnSceneSave(string saveFolder)
		{
			base.OnSceneSave(saveFolder);
			this.SaveSettlementPositions();
		}

		// Token: 0x06000007 RID: 7 RVA: 0x000020E0 File Offset: 0x000002E0
		private void CheckSettlementPositions()
		{
			XmlDocument xmlDocument = this.LoadXmlFile(this.SettlementsXmlPath);
			base.GameEntity.RemoveAllChildren();
			foreach (object obj in xmlDocument.DocumentElement.SelectNodes("Settlement"))
			{
				string value = ((XmlNode)obj).Attributes["id"].Value;
				GameEntity campaignEntityWithName = base.Scene.GetCampaignEntityWithName(value);
				Vec3 origin = campaignEntityWithName.GetGlobalFrame().origin;
				Vec3 vec = default(Vec3);
				List<GameEntity> list = new List<GameEntity>();
				campaignEntityWithName.GetChildrenRecursive(ref list);
				bool flag = false;
				foreach (GameEntity gameEntity in list)
				{
					if (gameEntity.HasTag("main_map_city_gate"))
					{
						vec = gameEntity.GetGlobalFrame().origin;
						flag = true;
						break;
					}
				}
				Vec3 pos = origin;
				if (flag)
				{
					pos = vec;
				}
				PathFaceRecord pathFaceRecord = new PathFaceRecord(-1, -1, -1);
				base.GameEntity.Scene.GetNavMeshFaceIndex(ref pathFaceRecord, pos.AsVec2, true, false);
				int num = 0;
				if (pathFaceRecord.IsValid())
				{
					num = pathFaceRecord.FaceGroupIndex;
				}
				if (num == 0 || num == 7 || num == 8 || num == 10 || num == 11 || num == 13 || num == 14)
				{
					MBEditor.ZoomToPosition(pos);
					break;
				}
			}
		}

		// Token: 0x06000008 RID: 8 RVA: 0x00002284 File Offset: 0x00000484
		protected override void OnInit()
		{
			try
			{
				Debug.Print("SettlementsDistanceCacheFilePath: " + this.SettlementsDistanceCacheFilePath, 0, Debug.DebugColor.White, 17592186044416UL);
				System.IO.BinaryReader binaryReader = new System.IO.BinaryReader(File.Open(this.SettlementsDistanceCacheFilePath, FileMode.Open, FileAccess.Read));
				if (Campaign.Current.Models.MapDistanceModel is DefaultMapDistanceModel)
				{
					((DefaultMapDistanceModel)Campaign.Current.Models.MapDistanceModel).LoadCacheFromFile(binaryReader);
				}
				binaryReader.Close();
			}
			catch
			{
				Debug.FailedAssert("SettlementsDistanceCacheFilePath could not be read!. Campaign performance will be affected very badly.", "C:\\Develop\\mb3\\Source\\Bannerlord\\SandBox.View\\Map\\SettlementPositionScript.cs", "OnInit", 159);
				Debug.Print("SettlementsDistanceCacheFilePath could not be read!. Campaign performance will be affected very badly.", 0, Debug.DebugColor.White, 17592186044416UL);
			}
		}

		// Token: 0x06000009 RID: 9 RVA: 0x00002340 File Offset: 0x00000540
		private List<SettlementRecord> LoadSettlementData(XmlDocument settlementDocument)
		{
			List<SettlementRecord> list = new List<SettlementRecord>();
			base.GameEntity.RemoveAllChildren();
			foreach (object obj in settlementDocument.DocumentElement.SelectNodes("Settlement"))
			{
				XmlNode xmlNode = (XmlNode)obj;
				string value = xmlNode.Attributes["name"].Value;
				string value2 = xmlNode.Attributes["id"].Value;
				GameEntity campaignEntityWithName = base.Scene.GetCampaignEntityWithName(value2);
				if (!(campaignEntityWithName == null))
				{
					Vec2 asVec = campaignEntityWithName.GetGlobalFrame().origin.AsVec2;
					Vec2 vec = default(Vec2);
					List<GameEntity> list2 = new List<GameEntity>();
					campaignEntityWithName.GetChildrenRecursive(ref list2);
					bool flag = false;
					foreach (GameEntity gameEntity in list2)
					{
						if (gameEntity.HasTag("main_map_city_gate"))
						{
							vec = gameEntity.GetGlobalFrame().origin.AsVec2;
							flag = true;
						}
					}
					list.Add(new SettlementRecord(value, value2, asVec, flag ? vec : asVec, xmlNode, flag));
				}
			}
			return list;
		}

		// Token: 0x0600000A RID: 10 RVA: 0x000024C8 File Offset: 0x000006C8
		private void SaveSettlementPositions()
		{
			XmlDocument xmlDocument = this.LoadXmlFile(this.SettlementsXmlPath);
			foreach (SettlementRecord settlementRecord in this.LoadSettlementData(xmlDocument))
			{
				if (settlementRecord.Node.Attributes["posX"] == null)
				{
					XmlAttribute node = xmlDocument.CreateAttribute("posX");
					settlementRecord.Node.Attributes.Append(node);
				}
				settlementRecord.Node.Attributes["posX"].Value = settlementRecord.Position.X.ToString();
				if (settlementRecord.Node.Attributes["posY"] == null)
				{
					XmlAttribute node2 = xmlDocument.CreateAttribute("posY");
					settlementRecord.Node.Attributes.Append(node2);
				}
				settlementRecord.Node.Attributes["posY"].Value = settlementRecord.Position.Y.ToString();
				if (settlementRecord.HasGate)
				{
					if (settlementRecord.Node.Attributes["gate_posX"] == null)
					{
						XmlAttribute node3 = xmlDocument.CreateAttribute("gate_posX");
						settlementRecord.Node.Attributes.Append(node3);
					}
					settlementRecord.Node.Attributes["gate_posX"].Value = settlementRecord.GatePosition.X.ToString();
					if (settlementRecord.Node.Attributes["gate_posY"] == null)
					{
						XmlAttribute node4 = xmlDocument.CreateAttribute("gate_posY");
						settlementRecord.Node.Attributes.Append(node4);
					}
					settlementRecord.Node.Attributes["gate_posY"].Value = settlementRecord.GatePosition.Y.ToString();
				}
			}
			xmlDocument.Save(this.SettlementsXmlPath);
		}

		// Token: 0x0600000B RID: 11 RVA: 0x000026EC File Offset: 0x000008EC
		private void SaveSettlementDistanceCache()
		{
			System.IO.BinaryWriter binaryWriter = null;
			try
			{
				XmlDocument settlementDocument = this.LoadXmlFile(this.SettlementsXmlPath);
				List<SettlementRecord> list = this.LoadSettlementData(settlementDocument);
				int mountainIndex = MapScene.GetNavigationMeshIndexOfTerrainType(TerrainType.Mountain);
				int lakeIndex = MapScene.GetNavigationMeshIndexOfTerrainType(TerrainType.Lake);
				int waterIndex = MapScene.GetNavigationMeshIndexOfTerrainType(TerrainType.Water);
				int riverIndex = MapScene.GetNavigationMeshIndexOfTerrainType(TerrainType.River);
				int canyonIndex = MapScene.GetNavigationMeshIndexOfTerrainType(TerrainType.Canyon);
				int ruralAreaIndex = MapScene.GetNavigationMeshIndexOfTerrainType(TerrainType.RuralArea);
				base.Scene.SetAbilityOfFacesWithId(mountainIndex, false);
				base.Scene.SetAbilityOfFacesWithId(lakeIndex, false);
				// base.Scene.SetAbilityOfFacesWithId(navigationMeshIndexOfTerrainType3, false);
				// base.Scene.SetAbilityOfFacesWithId(navigationMeshIndexOfTerrainType4, false);
				base.Scene.SetAbilityOfFacesWithId(canyonIndex, false);
				base.Scene.SetAbilityOfFacesWithId(ruralAreaIndex, false);
				binaryWriter = new System.IO.BinaryWriter(File.Open(this.SettlementsDistanceCacheFilePath, FileMode.Create));
				binaryWriter.Write(list.Count);
				for (int i = 0; i < list.Count; i++)
				{
					binaryWriter.Write(list[i].SettlementId);
					Vec2 gatePosition = list[i].GatePosition;
					PathFaceRecord pathFaceRecord = new PathFaceRecord(-1, -1, -1);
					base.Scene.GetNavMeshFaceIndex(ref pathFaceRecord, gatePosition, false, false);
					for (int j = i + 1; j < list.Count; j++)
					{
						binaryWriter.Write(list[j].SettlementId);
						Vec2 gatePosition2 = list[j].GatePosition;
						PathFaceRecord pathFaceRecord2 = new PathFaceRecord(-1, -1, -1);
						base.Scene.GetNavMeshFaceIndex(ref pathFaceRecord2, gatePosition2, false, false);
						float value;
						base.Scene.GetPathDistanceBetweenAIFaces(pathFaceRecord.FaceIndex, pathFaceRecord2.FaceIndex, gatePosition, gatePosition2, 0.1f, float.MaxValue, out value);
						binaryWriter.Write(value);
					}
				}
				int navMeshFaceCount = base.Scene.GetNavMeshFaceCount();
				for (int k = 0; k < navMeshFaceCount; k++)
				{
					int idOfNavMeshFace = base.Scene.GetIdOfNavMeshFace(k);
					if (idOfNavMeshFace != mountainIndex && idOfNavMeshFace != lakeIndex && idOfNavMeshFace != canyonIndex && idOfNavMeshFace != ruralAreaIndex)
					{
						Vec3 zero = Vec3.Zero;
						base.Scene.GetNavMeshCenterPosition(k, ref zero);
						Vec2 asVec = zero.AsVec2;
						float num = float.MaxValue;
						string value2 = "";
						for (int l = 0; l < list.Count; l++)
						{
							Vec2 gatePosition3 = list[l].GatePosition;
							PathFaceRecord pathFaceRecord3 = new PathFaceRecord(-1, -1, -1);
							base.Scene.GetNavMeshFaceIndex(ref pathFaceRecord3, gatePosition3, false, false);
							float num2;
							if ((num == 3.4028235E+38f || asVec.DistanceSquared(gatePosition3) < num * num) && base.Scene.GetPathDistanceBetweenAIFaces(k, pathFaceRecord3.FaceIndex, asVec, gatePosition3, 0.1f, num, out num2) && num2 < num)
							{
								num = num2;
								value2 = list[l].SettlementId;
							}
						}
						if (!string.IsNullOrEmpty(value2))
						{
							binaryWriter.Write(k);
							binaryWriter.Write(value2);
						}
					}
				}
				binaryWriter.Write(-1);
			}
			catch
			{
			}
			finally
			{
				base.Scene.SetAbilityOfFacesWithId(MapScene.GetNavigationMeshIndexOfTerrainType(TerrainType.Mountain), true);
				base.Scene.SetAbilityOfFacesWithId(MapScene.GetNavigationMeshIndexOfTerrainType(TerrainType.Lake), true);
				base.Scene.SetAbilityOfFacesWithId(MapScene.GetNavigationMeshIndexOfTerrainType(TerrainType.Water), true);
				base.Scene.SetAbilityOfFacesWithId(MapScene.GetNavigationMeshIndexOfTerrainType(TerrainType.River), true);
				base.Scene.SetAbilityOfFacesWithId(MapScene.GetNavigationMeshIndexOfTerrainType(TerrainType.Canyon), true);
				base.Scene.SetAbilityOfFacesWithId(MapScene.GetNavigationMeshIndexOfTerrainType(TerrainType.RuralArea), true);
				if (binaryWriter != null)
				{
					binaryWriter.Close();
				}
			}
		}

		// Token: 0x0600000C RID: 12 RVA: 0x00002A94 File Offset: 0x00000C94
		private XmlDocument LoadXmlFile(string path)
		{
			Debug.Print("opening " + path, 0, Debug.DebugColor.White, 17592186044416UL);
			XmlDocument xmlDocument = new XmlDocument();
			StreamReader streamReader = new StreamReader(path);
			string xml = streamReader.ReadToEnd();
			xmlDocument.LoadXml(xml);
			streamReader.Close();
			return xmlDocument;
		}

		// Token: 0x0600000D RID: 13 RVA: 0x00002ADD File Offset: 0x00000CDD
		protected override bool IsOnlyVisual()
		{
			return true;
		}

		// Token: 0x04000001 RID: 1
		public SimpleButton CheckPositions;

		// Token: 0x04000002 RID: 2
		public SimpleButton SavePositions;

		// Token: 0x04000003 RID: 3
		public SimpleButton ComputeAndSaveSettlementDistanceCache;

		// Token: 0x0200005E RID: 94
		private struct SettlementRecord
		{
			// Token: 0x060003C9 RID: 969 RVA: 0x0002144F File Offset: 0x0001F64F
			public SettlementRecord(string settlementName, string settlementId, Vec2 position, Vec2 gatePosition, XmlNode node, bool hasGate)
			{
				this.SettlementName = settlementName;
				this.SettlementId = settlementId;
				this.Position = position;
				this.GatePosition = gatePosition;
				this.Node = node;
				this.HasGate = hasGate;
			}

			// Token: 0x040001F7 RID: 503
			public readonly string SettlementName;

			// Token: 0x040001F8 RID: 504
			public readonly string SettlementId;

			// Token: 0x040001F9 RID: 505
			public readonly XmlNode Node;

			// Token: 0x040001FA RID: 506
			public readonly Vec2 Position;

			// Token: 0x040001FB RID: 507
			public readonly Vec2 GatePosition;

			// Token: 0x040001FC RID: 508
			public readonly bool HasGate;
		}
	}
}
