using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions.GameKeys;
using Module = TaleWorlds.MountAndBlade.Module;
using TaleWorlds.Localization;

using HarmonyLib;

namespace Europe_LemmyProject.General
{
	/// <summary>
	/// Registers new keys for opening ship management and ghosting settlement physics.
	/// </summary>
    public class ELPHotKeys : GameKeyContext
    {
		// Token: 0x04000A38 RID: 2616
		public const string CategoryId = "MapHotKeyCategory";

		// Token: 0x04000A39 RID: 2617
		public const int QuickSave = 53;

		// Token: 0x04000A3A RID: 2618
		public const int PartyMoveUp = 49;

		// Token: 0x04000A3B RID: 2619
		public const int PartyMoveLeft = 52;

		// Token: 0x04000A3C RID: 2620
		public const int PartyMoveDown = 50;

		// Token: 0x04000A3D RID: 2621
		public const int PartyMoveRight = 51;

		// Token: 0x04000A3E RID: 2622
		public const int MapMoveUp = 45;

		// Token: 0x04000A3F RID: 2623
		public const int MapMoveDown = 46;

		// Token: 0x04000A40 RID: 2624
		public const int MapMoveLeft = 48;

		// Token: 0x04000A41 RID: 2625
		public const int MapMoveRight = 47;

		// Token: 0x04000A42 RID: 2626
		public const string MovementAxisX = "MovementAxisX";

		// Token: 0x04000A43 RID: 2627
		public const string MovementAxisY = "MovementAxisY";

		// Token: 0x04000A44 RID: 2628
		public const int MapFastMove = 54;

		// Token: 0x04000A45 RID: 2629
		public const int MapZoomIn = 55;

		// Token: 0x04000A46 RID: 2630
		public const int MapZoomOut = 56;

		// Token: 0x04000A47 RID: 2631
		public const int MapRotateLeft = 57;

		// Token: 0x04000A48 RID: 2632
		public const int MapRotateRight = 58;

		// Token: 0x04000A49 RID: 2633
		public const int MapCameraFollowMode = 63;

		// Token: 0x04000A4A RID: 2634
		public const int MapToggleFastForward = 64;

		// Token: 0x04000A4B RID: 2635
		public const int MapTrackSettlement = 65;

		// Token: 0x04000A4C RID: 2636
		public const int MapGoToEncylopedia = 66;

		// Token: 0x04000A4D RID: 2637
		public const string MapClick = "MapClick";

		// Token: 0x04000A4E RID: 2638
		public const string MapFollowModifier = "MapFollowModifier";

		// Token: 0x04000A4F RID: 2639
		public const string MapChangeCursorMode = "MapChangeCursorMode";

		// Token: 0x04000A50 RID: 2640
		public const int MapTimeStop = 59;

		// Token: 0x04000A51 RID: 2641
		public const int MapTimeNormal = 60;

		// Token: 0x04000A52 RID: 2642
		public const int MapTimeFastForward = 61;

		// Token: 0x04000A53 RID: 2643
		public const int MapTimeTogglePause = 62;

		// Token: 0x04000A54 RID: 2644
		public const int MapShowPartyNames = 5;
		public const int MapOpenShipManagement = 68;
		public const int MapGhostSettlementPhysics = 67;


		public ELPHotKeys() : base("MapHotKeyCategory", 108, GameKeyContext.GameKeyContextType.Default)
        {
            this.RegisterHotKeys();
            this.RegisterGameKeys();
            this.RegisterGameAxisKeys();
        }
		private void RegisterHotKeys()
		{
			List<Key> keys = new List<Key>
			{
				new Key(InputKey.LeftMouseButton),
				new Key(InputKey.ControllerRDown)
			};
			base.RegisterHotKey(new HotKey("MapClick", "MapHotKeyCategory", keys, HotKey.Modifiers.None, HotKey.Modifiers.None), true);
			List<Key> keys2 = new List<Key>
			{
				new Key(InputKey.LeftAlt),
				new Key(InputKey.ControllerLBumper)
			};
			base.RegisterHotKey(new HotKey("MapFollowModifier", "MapHotKeyCategory", keys2, HotKey.Modifiers.None, HotKey.Modifiers.None), true);
			List<Key> keys3 = new List<Key>
			{
				new Key(InputKey.ControllerRRight)
			};
			base.RegisterHotKey(new HotKey("MapChangeCursorMode", "MapHotKeyCategory", keys3, HotKey.Modifiers.None, HotKey.Modifiers.None), true);
		}

		// Token: 0x06001CB5 RID: 7349 RVA: 0x0006650C File Offset: 0x0006470C
		private void RegisterGameKeys()
		{
			base.RegisterGameKey(new GameKey(49, "PartyMoveUp", "MapHotKeyCategory", InputKey.Up, GameKeyMainCategories.CampaignMapCategory), true);
			base.RegisterGameKey(new GameKey(50, "PartyMoveDown", "MapHotKeyCategory", InputKey.Down, GameKeyMainCategories.CampaignMapCategory), true);
			base.RegisterGameKey(new GameKey(51, "PartyMoveRight", "MapHotKeyCategory", InputKey.Right, GameKeyMainCategories.CampaignMapCategory), true);
			base.RegisterGameKey(new GameKey(52, "PartyMoveLeft", "MapHotKeyCategory", InputKey.Left, GameKeyMainCategories.CampaignMapCategory), true);
			base.RegisterGameKey(new GameKey(53, "QuickSave", "MapHotKeyCategory", InputKey.F5, GameKeyMainCategories.CampaignMapCategory), true);
			base.RegisterGameKey(new GameKey(54, "MapFastMove", "MapHotKeyCategory", InputKey.LeftShift, GameKeyMainCategories.CampaignMapCategory), true);
			base.RegisterGameKey(new GameKey(55, "MapZoomIn", "MapHotKeyCategory", InputKey.MouseScrollUp, InputKey.ControllerRTrigger, GameKeyMainCategories.CampaignMapCategory), true);
			base.RegisterGameKey(new GameKey(56, "MapZoomOut", "MapHotKeyCategory", InputKey.MouseScrollDown, InputKey.ControllerLTrigger, GameKeyMainCategories.CampaignMapCategory), true);
			base.RegisterGameKey(new GameKey(57, "MapRotateLeft", "MapHotKeyCategory", InputKey.Q, InputKey.Invalid, GameKeyMainCategories.CampaignMapCategory), true);
			base.RegisterGameKey(new GameKey(58, "MapRotateRight", "MapHotKeyCategory", InputKey.E, InputKey.Invalid, GameKeyMainCategories.CampaignMapCategory), true);
			base.RegisterGameKey(new GameKey(59, "MapTimeStop", "MapHotKeyCategory", InputKey.D1, GameKeyMainCategories.CampaignMapCategory), true);
			base.RegisterGameKey(new GameKey(60, "MapTimeNormal", "MapHotKeyCategory", InputKey.D2, GameKeyMainCategories.CampaignMapCategory), true);
			base.RegisterGameKey(new GameKey(61, "MapTimeFastForward", "MapHotKeyCategory", InputKey.D3, GameKeyMainCategories.CampaignMapCategory), true);
			base.RegisterGameKey(new GameKey(62, "MapTimeTogglePause", "MapHotKeyCategory", InputKey.Space, InputKey.ControllerRLeft, GameKeyMainCategories.CampaignMapCategory), true);
			base.RegisterGameKey(new GameKey(63, "MapCameraFollowMode", "MapHotKeyCategory", InputKey.Invalid, InputKey.ControllerLThumb, GameKeyMainCategories.CampaignMapCategory), true);
			base.RegisterGameKey(new GameKey(64, "MapToggleFastForward", "MapHotKeyCategory", InputKey.Invalid, InputKey.ControllerRBumper, GameKeyMainCategories.CampaignMapCategory), true);
			base.RegisterGameKey(new GameKey(65, "MapTrackSettlement", "MapHotKeyCategory", InputKey.Invalid, InputKey.ControllerRThumb, GameKeyMainCategories.CampaignMapCategory), true);
			base.RegisterGameKey(new GameKey(66, "MapGoToEncylopedia", "MapHotKeyCategory", InputKey.Invalid, InputKey.ControllerLOption, GameKeyMainCategories.CampaignMapCategory), true);
			GameKey gameKey = GenericGameKeyContext.Current.RegisteredGameKeys.First((GameKey g) => g.Id.Equals(5));
			base.RegisterGameKey(gameKey, true);

			#region New Keys
			foreach(GameKey key in SubModule.MapGameKeys)
            {
				base.RegisterGameKey(key, true);
            }
			#endregion
		}

		// Token: 0x06001CB6 RID: 7350 RVA: 0x000667B0 File Offset: 0x000649B0
		private void RegisterGameAxisKeys()
		{
			GameAxisKey gameKey = GenericGameKeyContext.Current.RegisteredGameAxisKeys.First((GameAxisKey g) => g.Id.Equals("CameraAxisX"));
			GameAxisKey gameKey2 = GenericGameKeyContext.Current.RegisteredGameAxisKeys.First((GameAxisKey g) => g.Id.Equals("CameraAxisY"));
			base.RegisterGameAxisKey(gameKey, true);
			base.RegisterGameAxisKey(gameKey2, true);
			GameKey gameKey3 = new GameKey(45, "MapMoveUp", "MapHotKeyCategory", InputKey.W, GameKeyMainCategories.CampaignMapCategory);
			GameKey gameKey4 = new GameKey(46, "MapMoveDown", "MapHotKeyCategory", InputKey.S, GameKeyMainCategories.CampaignMapCategory);
			GameKey gameKey5 = new GameKey(47, "MapMoveRight", "MapHotKeyCategory", InputKey.D, GameKeyMainCategories.CampaignMapCategory);
			GameKey gameKey6 = new GameKey(48, "MapMoveLeft", "MapHotKeyCategory", InputKey.A, GameKeyMainCategories.CampaignMapCategory);
			base.RegisterGameKey(gameKey3, true);
			base.RegisterGameKey(gameKey4, true);
			base.RegisterGameKey(gameKey6, true);
			base.RegisterGameKey(gameKey5, true);
			base.RegisterGameAxisKey(new GameAxisKey("MovementAxisX", InputKey.ControllerLStick, gameKey5, gameKey6, GameAxisKey.AxisType.X), true);
			base.RegisterGameAxisKey(new GameAxisKey("MovementAxisY", InputKey.ControllerLStick, gameKey3, gameKey4, GameAxisKey.AxisType.Y), true);
		}
	}

	[HarmonyPatch]
	public static class HotKeyPatch
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="__instance"></param>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameKeyOptionVM), "RefreshValues")]
		public static void KeyOptionVM_ConstructorPatch(GameKeyOptionVM __instance)
        {
			TextObject temp = new TextObject();
			//string name = "str_key_name", gameKey.groupId + "_" + this._id;
			bool doesNameExist = Module.CurrentModule.GlobalTextManager.TryGetText(
				"str_key_name", 
				__instance.CurrentGameKey.GroupId + "_" + ((GameKeyDefinition)__instance.CurrentGameKey.Id).ToString(), 
				out temp);
            if (!doesNameExist)
            {
				string test = HotKeyManager.GetHotKeyId(ELPHotKeys.CategoryId, __instance.CurrentGameKey.Id);
				string name = __instance.CurrentGameKey.StringId;
				var prop = __instance.GetType().GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance);
				prop.SetValue(__instance, name);
			}
		}
	}
}
