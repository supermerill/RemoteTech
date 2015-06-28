﻿using System;

namespace RemoteTech.Modules
{
    public sealed class MissionControlAntenna : IAntenna
    {
        [Persistent] public float Omni = 75000000000000f;
        public ISatellite Parent { get; set; }

        float IAntenna.Omni { get { return Omni; } }
        Guid IAntenna.Guid { get { return Parent.Guid; } }
        String IAntenna.Name { get { return "Dummy Antenna"; } }
        bool IAntenna.Powered { get { return true; } }
        bool IAntenna.Activated { get { return true; } set { return; } }
        float IAntenna.Consumption { get { return 0.0f; } }
        bool IAntenna.CanTarget { get { return false; } }
        Guid IAntenna.Target { get { return Guid.Empty; } set { return; } }
        float IAntenna.Dish { get { return 0.0f; } }
		double IAntenna.CosAngle { get { return 1.0; } }
		public float PacketSize { get { return 1000.0f; } }
		public float PacketInterval { get { return 0.1f; } }
		public float PacketResourceCost { get { return 0.0f; } }


        public void OnConnectionRefresh() { }

        public int CompareTo(IAntenna antenna)
        {
            return ((IAntenna)this).Consumption.CompareTo(antenna.Consumption);
        }
    }
}