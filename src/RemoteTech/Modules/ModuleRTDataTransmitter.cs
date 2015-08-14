using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RemoteTech;
using RemoteTech.SimpleTypes;

namespace RemoteTech.Modules
{
    public sealed class ModuleRTDataTransmitter : PartModule, IScienceDataTransmitter
    {
        [KSPField]
        public float
            PacketInterval = 0.5f,
            PacketSize = 1.0f,
            PacketResourceCost = 10f;

		[KSPField]
		public float signalWeaken;

        [KSPField]
        public String
            RequiredResource = "ElectricCharge";
        [KSPField(guiName = "Comms", guiActive = true)]
        public String GUIStatus = "";


		[KSPField(guiActive = true, guiName = "bandwidth")]
		public float guiBandwidth = 0;
		public int refreshBandwidthCounter = 0;

		[KSPField(guiActive = true, guiName = "Energy/Mib")]
		public float guiEnergy = 0;

        private bool isBusy;
        private readonly List<ScienceData> scienceDataQueue = new List<ScienceData>();

        // Compatible with ModuleDataTransmitter
        public override void OnLoad(ConfigNode node)
        {
            foreach (ConfigNode data in node.GetNodes("CommsData"))
            {
                scienceDataQueue.Add(new ScienceData(data));
            }

            var antennas = part.FindModulesImplementing<ModuleRTAntenna>();
            GUIStatus = "Idle";
        }

        // Compatible with ModuleDataTransmitter
        public override void OnSave(ConfigNode node)
        {
            scienceDataQueue.ForEach(d => d.Save(node.AddNode("CommsData")));
        }
       
        bool IScienceDataTransmitter.CanTransmit()
        {
            return true;
        }

        void IScienceDataTransmitter.TransmitData(List<ScienceData> dataQueue, Callback callback)
        {
            scienceDataQueue.AddRange(dataQueue);
            if (!isBusy)
            {
                StartCoroutine(Transmit(callback));
            }
        }

        float IScienceDataTransmitter.DataRate { get { return PacketSize / PacketInterval; } }
        double IScienceDataTransmitter.DataResourceCost { get { return PacketResourceCost / PacketSize; } }
        bool IScienceDataTransmitter.IsBusy() { return isBusy; }

        void IScienceDataTransmitter.TransmitData(List<ScienceData> dataQueue)
        {
            scienceDataQueue.AddRange(dataQueue);
            if (!isBusy)
            {
                StartCoroutine(Transmit());
            }
        }

		public override void OnUpdate()
		{
			base.OnUpdate();

			//check for refresh the gui (not at each update, as it's not trivial)
			//TODO: cache the path for ~1000 updates.
			if (refreshBandwidthCounter++ > 120)
			{
				refreshBandwidthCounter = new System.Random().Next() % 50; // to not be in sync with anything
				double maxBandwidth = Double.MaxValue;
				foreach (NetworkRoute<ISatellite> segment in RTCore.Instance.Network[RTCore.Instance.Network[NetworkManager.ActiveVesselGuid]])
				{
					//check the max bandwidth against the distance
					//we use the best antenna in each satelite 
					//(it's like the sat turn to receive with his best antenna then rotate to emit with its best antenna)
					//TODO: use target /can target/isOmni mode
					double checkBandwidthStart = 0;
					foreach (IAntenna checkAntenna in segment.Start.Antennas)
					{
						if (checkAntenna.Activated && checkAntenna.Powered)
						{
							//need to get the TRANSMITTER node of the "ModuleRTAntenna" module (or the passive one)
							Debug.Log("[RTMerill] AntennaSource : " + checkAntenna.Name + ", type : " + checkAntenna.GetType());

							double checkBandwidth = checkAntenna.PacketSize / checkAntenna.PacketInterval;
							Debug.Log("[RTMerill] base bandwidth: "
								+ checkAntenna.PacketSize + " / " + checkAntenna.PacketInterval
								+ " = " + checkBandwidth);
							//remove distance
							//checkBandwidth = Math.Min(checkBandwidth,
							//	checkBandwidth / Math.Log10(
							//		Math.Max(10, segment.Length -
							//			Math.Max(checkAntenna.Omni, checkAntenna.Dish))));
							//Debug.Log("[RTMerill] reduced bandwidth compute: "
							//	 + segment.Length + " - Math.Max " + checkAntenna.Omni + ", " + checkAntenna.Dish);
							checkBandwidth = Math.Min(checkBandwidth,
								checkBandwidth *
									Math.Pow(Math.Max(checkAntenna.Omni, checkAntenna.Dish) / segment.Length, 2));
							Debug.Log("[RTMerill] reduced bandwidth compute: pow="
								 + Math.Pow(Math.Max(checkAntenna.Omni, checkAntenna.Dish) / segment.Length, 2));
							Debug.Log("[RTMerill] reduced bandwidth: "
								 + segment.Length + " ~ " + Math.Max(checkAntenna.Omni, checkAntenna.Dish)
								 + " => " + checkBandwidth);
							checkBandwidthStart = Math.Max(checkBandwidthStart, checkBandwidth);
						}
					}
					double checkBandwidthGoal = 0;
					foreach (IAntenna checkAntenna in segment.Goal.Antennas)
					{
						if (checkAntenna.Activated && checkAntenna.Powered)
						{
							//need to get the TRANSMITTER node of the "ModuleRTAntenna" module (or the passive one)
							Debug.Log("[RTMerill] AntennaGoal : " + checkAntenna.Name + ", type : " + checkAntenna.GetType());

							double checkBandwidth = checkAntenna.PacketSize / checkAntenna.PacketInterval;
							Debug.Log("[RTMerill] base bandwidth: "
								+ checkAntenna.PacketSize + " / " + checkAntenna.PacketInterval
								+ " = " + checkBandwidth);
							//remove distance
							//checkBandwidth = Math.Min(checkBandwidth,
							//	checkBandwidth / Math.Log10(
							//		Math.Max(10, segment.Length -
							//			Math.Max(checkAntenna.Omni, checkAntenna.Dish))));
							checkBandwidth = Math.Min(checkBandwidth,
								checkBandwidth *
									Math.Pow(Math.Max(checkAntenna.Omni, checkAntenna.Dish) / segment.Length, 2));
							Debug.Log("[RTMerill] reduced bandwidth compute: pow="
								 + Math.Pow(Math.Max(checkAntenna.Omni, checkAntenna.Dish) / segment.Length, 2));
							Debug.Log("[RTMerill] reduced bandwidth: "
								 + (segment.Length - Math.Max(checkAntenna.Omni, checkAntenna.Dish))
								 + " => " + checkBandwidth);
							checkBandwidthGoal = Math.Max(checkBandwidthGoal, checkBandwidth);
						}
					}
					maxBandwidth = Math.Min(maxBandwidth, Math.Min(checkBandwidthStart, checkBandwidthGoal));
					Debug.Log("[RTMerill] new max bandwidth : " + maxBandwidth);
				}

				guiBandwidth = (float)maxBandwidth;
				float currentPacketSize = (float)(PacketSize * maxBandwidth / (PacketSize / PacketInterval));
				Debug.Log("[RTMerill] reduce packet size : " + PacketSize + "=>" + currentPacketSize);
				//int packets = Mathf.CeilToInt(PacketResourceCost / currentPacketSize);
				//Debug.Log("[RTMerill] need nbpackets =  " + packets);
				guiEnergy = PacketResourceCost / currentPacketSize;
			}
		}

        private IEnumerator Transmit(Callback callback = null)
        {
            var msg = new ScreenMessage(String.Format("[{0}]: Starting Transmission...", part.partInfo.title), 4f, ScreenMessageStyle.UPPER_LEFT);
            var msgStatus = new ScreenMessage(String.Empty, 4.0f, ScreenMessageStyle.UPPER_LEFT);
			ScreenMessages.PostScreenMessage(msg);
			Debug.Log("[RTMerill] Transmit: " + scienceDataQueue.Any());

            isBusy = true;

            while (scienceDataQueue.Any())
			{
				Debug.Log("[RTMerill] Transmitany");
                RnDCommsStream commStream = null;
                var scienceData = scienceDataQueue[0];
                var dataAmount = scienceData.dataAmount;
                scienceDataQueue.RemoveAt(0);
                var subject = ResearchAndDevelopment.GetSubjectByID(scienceData.subjectID);
				int packets = Mathf.CeilToInt(scienceData.dataAmount / PacketSize);
				Debug.Log("[RTMerill] Transmit amount: " + dataAmount);

				//TODO: move this to network manager?
				//create the best liaison.
				//IAntenna antenna = null;
				//List<ModuleRTAntenna> antennasFromWichToChoose = part.FindModulesImplementing<ModuleRTAntenna>();
				//if (antennasFromWichToChoose.Count == 0)
				//{
				//	List<ModuleRTAntennaPassive> antennasFromWichToChoose2 = part.FindModulesImplementing<ModuleRTAntennaPassive>();
				//	if (antennasFromWichToChoose2.Count != 0)
				//	{
				//		antenna = antennasFromWichToChoose2[0];
				//	}
				//}
				//else antenna = antennasFromWichToChoose[0];
				//Debug.Log("[RTMerill] RTCore.Instance " + (RTCore.Instance != null));
				//Debug.Log("[RTMerill] RTCore.Instance.Network " + (RTCore.Instance.Network != null));
				//Debug.Log("[RTMerill] RTCore.Instance.Network.count " + (RTCore.Instance.Network.Count));
				//ISatellite satellite = RTCore.Instance.Network[antenna.Guid];
				ISatellite activeSatellite = RTCore.Instance.Network[NetworkManager.ActiveVesselGuid];
				Debug.Log("[RTMerill] activeSatellite " + (activeSatellite != null));
				//bool route_home = RTCore.Instance.Network[activeSatellite]
				//	.Any(r => r.Links[0].Interfaces.Contains(antenna)
				//				&& RTCore.Instance.Network.GroundStations.ContainsKey(r.Goal.Guid));

				//get all segments
				Debug.Log("[RTMerill] Check bandwidth");
				List<NetworkRoute<ISatellite>> listRoute = RTCore.Instance.Network[activeSatellite];
				double maxBandwidth = Double.MaxValue;
				foreach (NetworkRoute<ISatellite> segment in listRoute)
				{
					//check the max bandwidth against the distance
					//we use the best antenna in each satelite 
					//(it's like the sat turn to receive with his best antenna then rotate to emit with its best antenna)
					//TODO: use target /can target/isOmni mode
					double checkBandwidthStart = 0;
					foreach (IAntenna checkAntenna in segment.Start.Antennas)
					{
						if (checkAntenna.Activated && checkAntenna.Powered)
						{
							//need to get the TRANSMITTER node of the "ModuleRTAntenna" module (or the passive one)
							Debug.Log("[RTMerill] AntennaSource : " + checkAntenna.Name + ", type : " + checkAntenna.GetType());
							
							double checkBandwidth = checkAntenna.PacketSize / checkAntenna.PacketInterval;
							Debug.Log("[RTMerill] base bandwidth: "
								+ checkAntenna.PacketSize + " / " + checkAntenna.PacketInterval
								+ " = " + checkBandwidth);
							//remove distance
							//checkBandwidth = Math.Min(checkBandwidth,
							//	checkBandwidth / Math.Log10(
							//		Math.Max(10, segment.Length -
							//			Math.Max(checkAntenna.Omni, checkAntenna.Dish))));
							//Debug.Log("[RTMerill] reduced bandwidth compute: "
							//	 + segment.Length + " - Math.Max " + checkAntenna.Omni + ", " + checkAntenna.Dish);
							checkBandwidth = Math.Min(checkBandwidth,
								checkBandwidth * 
									Math.Pow( Math.Max(checkAntenna.Omni, checkAntenna.Dish) / segment.Length, 2 ) );
							Debug.Log("[RTMerill] reduced bandwidth compute: pow="
								 + Math.Pow(Math.Max(checkAntenna.Omni, checkAntenna.Dish) / segment.Length, 2));
							Debug.Log("[RTMerill] reduced bandwidth: "
								 + segment.Length +" ~ "+ Math.Max(checkAntenna.Omni, checkAntenna.Dish)
								 + " => " + checkBandwidth);
							checkBandwidthStart = Math.Max(checkBandwidthStart, checkBandwidth);
						}
					}
					double checkBandwidthGoal = 0;
					foreach (IAntenna checkAntenna in segment.Goal.Antennas)
					{
						if (checkAntenna.Activated && checkAntenna.Powered)
						{
							//need to get the TRANSMITTER node of the "ModuleRTAntenna" module (or the passive one)
							Debug.Log("[RTMerill] AntennaGoal : " + checkAntenna.Name + ", type : " + checkAntenna.GetType());

							double checkBandwidth = checkAntenna.PacketSize / checkAntenna.PacketInterval;
							Debug.Log("[RTMerill] base bandwidth: "
								+ checkAntenna.PacketSize + " / " + checkAntenna.PacketInterval
								+ " = " + checkBandwidth);
							//remove distance
							//checkBandwidth = Math.Min(checkBandwidth,
							//	checkBandwidth / Math.Log10(
							//		Math.Max(10, segment.Length -
							//			Math.Max(checkAntenna.Omni, checkAntenna.Dish))));
							checkBandwidth = Math.Min(checkBandwidth,
								checkBandwidth *
									Math.Pow(Math.Max(checkAntenna.Omni, checkAntenna.Dish) / segment.Length, 2));
							Debug.Log("[RTMerill] reduced bandwidth compute: pow="
								 + Math.Pow(Math.Max(checkAntenna.Omni, checkAntenna.Dish) / segment.Length, 2));
							Debug.Log("[RTMerill] reduced bandwidth: "
								 + (segment.Length - Math.Max(checkAntenna.Omni, checkAntenna.Dish))
								 + " => " + checkBandwidth);
                            checkBandwidthGoal = Math.Max(checkBandwidthGoal, checkBandwidth);
						}
					}
					maxBandwidth = Math.Min(maxBandwidth, Math.Min(checkBandwidthStart, checkBandwidthGoal));
					Debug.Log("[RTMerill] new max bandwidth : " + maxBandwidth);
				}

				guiBandwidth = (float) maxBandwidth;
				//reduce our bandwith
				// newPS = oldPS*newBandwith/oldBandwith == newBandwith/oldPacketInterval
				float currentPacketSize = (float)(PacketSize * maxBandwidth / (PacketSize / PacketInterval));
				Debug.Log("[RTMerill] reduce packet size : " + PacketSize + "=>" + currentPacketSize);
				packets = Mathf.CeilToInt(scienceData.dataAmount / currentPacketSize);
				Debug.Log("[RTMerill] need nbpackets =  " + packets);

                if (ResearchAndDevelopment.Instance != null)
                {
                    // pre calculate the time interval - fix for x64 systems
                    // workaround for issue #136
                    float time1 = Time.time;
                    yield return new WaitForSeconds(PacketInterval);
                    // get the delta time
                    float x64PacketInterval = (Time.time - time1);

                    RTLog.Notify("Changing RnDCommsStream timeout from {0} to {1}", PacketInterval, x64PacketInterval);

                    commStream = new RnDCommsStream(subject, scienceData.dataAmount, x64PacketInterval,
                                            scienceData.transmitValue, ResearchAndDevelopment.Instance);
                }

                //StartCoroutine(SetFXModules_Coroutine(modules_progress, 0.0f));
                float power = 0;
                while (packets > 0)
                {
                    power += part.RequestResource("ElectricCharge", PacketResourceCost - power);
                    if (power >= PacketResourceCost * 0.95)
                    {
                        float frame = Math.Min(currentPacketSize, dataAmount);
                        power -= PacketResourceCost;
                        GUIStatus = "Uploading Data...";
                        dataAmount -= frame;
                        packets--;
                        float progress = (scienceData.dataAmount - dataAmount) / scienceData.dataAmount;
                        //StartCoroutine(SetFXModules_Coroutine(modules_progress, progress));
                        msgStatus.message = String.Format("[{0}]: Uploading Data... {1}", part.partInfo.title, progress.ToString("P0"));
                        RTLog.Notify("[Transmitter]: Uploading Data... ({0}) - {1} Mits/sec. Packets to go: {2} - Files to Go: {3}",
                            scienceData.title, (currentPacketSize / PacketInterval).ToString("0.00"), packets, scienceDataQueue.Count);
                        ScreenMessages.PostScreenMessage(msgStatus, true);

                        // if we've a defined callback parameter so skip to stream each packet
                        if (commStream != null && callback == null)
                        {
                            commStream.StreamData(frame, vessel.protoVessel);
                        }
                    }
                    else
                    {
                        msg.message = String.Format("<b><color=orange>[{0}]: Warning! Not Enough {1}!</color></b>", part.partInfo.title, RequiredResource);
                        ScreenMessages.PostScreenMessage(msg, true);
                        GUIStatus = String.Format("{0}/{1} {2}", power, PacketResourceCost, RequiredResource);

                    }
                    yield return new WaitForSeconds(PacketInterval);
                }
                yield return new WaitForSeconds(PacketInterval * 2);
            }
            isBusy = false;
            msg.message = String.Format("[{0}]: Done!", part.partInfo.title);
            ScreenMessages.PostScreenMessage(msg, true);
            if (callback != null) callback.Invoke();
            GUIStatus = "Idle";
        }
    }
}
