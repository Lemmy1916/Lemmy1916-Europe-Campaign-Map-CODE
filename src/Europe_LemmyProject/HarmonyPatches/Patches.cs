#define V1_0_2

using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using System.Xml;
using System.Linq;

using HarmonyLib;

using SandBox;
using StoryMode;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;
using TaleWorlds.ObjectSystem;
using TaleWorlds.Engine;
using SandBox.View.Map;
using SandBox.ViewModelCollection.Nameplate;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;

using Europe_LemmyProject.MaritimeSystem;
using Europe_LemmyProject.MaritimeSystem.ShipManagementUI;
using System.Reflection;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Election;

namespace Europe_LemmyProject
{
    [HarmonyPatch]
    public static class Patches
    {
        private static Dictionary<string, Vec2> _startingPoints;
        private static readonly string SettlementsDistanceCacheFilePath = ModuleHelper.GetModuleFullPath("Europe_LemmyProject") + "ModuleData/settlements_distance_cache.bin";

        #region Visual Patches
        /// <summary>
        /// Changes the visual position of a party that is lower than the map scene's water level 
        /// to be at water level. Adjusts the position of a party in an Army so ship models don't collide.
        /// </summary>
        /// <remarks>
        /// Weird oscillating of ship's position when in army.
        /// </remarks>
        /// <param name="__instance"></param>
        /// <param name="__result"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MobileParty), "GetVisualPosition")]
        public static void MobileParty_GetVisualPosition_Patch(MobileParty __instance, ref Vec3 __result)
        {
            // adjust x-y position if the party is at sea and in army so boat meshes don't collide in arym
            if (__instance.Army != null && __instance.Army.LeaderParty.AttachedParties.Contains(__instance) && MaritimeManager.Instance.IsPartyAtSea(__instance))
            {
                Vec3 add = new Vec3(__instance.ArmyPositionAdder, 0f);
                __result += +1.5f * add;
            }

            // adjust visual z position to be at water level if below
            float waterLevel = (Campaign.Current.MapSceneWrapper as MapScene).Scene.GetWaterLevel();
            float z = 0f;
            Campaign.Current.MapSceneWrapper.GetHeightAtPoint(__result.AsVec2, ref z);
            if (z < waterLevel)
            {
                __result = new Vec3(__result.AsVec2, waterLevel);
            }
        }

        /// <summary>
        /// If for whatever reason a party at sea is not using it's ship mesh, make it. This 
        /// shouldn't be necessary, but it works for now.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="party"></param>
        /// <param name="clearBannerComponentCache"></param>
        /// <param name="clearBannerEntityCache"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PartyVisual), "AddMobileIconComponents")]
        public static void PartyVisual_AddMobileIconComponents_Patch(PartyVisual __instance, PartyBase party, ref bool clearBannerComponentCache, ref bool clearBannerEntityCache)
        {
            bool isAtSea = MaritimeManager.Instance.IsPartyAtSea(party.MobileParty);
            if (party.IsMobile && isAtSea)
            {
                string prefabName = MaritimeManager.GetShipPrefab(party.MobileParty);
                MaritimeManager.UseBoatPrefab(party.MobileParty, prefabName);
            }
        }

        public static SoundPlayer wavesSound = null;
        public static GameEntity wavesGameEntity = null;

        /// <summary>
        /// Stops the army, caravan, horse, and on-foot sounds from playing when the party is at sea.
        /// These sounds are hardcoded, so just muting for now.
        /// </summary>
        /// <param name="party"></param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MapScreen), "StepSounds")]
        public static bool MapScreen_StepSounds_Patch(MobileParty party)
        {
            /**
            if (party.IsMainParty && wavesSound == null)
            {
                wavesSound = new SoundPlayer();
                Scene scene = ((MapScene)Campaign.Current.MapSceneWrapper).Scene;
                wavesGameEntity = ((PartyVisual)party.Party.Visuals).StrategicEntity;
                wavesGameEntity.CreateAndAddScriptComponent("SoundPlayer");
                wavesSound = wavesGameEntity.GetFirstScriptOfType<SoundPlayer>();
                wavesSound.SoundName = "event:/mission/ambient/detail/waves_big";

            }

            if (party.IsMainParty && MaritimeManager.Instance.IsPartyAtSea(party))
            {
                int soundId = SoundEvent.GetEventIdFromString("event:/mission/ambient/detail/waves_big");
                Vec3 position = ((PartyVisual)party.Party.Visuals).StrategicEntity.GlobalPosition;
                MBSoundEvent.PlaySound(soundId, ref position);
                return false;
            }
            else if (party.IsMainParty && !MaritimeManager.Instance.IsPartyAtSea(party))
            {

            }
            */

            if (MaritimeManager.Instance.IsPartyAtSea(party))
                return false;
            return true;
        }

        /// <summary>
        /// Necessary to keep ai parties using their boat mesh when fading away. Without this, 
        /// parties that are fading in will be using both their boat mesh and their standard mesh.
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PartyVisual), "TickFadingState")]
        public static void PartyVisual_TickFadingState_Patch(PartyVisual __instance)
        {
            MobileParty party = MaritimeManager.Instance.PartiesAtSea.FirstOrDefault(x => x?.Party.Visuals == __instance);
            if (party != null)
            {
                MaritimeManager.DisappearPartyVisuals(party);
            }
        }

        /// <summary>
        /// Makes camera stay above the water level.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="cameraTarget"></param>
        /// <param name="cameraBearing"></param>
        /// <param name="cameraElevation"></param>
        /// <param name="cameraDistance"></param>
        /// <param name="lastUsedIdealCameraTarget"></param>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MapScreen), "ComputeMapCamera")]
        public static void MapScreen_ComputeMapCamera_Patch(MapScreen __instance, ref Vec3 cameraTarget, float cameraBearing, ref float cameraElevation, float cameraDistance, ref Vec2 lastUsedIdealCameraTarget)
        {
            float waterLevel = (Campaign.Current.MapSceneWrapper as MapScene).Scene.GetWaterLevel();
            if (cameraTarget.Z < waterLevel)
            {
                cameraTarget = new Vec3(cameraTarget.AsVec2, waterLevel);
            }
        }

        /// <summary>
        /// Necessary to keep ship mesh for traveling at sea when returning to the campaign map 
        /// after exiting party, inventory, or other similar menus.
        /// </summary>
        /// <param name="__instance"></param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PartyVisual), "SetMapIconAsDirty")]
        public static bool PartyVisual_SetMapIconAsDirty_Patch(PartyVisual __instance)
        {
            MobileParty party = __instance.GetMapEntity() as MobileParty;
            if (party != null && MaritimeManager.Instance.PartiesAtSea.Contains(party))
                return false;
            return true;
        }

        /// <summary>
        /// Makes it so that Port nameplates have the same view distance as Towns.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="__result"></param>
        /// <param name="cameraPosition"></param>
        /// <returns></returns>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SettlementNameplateVM), "IsVisible")]
        public static void SettlementNameplateVM_IsVisible_Patch(SettlementNameplateVM __instance, ref bool __result, Vec3 cameraPosition)
        {
            if (__instance.IsTracked)
                return;
            
            bool isPort = __instance.Settlement.SettlementComponent is Port;
            bool isLandingArea = __instance.Settlement.SettlementComponent is LandingArea;
            bool isFerryPoint = __instance.Settlement.SettlementComponent is FerryTravelPoint;
            bool isAtSea = MaritimeManager.Instance.IsPartyAtSea(MobileParty.MainParty);
            if (isAtSea)
            {
                if (isPort)
                {
                    if (__instance.WPos < 0f || !__instance.IsInside)
                    {
                        __result = false;
                    }
                    else if (cameraPosition.Z > 400f)
                    {
                        __result = true;
                    }
                    else
                    {
                        __result = __instance.DistanceToCamera < cameraPosition.z + 60f;
                    }
                    // if is a port and player is at sea, then treat the same as a town
                }
                else if (isLandingArea || isFerryPoint)
                {
                    if (__instance.WPos < 0f || !__instance.IsInside)
                    {
                        __result = false;
                    }
                    else if (cameraPosition.Z > 200f)
                    {
                        __result = false;
                    }
                    else
                    {
                        __result = __instance.DistanceToCamera < cameraPosition.z + 60f;
                    }
                }
                else
                    __result = false;
            }
            else 
            {
                // landing areas are only visible when on land if they hold the player's ships
                if (isLandingArea) {
                    if(MobilePartyWrapper.MainPartyWrapper.StoredFleets != null && MobilePartyWrapper.MainPartyWrapper.StoredFleets.Any(x => x.Settlement == __instance.Settlement))
                    {
                        __result = true;
                    }
                    else
                    {
                        __result = false;
                    }
                }
                else if (isPort)
                {
                    __result = false;
                }
            }
        }

        /// <summary>
        /// Makes sure that when an army is at sea, attached parties don't clip onto land.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="mobileParty"></param>
        /// <param name="armyFacing"></param>
        /// <param name="__result"></param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Army), "GetRelativePositionForParty")]
        public static bool Army_GetRelativePositionFor_Party(Army __instance, MobileParty mobileParty, Vec2 armyFacing, ref Vec2 __result)
        {
            if (MaritimeManager.Instance.IsPartyAtSea(mobileParty))
            {
                float num = 0.5f;
                float num2 = (float)MathF.Ceiling(-1f + MathF.Sqrt(1f + 8f * (float)(__instance.LeaderParty.AttachedParties.Count - 1))) / 4f * num * 0.5f + num;
                int num3 = -1;
                for (int i = 0; i < __instance.LeaderParty.AttachedParties.Count; i++)
                {
                    if (__instance.LeaderParty.AttachedParties[i] == mobileParty)
                    {
                        num3 = i;
                        break;
                    }
                }
                int num4 = MathF.Ceiling((-1f + MathF.Sqrt(1f + 8f * (float)(num3 + 2))) / 2f) - 1;
                int num5 = num3 + 1 - num4 * (num4 + 1) / 2;
                bool flag = (num4 & 1) != 0;
                num5 = ((((num5 & 1) != 0) ? (-num5 - 1) : num5) >> 1) * (flag ? -1 : 1);
                float num6 = 1.25f;
                Vec2 geometricCenter = Vec2.Zero;
                foreach (MobileParty mobileParty2 in __instance.LeaderPartyAndAttachedParties)
                {
                    geometricCenter.x += mobileParty2.VisualPosition2DWithoutError.x;
                    geometricCenter.y += mobileParty2.VisualPosition2DWithoutError.y;
                }
                geometricCenter = new Vec2(geometricCenter.x / (float)__instance.LeaderPartyAndAttachedParties.Count<MobileParty>(), geometricCenter.y / (float)__instance.LeaderPartyAndAttachedParties.Count<MobileParty>());
                Vec2 vec = geometricCenter - (float)MathF.Sign((float)num5 - (((num4 & 1) != 0) ? 0.5f : 0f)) * armyFacing.LeftVec() * num2;
                PathFaceRecord faceIndex = Campaign.Current.MapSceneWrapper.GetFaceIndex(geometricCenter);
                if (geometricCenter != vec)
                {
                    Vec2 lastPointWithinNavigationMeshForLineToPoint = Vec2.Invalid;
                    #if V1_0_1
                    lastPointWithinNavigationMeshForLineToPoint = Campaign.Current.MapSceneWrapper.GetLastPointWithinNavigationMeshForLineToPoint(faceIndex, geometricCenter, vec);
                    #endif

                    #if V1_0_2
                    lastPointWithinNavigationMeshForLineToPoint = Campaign.Current.MapSceneWrapper.GetLastPointOnNavigationMeshFromPositionToDestination(faceIndex, geometricCenter, vec);
                    #endif

                    if (MaritimeManager.IsOverLand(lastPointWithinNavigationMeshForLineToPoint))
                    {
                        // find new point that is at sea
                        #if V1_0_1
                        lastPointWithinNavigationMeshForLineToPoint = Campaign.Current.MapSceneWrapper.GetLastPointWithinNavigationMeshForLineToPoint(faceIndex, vec, geometricCenter);
                        #endif

                        #if V1_0_2
                        lastPointWithinNavigationMeshForLineToPoint = Campaign.Current.MapSceneWrapper.GetLastPointOnNavigationMeshFromPositionToDestination(faceIndex, vec, geometricCenter);
                        #endif
                    }

                    if ((vec - lastPointWithinNavigationMeshForLineToPoint).LengthSquared > 2.25E-06f)
                    {
                        num = num * (geometricCenter - lastPointWithinNavigationMeshForLineToPoint).Length / num2;
                        num6 = num6 * (geometricCenter - lastPointWithinNavigationMeshForLineToPoint).Length / (num2 / 1.5f);
                    }
                }
                __result = new Vec2((flag ? (-num * 0.5f) : 0f) + (float)num5 * num + mobileParty.Party.RandomFloat(-0.25f, 0.25f) * 0.6f * num, ((float)(-(float)num4) + mobileParty.Party.RandomFloatWithSeed(1U, -0.25f, 0.25f)) * num6 * 0.3f);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Makes parts of the MapBar ui disappear when ShipManagement ui is active like is done 
        /// natively for Encyclopedia ui.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="value"></param>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MapTimeControlVM), "IsCenterPanelEnabled", MethodType.Setter)]
        public static void MapTimeControlVM_IsCenterPanelEnabled_Patch(MapTimeControlVM __instance, ref bool value)
        {
            if (ShipManagementUIManager.Instance._shipManagementUIGauntlet!= null)
            {
                value = false;
            }
        }
#endregion

#region Mechanical Patches

        /// <summary>
        /// Sets the traveling player to fast forward automatically when traveling by maritime path.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MapState), "ExitMenuMode")]
        public static void MapState_ExitMenuMode_Patch()
        {
            if (MaritimeManager.Instance?.CurrentMaritimePathTravel != null)
                Campaign.Current.TimeControlMode = CampaignTimeControlMode.UnstoppableFastForward;
        }

        /// <summary>
        /// Maybe this will allow a circle to be rendered at sea?
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="__result"></param>
        /// <param name="position"></param>
        /// <param name="checkHoles"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Scene), "GetTerrainHeight")]
        public static void Scene_GetTerrainHeight_Patch(Scene __instance, ref float __result, Vec2 position, bool checkHoles)
        {
            if (__instance == (Campaign.Current.MapSceneWrapper as MapScene)?.Scene)
            {
                if(__result < __instance.GetWaterLevel())
                {
                    __result = __instance.GetWaterLevel() - 0.05f;
                }
            }
        }

        /// <summary>
        /// Makes GameEntity's that have physcis invisible for towns, castles, and villages.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="dt"></param>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MapScreen), "HandleMouse")]
        public static void MapScreen_HandleMouse_Patch(MapScreen __instance, float dt)
        {
            if (Campaign.Current.GameStarted)
            {
                if (__instance.Input.IsGameKeyDown(General.ELPHotKeys.MapGhostSettlementPhysics))
                {
                    List<GameEntity> settlementColliders = Campaign.Current.Settlements
                        .Where(x => x.IsTown || x.IsCastle || x.IsVillage)
                        .Select(x => (x.Party.Visuals as PartyVisual).StrategicEntity)
                        .SelectMany(x => x.GetChildren())
                        .Where(x => x.HasPhysicsBody())
                        .ToList();
                    settlementColliders.ForEach(x => x.SetVisibilityExcludeParents(false));
                }
                else
                {
                    List<GameEntity> entities = Campaign.Current.Settlements
                        .Where(x => x.IsTown || x.IsCastle || x.IsVillage)
                        .Select(x => (x.Party.Visuals as PartyVisual).StrategicEntity)
                        .SelectMany(x => x.GetChildren())
                        .Where(x => x.HasPhysicsBody() && !x.IsVisibleIncludeParents())
                        .ToList();

                    if (entities.Count > 0)
                        entities.ForEach(x => x.SetVisibilityExcludeParents(true));
                }
            }
        }

        /// <summary>
        /// This patch regulates how a player can navigate to land from sea and from sea to land. 
        /// Overall purpose is to force the player to use a port to access the sea. 
        /// </summary>
        /// <remarks>
        /// Should make a settlement for the player to click on only when he is at sea. Even restricting 
        /// the player to only clicking on port settlements can still result in pathfinding taking the 
        /// player over land when he is still at sea.
        /// </remarks>
        /// <param name="selectedSiegeEntity"></param>
        /// <param name="visualOfSelectedEntity"></param>
        /// <param name="intersectionPoint"></param>
        /// <param name="mouseOverFaceIndex"></param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MapScreen), "HandleLeftMouseButtonClick")]
        public static bool MapScreen_HandleLeftMouseButtonClick_Patch(MapScreen __instance, GameEntity selectedSiegeEntity, ref IPartyVisual visualOfSelectedEntity, Vec3 intersectionPoint, ref PathFaceRecord mouseOverFaceIndex)
        {
            if (__instance.Input.IsControlDown() && Game.Current.CheatMode)
                return true;

            IMapEntity mapEntity = visualOfSelectedEntity != null ? visualOfSelectedEntity.GetMapEntity() : null;
            if (mapEntity != null && mapEntity.IsMainEntity())
                return true;

            if (selectedSiegeEntity != null)
                return true;

            bool validClick = true;

            bool isTravelingByPath = MaritimeManager.Instance.CurrentMaritimePathTravel != null;
            bool isAtSea = MaritimeManager.Instance.PartiesAtSea.Contains(MobileParty.MainParty);
            bool isMapEntity = mapEntity != null;
            Settlement targetSettlement = isMapEntity ? mapEntity as Settlement : null;
            MobileParty targetMobileParty = isMapEntity ? mapEntity as MobileParty : null;
            bool clickedOnQuestParty = targetMobileParty != null && targetMobileParty.IsCurrentlyUsedByAQuest;
            bool clickedOnPartyOnLand = targetMobileParty != null && !MaritimeManager.Instance.PartiesAtSea.Contains(targetMobileParty);
            bool clickedOnPortSettlement = targetSettlement != null && PortCampaignBehavior.IsPortSettlement(targetSettlement);
            bool clickedOnCasleOrVillage = targetSettlement != null && (targetSettlement.IsCastle || targetSettlement.IsVillage);

            // don't let player try to travel anywhere when tracked on maritime path
            if (isTravelingByPath)
            {
                if (MapScreen.Instance.SceneLayer.Input.GetIsMouseActive())
                {
                    MapScreen.Instance.CurrentCameraFollowMode = MapScreen.CameraFollowMode.FollowParty;
                    Campaign.Current.CameraFollowParty = PartyBase.MainParty;
                }
                validClick = false;
            }
            // else if the player is at sea only allow to interact with other paries at sea and ports
            else if (isAtSea)
            {
                // don't allow click on land if player is at sea
                if (isMapEntity)
                {
                    // but allow navigation to ports settlements and landing areas if at sea
                    Settlement settlement = mapEntity as Settlement;
                    // and also allow to interact with other parties at sea
                    MobileParty targetParty = mapEntity as MobileParty;

                    if (targetParty != null && targetParty.IsCurrentlyUsedByAQuest)
                    {
                        validClick = true;
                    }
                    else if (targetParty != null && !MaritimeManager.Instance.PartiesAtSea.Contains(targetParty))
                    {
                        validClick = false;
                    }
                    else if (settlement != null && PortCampaignBehavior.IsPortSettlement(settlement))
                    {
                        visualOfSelectedEntity = PortCampaignBehavior.Instance.ActivePorts.First(x => (x.SettlementComponent as Port).PortOf == settlement).Party.Visuals;
                        validClick = true;
                    }
                    else if (settlement != null && (settlement.IsVillage || settlement.IsCastle))
                    {
                        validClick = false;
                    }
                }
                else if (mouseOverFaceIndex.IsValid())
                {
                    // only allow navigation to other water faces while at sea
                    validClick = MaritimeManager.IsWaterBetween(MobileParty.MainParty.CurrentNavigationFace, mouseOverFaceIndex, MobileParty.MainParty.Position2D, intersectionPoint.AsVec2, 0.5f)
                        && MaritimeManager.IsOverNavigableWater(intersectionPoint.AsVec2);
                }
            }
            else // if the player is on land don't allow to interact with parties at sea
            {
                // don't allow click if the clicked entity is at sea, or if there's sea between the player party and the target position
                if (mapEntity != null)
                {                   
                    // don't allow navigation to settlement that has sea between it and the player on land
                    Settlement settlement = mapEntity as Settlement;
                    MobileParty targetParty = mapEntity as MobileParty;

                    if (settlement != null)
                    {
                        LandingArea landingArea = settlement.SettlementComponent as LandingArea;
                        Port port = landingArea != null ? null : settlement.SettlementComponent as Port;
                        if (landingArea != null)
                        {
                            PathFaceRecord faceIndex = Campaign.Current.MapSceneWrapper.GetFaceIndex(landingArea.LandedShipsPoint);
                            NavigationPath path2 = new NavigationPath();
                            Campaign.Current.MapSceneWrapper.GetPathBetweenAIFaces(MobileParty.MainParty.CurrentNavigationFace, faceIndex, MobileParty.MainParty.Position2D, landingArea.LandedShipsPoint, 0.1f, path2);
                            List<Vec2> points2 = points2 = path2.PathPoints.Where(x => x.IsNonZero()).ToList();
                            points2.Prepend(MobileParty.MainParty.Position2D);
                            bool isWaterBetween2 = MaritimeManager.IsWaterBetween(points2, 0.5f);
                            if (!isWaterBetween2)
                                validClick = true;
                            else
                                validClick = false;

                        }
                        else if (port != null)
                        {
                            //validClick = false;
                        }
                        else
                        {
                            PathFaceRecord faceIndex = Campaign.Current.MapSceneWrapper.GetFaceIndex(mapEntity.InteractionPosition);
                            validClick = !MaritimeManager.IsWaterBetween(MobileParty.MainParty.CurrentNavigationFace, faceIndex, MobileParty.MainParty.Position2D, mapEntity.InteractionPosition, 0.5f);
                        }
                    }
                    else if (targetParty != null)
                    {
                        // allow click if player is escorting the targeted party
                        bool isEscortedParty = targetParty.IsCurrentlyUsedByAQuest;
                        if (isEscortedParty)
                        {
                            validClick = true;
                        }
                        else 
                        {
                            PathFaceRecord faceIndex = Campaign.Current.MapSceneWrapper.GetFaceIndex(targetParty.Position2D);
                            validClick = !MaritimeManager.IsWaterBetween(MobileParty.MainParty.CurrentNavigationFace, faceIndex, MobileParty.MainParty.Position2D, mapEntity.InteractionPosition, 0.5f);
                        }
                    }
                }
                else if (mouseOverFaceIndex.IsValid())
                {
                    validClick = !MaritimeManager.IsWaterBetween(MobileParty.MainParty.CurrentNavigationFace, mouseOverFaceIndex, MobileParty.MainParty.Position2D, intersectionPoint.AsVec2, 0.5f)
                        && MaritimeManager.IsOverLand(intersectionPoint.AsVec2);
                }
            }

            if (!validClick)
            {
                visualOfSelectedEntity = null;
                mouseOverFaceIndex = PathFaceRecord.NullFaceRecord;
            }
                
           return true;
        }

        /// <summary>
        /// Makes cursor show cancel sign when hovering over land while at sea, over land that is, 
        /// separated by sea, or when at sea and hovering over land
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MapScreen), "CheckCursorState")]
        public static void MapScreen_CheckCursorState_Patch(MapScreen __instance)
        {
            // if cursor is already disabled, keep it that way
            if (__instance.SceneLayer.ActiveCursor == TaleWorlds.ScreenSystem.CursorType.Default)
            {
                // otherwise check for new sea-travel-related conditions
                Vec3 zero = Vec3.Zero;
                Vec3 zero2 = Vec3.Zero;
                __instance.SceneLayer.SceneView.TranslateMouse(ref zero, ref zero2, -1f);
                Vec3 vec = zero;
                Vec3 vec2 = zero2;
                PathFaceRecord faceRecord = PathFaceRecord.NullFaceRecord;
                float num;
                Vec3 vec3;
                __instance.GetCursorIntersectionPoint(ref vec, ref vec2, out num, out vec3, ref faceRecord, BodyFlags.CommonFocusRayCastExcludeFlags);
                MobileParty mainParty = MobileParty.MainParty;
                bool atSeaClickLand = MaritimeManager.Instance.IsPartyAtSea(mainParty) && !MaritimeManager.IsValidWaterTerrain(faceRecord);
                bool onLandClickSea = !MaritimeManager.Instance.IsPartyAtSea(mainParty) && !MaritimeManager.IsValidLandTerrain(faceRecord);
                if (onLandClickSea)
                    __instance.SceneLayer.ActiveCursor = TaleWorlds.ScreenSystem.CursorType.Disabled;
            }
        }

        /// <summary>
        /// Needed to make the owner clan of the Port the same as its associated Town
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="__result"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Settlement), "OwnerClan", MethodType.Getter)]
        public static void Settlement_OwnerClan_Patch(Settlement __instance, ref Clan __result)
        {
            Port port = __instance.SettlementComponent as Port;
            if (port != null)
            {
                __result = port.PortOf.OwnerClan;
            }
        }

        /// <summary>
        /// Makes the faction of th
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="__result"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Settlement), "MapFaction", MethodType.Getter)]
        public static void Settlement_MapFaction_Patch(Settlement __instance, ref IFaction __result)
        {
            if (__result == null && (__instance.SettlementComponent as Port) != null)
            {
                __result = (__instance.SettlementComponent as Port).MapFaction;
            }
        }

        /// <summary>
        /// Disables a party being able to flee to water from land or from land to water. Also reduces flee distance.
        /// </summary>
        /// <remarks>
        /// I decided to reduce the flee distance to 5. In vanilla whatever it was calculated as is always too far and 
        /// parties keep fleeing in the same direction even if the chasing party overtakes them.
        /// Note - Need to change the navmesh id of ferry crossings to something unique. That way fleein parties can't 
        ///        set their target point to that navmesh face and escape to the sea or land that way.
        /// </remarks>
        /// <param name="__instance"></param>
        /// <param name="__result"></param>
        /// <param name="targetPoint"></param>
        /// <param name="direction"></param>
        /// <param name="distance"></param>
        /// <param name="alternativePosition"></param>
        /// <param name="neededTriesForAlternative"></param>
        /// <param name="rotationChangeLimitAddition"></param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MobileParty), "GetAccessableTargetPointInDirection")]
        public static bool MobileParty_GetAccessableTargetPointInDirection_Patch(MobileParty __instance, ref bool __result, ref Vec2 targetPoint, Vec2 direction, float distance, Vec2 alternativePosition, int neededTriesForAlternative, float rotationChangeLimitAddition = 0.1f)
        {
            distance = 5f;
            targetPoint = __instance.Position2D;
            float num = 2f * rotationChangeLimitAddition;
            float num2 = 1f;
            __result = false;
            int num3 = 0;
            PathFaceRecord faceIndex;
            while (!__result)
            {
                Vec2 v = direction;
                float randomFloat = MBRandom.RandomFloat;
                v.RotateCCW((-0.5f + randomFloat) * num);
                targetPoint = __instance.Position2D + v * distance * num2;
                num3++;
                num += rotationChangeLimitAddition;
                num2 *= 0.97f;
                faceIndex = Campaign.Current.MapSceneWrapper.GetFaceIndex(targetPoint);

                // if a party is fleeing, don't allow it to suddenly flee to the sea from land or vice versa
                bool canFleeTo = MaritimeManager.Instance.IsPartyAtSea(__instance) ? MaritimeManager.IsValidWaterTerrain(faceIndex) :
                                                                                     MaritimeManager.IsValidLandTerrain(faceIndex);
                
                canFleeTo = __instance.IsFleeing() ? canFleeTo : true;

                if (!canFleeTo)
                {

                }

                if (faceIndex.IsValid() && canFleeTo && Campaign.Current.MapSceneWrapper.AreFacesOnSameIsland(faceIndex, __instance.CurrentNavigationFace, false) && (targetPoint.x > Campaign.Current.MinSettlementX - 50f || targetPoint.x > __instance.Position2D.x) && (targetPoint.y > Campaign.Current.MinSettlementY - 50f || targetPoint.y > __instance.Position2D.y) && (targetPoint.x < Campaign.Current.MaxSettlementX + 50f || targetPoint.x < __instance.Position2D.x) && (targetPoint.y < Campaign.Current.MaxSettlementY + 50f || targetPoint.y < __instance.Position2D.y))
                {
                    __result = (num3 >= neededTriesForAlternative || CheckIfThereIsAnyHugeObstacleBetweenPartyAndTarget(__instance, targetPoint));
                }
                if (num3 >= neededTriesForAlternative)
                {
                    __result = true;
                    targetPoint = alternativePosition;
                }
            }
            return false;
        }

        /// <summary>
        /// Helper method for MobileParty_GetAccessableTargetPointInDirection_Patch
        /// </summary>
        /// <param name="party"></param>
        /// <param name="newTargetPosition"></param>
        /// <returns></returns>
        public static bool CheckIfThereIsAnyHugeObstacleBetweenPartyAndTarget(MobileParty party, Vec2 newTargetPosition)
        {
            IMapScene mapSceneWrapper = Campaign.Current.MapSceneWrapper;
            Vec2 position2D = party.Position2D;
            //newTargetPosition - position2D;
            Vec2 position = new Vec2((newTargetPosition.x + position2D.x * 3f) * 0.25f, (newTargetPosition.y + position2D.y * 3f) * 0.25f);
            PathFaceRecord faceIndex = mapSceneWrapper.GetFaceIndex(position);
            Vec2 position2 = new Vec2((newTargetPosition.x + position2D.x) * 0.5f, (newTargetPosition.y + position2D.y) * 0.5f);
            PathFaceRecord faceIndex2 = mapSceneWrapper.GetFaceIndex(position2);
            Vec2 position3 = new Vec2((newTargetPosition.x * 3f + position2D.x) * 0.25f, (newTargetPosition.y * 3f + position2D.y) * 0.25f);
            PathFaceRecord faceIndex3 = mapSceneWrapper.GetFaceIndex(position3);
            return faceIndex.IsValid() && mapSceneWrapper.AreFacesOnSameIsland(faceIndex, party.CurrentNavigationFace, false) && faceIndex2.IsValid() && mapSceneWrapper.AreFacesOnSameIsland(faceIndex2, party.CurrentNavigationFace, false) && faceIndex3.IsValid() && mapSceneWrapper.AreFacesOnSameIsland(faceIndex3, party.CurrentNavigationFace, false);
        }

        /// <summary>
        /// Adds MobileParty to PartiesAtSea when move onto water face from land. Removes 
        /// MobileParty from PartiesAtSea when moves onto land from water. Cosmetic and other 
        /// changes are left to be handled by events in MaritimeManager.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MobileParty), "OnAiTick")]
        public static void MobileParty_OnAiTick_Patch(MobileParty __instance)
        {
            bool isOverWater = MaritimeManager.IsOverNavigableWater(__instance.VisualPosition2DWithoutError);
            bool isAtSea = MaritimeManager.Instance.PartiesAtSea.Contains(__instance);
            if (isOverWater && !isAtSea) // if over a water face, but not managed by MaritimeManager
            {
                MaritimeManager.Instance.AddToSea(__instance);
            }
            else if (!isOverWater && isAtSea) // if over a land face, but managed by MaritimeManager
            {
                // these conditions should make sure that parties in armies at sea won't be able to enter land 
                // while the army leader is at sea
                // due to army position adder, attached parties can sometimes clip onto land
                // these conditions keep them at sea
                if (__instance.Army == null || (__instance.Army != null && (__instance.Army.LeaderParty == __instance || !MaritimeManager.Instance.IsPartyAtSea(__instance.Army.LeaderParty))))
                    MaritimeManager.Instance.RemoveFromSea(__instance);
            }
            else if (isOverWater && isAtSea) // idk
            {

            }
            else if (!isOverWater && !isAtSea) // idk
            {

            }
        }

        /// <summary>
        /// For LandingAreas, make mobile party use the land point if they are on land and the 
        /// sea point if they are at sea.
        /// </summary>
        /// <remarks>
        /// Disabling for now as it's still a bit wonky.
        /// </remarks>
        /// <param name="dt"></param>
        /// <param name="mobileParty"></param>
        /// <param name="targetPoint"></param>
        /// <param name="neededMaximumDistanceForEncountering"></param>

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EncounterManager), "GetEncounterTargetPoint")]
        public static bool EncounterManager_GetEncounterTargetPoint_Patch(float dt, MobileParty mobileParty, ref Vec2 targetPoint, ref float neededMaximumDistanceForEncountering)
        {
            LandingArea landingArea = mobileParty.TargetSettlement?.SettlementComponent as LandingArea;
            if (landingArea != null)
            {
                if (!MaritimeManager.Instance.IsPartyAtSea(mobileParty))
                {
                    // set target to the position on land
                    targetPoint = landingArea.LandedShipsPoint;
                    neededMaximumDistanceForEncountering = 0.5f;
                }
                else
                {
                    targetPoint = landingArea.GatePosition;
                    neededMaximumDistanceForEncountering = 0.5f;
                }
                return false;
            }
            return true;
        }

        /// <summary>
        /// This should work? This patch is necessary to be able to use different gate positions.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="settlement"></param>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MobileParty), "SetMoveGoToSettlement")]
        public static bool MobileParty_SetMoveGoToSettlement_Patch(MobileParty __instance, Settlement settlement)
        {
            if(!__instance.IsMainParty)
                return true;

            LandingArea landingArea = settlement.SettlementComponent as LandingArea;
            Port port = landingArea == null ? settlement.SettlementComponent as Port : null;
            FerryTravelPoint ferryTravelPoint = port == null ? settlement.SettlementComponent as FerryTravelPoint : null;
            if (landingArea != null)
            {
                if (!MaritimeManager.Instance.IsPartyAtSea(MobileParty.MainParty))
                {
                    var _gatePosition = settlement.GetType().GetField("_gatePosition", BindingFlags.NonPublic | BindingFlags.Instance);
                    _gatePosition.SetValue(settlement, landingArea.LandedShipsPoint);
                }
                else
                {
                    var _gatePosition = settlement.GetType().GetField("_gatePosition", BindingFlags.NonPublic | BindingFlags.Instance);
                    _gatePosition.SetValue(settlement, landingArea.GatePosition);
                }
            }
            else if (port != null)
            {

            }
            else if (ferryTravelPoint != null)
            {
                if (MaritimeManager.Instance.IsPartyAtSea(__instance))
                {
                    __instance.SetMoveGoToPoint(settlement.GatePosition);
                    return false;
                }
            }
            return true;
        }
#endregion

#region Navmesh Patches
        /// <summary>
        /// Opens up water and river faces for navigation.
        /// </summary>
        /// <param name="__instance"></param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MapScene), "DisableUnwalkableNavigationMeshes")]
        public static bool DisableUnwalkableNavigationMeshesPatch(MapScene __instance)
        {
            __instance.Scene.SetAbilityOfFacesWithId(PortCampaignBehavior.CoastLineNavmeshID, false);
            __instance.Scene.SetAbilityOfFacesWithId(MapScene.GetNavigationMeshIndexOfTerrainType(TerrainType.Mountain), false);
            __instance.Scene.SetAbilityOfFacesWithId(MapScene.GetNavigationMeshIndexOfTerrainType(TerrainType.Lake), false);
            //__instance.Scene.SetAbilityOfFacesWithId(MapScene.GetNavigationMeshIndexOfTerrainType(TerrainType.Water), false);
            //__instance.Scene.SetAbilityOfFacesWithId(MapScene.GetNavigationMeshIndexOfTerrainType(TerrainType.River), false);
            __instance.Scene.SetAbilityOfFacesWithId(MapScene.GetNavigationMeshIndexOfTerrainType(TerrainType.Canyon), false);
            __instance.Scene.SetAbilityOfFacesWithId(MapScene.GetNavigationMeshIndexOfTerrainType(TerrainType.RuralArea), false);
            return false;
        }

        /// <summary>
        /// Makes sure valid and invalid terrain are what we want them to be.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="__result"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PartyBase), "IsPositionOkForTraveling")]
        public static bool IsPositionOkForTravelingPatch(PartyBase __instance, ref bool __result, Vec2 position)
        {
            IMapScene mapSceneWrapper = Campaign.Current.MapSceneWrapper;
            PathFaceRecord faceIndex = mapSceneWrapper.GetFaceIndex(position);
            if (!faceIndex.IsValid())
            {
                return false;
            }
            TerrainType faceTerrainType = mapSceneWrapper.GetFaceTerrainType(faceIndex);
            __result = MaritimeManager.IsValidWaterTerrain(faceTerrainType) || MaritimeManager.IsValidLandTerrain(faceTerrainType);

            return false;
        }

#endregion

#region Gameplay Patches
#region Better War Decision
        /// <summary>
        /// This makes it so that kingdoms only consider nearby kingdoms to declare war on. This 
        /// should make it so that wars happen more frequently, since a kingdom is more likely to 
        /// declare war if the target is nearby.
        /// </summary>
        /// <param name="clan"></param>
        /// <param name="__result"></param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(KingdomDecisionProposalBehavior), "GetRandomWarDecision")]
        public static bool KingdomDecisionProposalBehavior_GetRandomWarDecision_Patch(Clan clan, ref KingdomDecision __result)
        {
            Kingdom kingdom = clan.Kingdom;
            if (kingdom.UnresolvedDecisions.FirstOrDefault((KingdomDecision x) => x is DeclareWarDecision) != null)
            {
                __result = null;
            }
            // try to make it so that kindoms only declare war on kingdoms that they border
            // so that we don't have the kuzaits declaring war on the battanians at the start
            // 
            // this defines whether or not a kingdom borders another if at least one settlement of 
            // one kingdom is at most 50 units away from at least one settlement of the other 
            // kingdom
            float borderMax = 50f;
            List<Settlement> kingdomForts= kingdom.Settlements.Where(x => x.IsFortification).ToList();
            List<Kingdom> otherKingdoms = Kingdom.All.Where(x => x != kingdom).ToList();
            List<Kingdom> borderedKingdoms = new List<Kingdom>();
            foreach (Kingdom otherKingdom in otherKingdoms)
            {
                List<Settlement> otherKingdomForts = otherKingdom.Settlements.Where(x => x.IsFortification).ToList();
                bool doesBorder = kingdomForts.Any(x => otherKingdomForts.Any(y => (x.Position2D - y.Position2D).Length <= borderMax));
                if (doesBorder)
                    borderedKingdoms.Add(otherKingdom);
            }

            Kingdom randomElementWithPredicate = borderedKingdoms.GetRandomElementWithPredicate((Kingdom x) => x != kingdom && !x.IsAtWarWith(kingdom) && x.GetStanceWith(kingdom).PeaceDeclarationDate.ElapsedDaysUntilNow > 20f);
            if (randomElementWithPredicate != null && ConsiderWar(clan, kingdom, randomElementWithPredicate))
            {
                __result = new DeclareWarDecision(clan, randomElementWithPredicate);
            }

            return false;
        }

        /// <summary>
        /// Helper method for KingdomDecisionProposalBehavior_GetRandomWarDecision_Patch.
        /// </summary>
        /// <param name="clan"></param>
        /// <param name="kingdom"></param>
        /// <param name="otherFaction"></param>
        /// <returns></returns>
        private static bool ConsiderWar(Clan clan, Kingdom kingdom, IFaction otherFaction)
        {
            int num = Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfProposingWar(kingdom) / 2;
            if (clan.Influence < (float)num)
            {
                return false;
            }
            DeclareWarDecision declareWarDecision = new DeclareWarDecision(clan, otherFaction);
            if (declareWarDecision.CalculateSupport(clan) > 50f)
            {
                float kingdomSupportForDecision = GetKingdomSupportForDecision(declareWarDecision);
                if (MBRandom.RandomFloat < 1.4f * kingdomSupportForDecision - 0.55f)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Helper method for KingdomDecisionProposalBehavior_GetRandomWarDecision_Patch.
        /// </summary>
        /// <param name="decision"></param>
        /// <returns></returns>
        private static float GetKingdomSupportForDecision(KingdomDecision decision)
        {
            return new KingdomElection(decision).GetLikelihoodForOutcome(0);
        }
#endregion

        /// <summary>
        /// Changes the default starting position to be appropriate to the custom map. for some reason 
        /// a default party is spawned on the campaign map at character creation. Then character creation 
        /// is basically a process of editing this default party. Problem is that the vanilla default 
        /// starting position is off our navmesh, so, if I remember right, a call to pathfinding causes a crash.
        /// </summary>
        /// <param name="__result"></param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Campaign), "DefaultStartingPosition", MethodType.Getter)]
        public static bool Campaign_NewDefaultStartingPosition_Patch(ref Vec2 __result)
        {
            XmlReaderSettings readerSettings = new XmlReaderSettings();
            readerSettings.IgnoreComments = true;
            string path = ModuleHelper.GetModuleFullPath("Europe_LemmyProject") + "ModuleData/ELPData/DefaultStartingPosition.xml";
            using (XmlReader reader = XmlReader.Create(path, readerSettings))
            {
                XmlDocument coordsDoc = new XmlDocument();
                coordsDoc.Load(reader);
                XmlNode root = coordsDoc.ChildNodes[1];
                float x = float.Parse(root.Attributes["posX"].Value);
                float y = float.Parse(root.Attributes["posY"].Value);
                __result = new Vec2(x, y);
            }
            return false;
        }

        /// <summary>
        /// Changes spawn position of new sandbox campaign to match the selected culture.
        /// </summary>
        /// <remarks>
        /// Should probably get rid of this and put it in a new CampaignBehaviorBase class.
        /// </remarks>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(SandboxCharacterCreationContent), "OnCharacterCreationFinalized")]
        public static bool SandboxCharacterCreationContent_CustomStartingPositions_Patch()
        {
            _startingPoints = new Dictionary<string, Vec2> { };
            XmlReaderSettings readerSettings = new XmlReaderSettings();
            readerSettings.IgnoreComments = true;
            string path = ModuleHelper.GetModuleFullPath("Europe_LemmyProject") + "ModuleData/ELPData/CultureStartPositions.xml";
            using (XmlReader reader = XmlReader.Create(path, readerSettings))
            {
                XmlDocument coordsDoc = new XmlDocument();
                coordsDoc.Load(reader);

                XmlNode root = coordsDoc.ChildNodes[1];
                foreach (XmlNode child in root.ChildNodes)
                {
                    _startingPoints.Add(child.Attributes["id"].Value,
                                        new Vec2(
                                            float.Parse(child.Attributes["start_pos_x"].Value),
                                            float.Parse(child.Attributes["start_pos_y"].Value))
                                        );
                }
            }

            CultureObject culture = CharacterObject.PlayerCharacter.Culture;
            Vec2 position2D;
            if (_startingPoints.TryGetValue(culture.StringId, out position2D))
            {
                MobileParty.MainParty.Position2D = position2D;
            }
            else
            {
                MobileParty.MainParty.Position2D = Campaign.Current.DefaultStartingPosition;
                TaleWorlds.Library.Debug.FailedAssert("Selected culture cannot be found in the starting positions dictionary. Harmony patch SandboxCharacterCreationContent.OnCharacterCreationFinalized in Europe_Lemmy_Project");
            }
            MapState? mapState;
            if ((mapState = GameStateManager.Current.ActiveState as MapState) != null)
            {
                mapState.Handler.ResetCamera(true, true);
                mapState.Handler.TeleportCameraToMainParty();
            }
            Hero.MainHero.SetBirthDay(CampaignTime.YearsFromNow(-30f));
            return false;
        }

        /// <summary>
        /// Idk what this is meant for. I guess the training field needs to be moved as well.
        /// </summary>
        /// <param name="isSavedCampaign"></param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(StoryModeSubModule), "RegisterSubModuleObjects")]
        public static bool StoryModeSubModule_CustomTrainingField_Patch(bool isSavedCampaign)
        {
            if (StoryModeManager.Current != null)
            {
                MBObjectManager.Instance.LoadOneXmlFromFile(ModuleHelper.GetModuleFullPath("Europe_LemmyProject") + "ModuleData/ELPData/training_field.xml", null, true);
            }
            return false;
        }
#endregion

#region Technical Patches
        /// <summary>
        /// Necessary to stop crash in SandBoxSubModule.InitializeGameStarter
        /// </summary>
        /// <remarks>
        /// I think the real cause of the crash is a bad snow flow map, but this works for now.
        /// </remarks>
        /// <param name="snowData"></param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Utilities), "GetSnowAmountData")]
        public static bool Utilities_GetSnowAmountData_Patch(byte[] snowData)
        {
            return false;
        }

        /// <summary>
        /// The magic of Aurelian's MapFix. 
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(MapScene), "Load")]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int truthOccurance = -1;
            bool truthFlag = false;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldstr && instruction.OperandIs("Main_map"))
                {
                    instruction.operand = SubModule.MainMapName;
                }
                else if (instruction.opcode == OpCodes.Ldloca_S)
                {
                    truthOccurance++;
                    truthFlag = true;
                }
                else if (instruction.opcode == OpCodes.Stfld)
                {
                    truthFlag = false;
                }
                else if (instruction.opcode == OpCodes.Ldc_I4_0 && truthFlag && (truthOccurance == 1 || truthOccurance == 3))
                {
                    instruction.opcode = OpCodes.Ldc_I4_1;
                }
                yield return instruction;
            }
        }

        /// <summary>
        /// Some more magic from Aurelian.
        /// </summary>
        /// <param name="reader"></param>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DefaultMapDistanceModel), "LoadCacheFromFile")]
        public static void DefaultMapDistanceModel_LoadCacheFromFile_Patch(ref System.IO.BinaryReader reader)
        {
            try
            {
                TaleWorlds.Library.Debug.Print("SettlementsDistanceCacheFilePath: " + SettlementsDistanceCacheFilePath, 0, TaleWorlds.Library.Debug.DebugColor.White, 17592186044416UL);
                System.IO.BinaryReader binaryReader = new System.IO.BinaryReader(File.Open(SettlementsDistanceCacheFilePath, FileMode.Open, FileAccess.Read));
                reader = binaryReader;
            }
            catch
            {
                TaleWorlds.Library.Debug.Print("SettlementsDistanceCacheFilePath could not be read!. Campaign performance will be affected very badly.", 0, TaleWorlds.Library.Debug.DebugColor.White, 17592186044416UL);
            }
        }

        /// <summary>
        /// Last bit of Aurelian's magic.
        /// </summary>
        /// <param name="reader"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(DefaultMapDistanceModel), "LoadCacheFromFile")]
        public static void DefaultMapDistanceModel_LoadCacheFromFile_Patch(System.IO.BinaryReader reader)
        {
            reader.Close();
        }
#endregion

#region Unused

        /// <summary>
        /// Forget what this is necessary for, but I think it's important, so let's keep it around for now.
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PartyNameplateVM), "RefreshDynamicProperties")]
        public static void RefreshDynamicPropertiesPatch(PartyNameplateVM __instance)
        {
            //__instance.RefreshPosition();
        }
#endregion
    }
}
