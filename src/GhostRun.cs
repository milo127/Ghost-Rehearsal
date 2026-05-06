using System;
using System.Collections.Generic;
using UnityEngine;

namespace GhostRehearsal
{
    [Serializable]
    public sealed class GhostRun
    {
        public string ModVersion = "0.1.0";
        public string VesselName = "";
        public string SlotName = "default";
        public string BodyName = "";
        public string StartedUtc = "";
        public double StartUniversalTime;
        public List<GhostSample> Samples = new List<GhostSample>();
        public List<GhostEvent> Events = new List<GhostEvent>();

        public bool HasSamples
        {
            get { return Samples.Count > 0; }
        }
    }

    [Serializable]
    public sealed class GhostSample
    {
        public double MissionTime;
        public double UniversalTime;
        public string BodyName;
        public double Latitude;
        public double Longitude;
        public double Altitude;
        public double RadarAltitude;
        public double SurfaceSpeed;
        public double VerticalSpeed;
        public double OrbitalSpeed;
        public double Apoapsis;
        public double Periapsis;
        public double LiquidFuel;
        public double Oxidizer;
        public int Stage;

        public Vector3d ToWorldPosition(CelestialBody body)
        {
            return body.GetWorldSurfacePosition(Latitude, Longitude, Altitude);
        }
    }

    [Serializable]
    public sealed class GhostEvent
    {
        public double MissionTime;
        public string Kind;
        public string Label;
    }
}
