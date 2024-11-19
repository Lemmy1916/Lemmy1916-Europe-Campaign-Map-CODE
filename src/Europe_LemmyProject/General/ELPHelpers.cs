using HarmonyLib;
using SandBox.GauntletUI.Map;
using SandBox.View.Map;
using SandBox.ViewModelCollection.Nameplate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Europe_LemmyProject.General
{
    public static class ELPHelpers
    {
        public static void AddNameplate(Settlement settlement, int size = 1)
        {
            GauntletMapSettlementNameplateView gmsn = MapScreen.Instance.GetMapView<GauntletMapSettlementNameplateView>();
            SettlementNameplatesVM nameplates = Traverse.Create(gmsn).Field("_dataSource").GetValue() as SettlementNameplatesVM;
            SettlementNameplateVM nameplate = new SettlementNameplateVM(settlement, ((PartyVisual)settlement.Party.Visuals).StrategicEntity,
                MapScreen.Instance.MapCamera, new Action<Vec2>(MapScreen.Instance.FastMoveCameraToPosition));
            nameplate.SettlementType = size;
            nameplates.Nameplates.Add(nameplate);
            nameplate.RefreshValues();
            nameplate.RefreshRelationStatus();
        }
    }
}
