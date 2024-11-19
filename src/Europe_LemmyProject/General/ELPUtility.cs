using System;
using System.Collections.Generic;
using System.Linq;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

using Europe_LemmyProject.MaritimeSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapNotificationTypes;
using TaleWorlds.Library;
using SandBox.View.Map;

namespace Europe_LemmyProject.General
{
    /// <summary>
    /// 
    /// </summary>
    public class ELPGeneralTypeDefiner : SaveableTypeDefiner
    {
        public ELPGeneralTypeDefiner() : base(491465)
        { }

        protected override void DefineClassTypes()
        {
            base.AddClassDefinition(typeof(TimedFleetDelivery), 1);
            base.AddClassDefinition(typeof(FleetDeliveredMapNotification), 3);
        }

        protected override void DefineInterfaceTypes()
        {
            base.AddInterfaceDefinition(typeof(GenericCampaignEvent), 2);
        }

        protected override void DefineContainerDefinitions()
        {
            base.ConstructContainerDefinition(typeof(List<GenericCampaignEvent>));
        }
    }

    /// <summary>
    /// Represents an event that should fire at a given CampaignTime.
    /// </summary>
    public interface GenericCampaignEvent
    {
        public abstract bool DoFire();

        public abstract void Fire();
    }


    /// <summary>
    /// Manager class for TimeCampaignActions.
    /// </summary>
    public class TimedCampaignActionManager : CampaignBehaviorBase
    {
        [SaveableField(0)]
        List<GenericCampaignEvent> _actions;
        public List<GenericCampaignEvent> Actions { get { return _actions; } }

        public override void RegisterEvents()
        {
            CampaignEvents.TickEvent.AddNonSerializedListener(this, new Action<float>(this.OnTick));
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData<List<GenericCampaignEvent>>("_actions", ref this._actions);
        }

        public static TimedCampaignActionManager Instance { get; private set; }

        public TimedCampaignActionManager()
        {
            _actions = new List<GenericCampaignEvent>();
            Instance = this;
        }

        public void AddAction(GenericCampaignEvent timedCampaignAction)
        {
            _actions.Add(timedCampaignAction);
        }

        private void OnTick(float dt)
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                GenericCampaignEvent timedCampaignAction = _actions[i];
                if (timedCampaignAction.DoFire())
                {
                    timedCampaignAction.Fire();
                    _actions.Remove(timedCampaignAction);
                }
            }
        }
    }

    public class TimedFleetDelivery : GenericCampaignEvent
    {
        [SaveableField(0)]
        private Settlement _start;
        public Settlement Start { get { return _start; } }
        [SaveableField(1)]
        private Settlement _end;
        public Settlement End { get { return _end; } }
        [SaveableField(2)]
        private Fleet _fleet;
        public Fleet Fleet { get { return _fleet; } }
        [SaveableProperty(0)]
        public CampaignTime ActionTime { get; private set; }

        public TimedFleetDelivery(Settlement start, Settlement end, Fleet fleet, CampaignTime campaignTime)
        {
            this._start = start;
            this._end = end;
            this._fleet = fleet;
            ActionTime = campaignTime;
        }

        public bool DoFire()
        {
            return ActionTime.IsPast;
        }

        public void Fire()
        {
            OnTransportFleetArrived();
        }

        public void OnTransportFleetArrived()
        {
            Fleet storedFleet = MobilePartyWrapper.MainPartyWrapper.StoredFleets.FirstOrDefault(x => x.Settlement == _end);
            if (storedFleet == null)
                _fleet.TransferToSettlement(_end);
            else
            {
                storedFleet.AddShips(_fleet.Ships);
                MobilePartyWrapper.MainPartyWrapper.DestroyStoredFleet(_fleet);
            }

            TextObject textObject = new TextObject("{=9yBJW5N3VL}Your fleet {FLEET_NAME} has arrive at {DESTINATION_NAME}");
            textObject.SetTextVariable("DESTINATION_NAME", _end.Name);
            textObject.SetTextVariable("FLEET_NAME", _fleet.Name);
            MBInformationManager.AddNotice(new FleetDeliveredMapNotification(_end, textObject));
        }
    }

    public class FleetDeliveredMapNotification : InformationData
    {
        [SaveableProperty(0)]
        public Settlement Destination { get; private set; } 
        public FleetDeliveredMapNotification(Settlement destination, TextObject description) : base(description)
        {
            this.Destination = destination;
        }

        public override TextObject TitleText
        {
            get
            {
                return new TextObject("{=svOVj9sGAG}Fleet arrived");
            }
        }

        public override string SoundEventPath
        {
            get
            {
                return "event:/ui/notification/quest_update";
            }
        }
    }

    public class FleetDeliveredMapNotificationVM : MapNotificationItemBaseVM
    {
        public Settlement Destination { get; private set; }
        public FleetDeliveredMapNotificationVM(FleetDeliveredMapNotification data) : base(data)
        {
            Destination = data.Destination;
            base.NotificationIdentifier = "settlementownerchanged";
            this._onInspect = delegate ()
            {
                Settlement settlement = this.Destination;
                Vec2? vec;
                if (settlement == null)
                {
                    vec = null;
                }
                else
                {
                    TaleWorlds.CampaignSystem.Party.PartyBase leaderParty = settlement.Party;
                    vec = (leaderParty != null) ? new Vec2?(leaderParty.Position2D) : null;
                }
                MapScreen.Instance.FastMoveCameraToPosition(vec ?? settlement.Position2D);
                base.ExecuteRemove();
            };
        }

    }
}
