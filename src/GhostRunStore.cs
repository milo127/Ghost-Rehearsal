using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace GhostRehearsal
{
    public sealed class GhostRunStore
    {
        private readonly string runDirectory;

        public GhostRunStore()
        {
            runDirectory = Path.Combine(KSPUtil.ApplicationRootPath, "GameData/GhostRehearsal/Runs");
            Directory.CreateDirectory(runDirectory);
        }

        public string GetPathForVessel(string vesselName)
        {
            return GetPathForVessel(vesselName, "default");
        }

        public string GetPathForVessel(string vesselName, string slotName)
        {
            string safeVessel = SanitizeFileName(vesselName);
            string safeSlot = SanitizeFileName(NormalizeSlotName(slotName));
            return Path.Combine(runDirectory, safeVessel + "__" + safeSlot + ".ghost");
        }

        public void Save(GhostRun run)
        {
            Save(run, run == null ? "default" : run.SlotName);
        }

        public void Save(GhostRun run, string slotName)
        {
            if (run == null || string.IsNullOrEmpty(run.VesselName))
            {
                return;
            }

            run.SlotName = NormalizeSlotName(slotName);
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("GhostRehearsal\t1");
            builder.AppendLine("VesselName\t" + Escape(run.VesselName));
            builder.AppendLine("SlotName\t" + Escape(run.SlotName));
            builder.AppendLine("BodyName\t" + Escape(run.BodyName));
            builder.AppendLine("StartedUtc\t" + Escape(run.StartedUtc));
            builder.AppendLine("StartUniversalTime\t" + Format(run.StartUniversalTime));
            builder.AppendLine("Samples\t" + run.Samples.Count);

            for (int i = 0; i < run.Samples.Count; i++)
            {
                GhostSample sample = run.Samples[i];
                builder.Append("S");
                builder.Append('\t').Append(Format(sample.MissionTime));
                builder.Append('\t').Append(Format(sample.UniversalTime));
                builder.Append('\t').Append(Escape(sample.BodyName));
                builder.Append('\t').Append(Format(sample.Latitude));
                builder.Append('\t').Append(Format(sample.Longitude));
                builder.Append('\t').Append(Format(sample.Altitude));
                builder.Append('\t').Append(Format(sample.RadarAltitude));
                builder.Append('\t').Append(Format(sample.SurfaceSpeed));
                builder.Append('\t').Append(Format(sample.VerticalSpeed));
                builder.Append('\t').Append(Format(sample.OrbitalSpeed));
                builder.Append('\t').Append(Format(sample.Apoapsis));
                builder.Append('\t').Append(Format(sample.Periapsis));
                builder.Append('\t').Append(Format(sample.LiquidFuel));
                builder.Append('\t').Append(Format(sample.Oxidizer));
                builder.Append('\t').Append(sample.Stage);
                builder.AppendLine();
            }

            builder.AppendLine("Events\t" + run.Events.Count);
            for (int i = 0; i < run.Events.Count; i++)
            {
                GhostEvent ghostEvent = run.Events[i];
                builder.Append("E");
                builder.Append('\t').Append(Format(ghostEvent.MissionTime));
                builder.Append('\t').Append(Escape(ghostEvent.Kind));
                builder.Append('\t').Append(Escape(ghostEvent.Label));
                builder.AppendLine();
            }

            File.WriteAllText(GetPathForVessel(run.VesselName, run.SlotName), builder.ToString(), Encoding.UTF8);
        }

        public GhostRun Load(string vesselName)
        {
            return Load(vesselName, "default");
        }

        public GhostRun Load(string vesselName, string slotName)
        {
            string normalizedSlot = NormalizeSlotName(slotName);
            string path = GetPathForVessel(vesselName, normalizedSlot);
            if (!File.Exists(path) && normalizedSlot == "default")
            {
                string legacyPath = Path.Combine(runDirectory, SanitizeFileName(vesselName) + ".ghost");
                if (File.Exists(legacyPath))
                {
                    path = legacyPath;
                }
            }

            if (!File.Exists(path))
            {
                return null;
            }

            GhostRun run = new GhostRun();
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split('\t');
                if (parts.Length == 0)
                {
                    continue;
                }

                if (parts[0] == "VesselName" && parts.Length > 1)
                {
                    run.VesselName = Unescape(parts[1]);
                }
                else if (parts[0] == "BodyName" && parts.Length > 1)
                {
                    run.BodyName = Unescape(parts[1]);
                }
                else if (parts[0] == "SlotName" && parts.Length > 1)
                {
                    run.SlotName = NormalizeSlotName(Unescape(parts[1]));
                }
                else if (parts[0] == "StartedUtc" && parts.Length > 1)
                {
                    run.StartedUtc = Unescape(parts[1]);
                }
                else if (parts[0] == "StartUniversalTime" && parts.Length > 1)
                {
                    run.StartUniversalTime = Parse(parts[1]);
                }
                else if (parts[0] == "S" && parts.Length >= 16)
                {
                    run.Samples.Add(new GhostSample
                    {
                        MissionTime = Parse(parts[1]),
                        UniversalTime = Parse(parts[2]),
                        BodyName = Unescape(parts[3]),
                        Latitude = Parse(parts[4]),
                        Longitude = Parse(parts[5]),
                        Altitude = Parse(parts[6]),
                        RadarAltitude = Parse(parts[7]),
                        SurfaceSpeed = Parse(parts[8]),
                        VerticalSpeed = Parse(parts[9]),
                        OrbitalSpeed = Parse(parts[10]),
                        Apoapsis = Parse(parts[11]),
                        Periapsis = Parse(parts[12]),
                        LiquidFuel = Parse(parts[13]),
                        Oxidizer = Parse(parts[14]),
                        Stage = (int)Parse(parts[15])
                    });
                }
                else if (parts[0] == "E" && parts.Length >= 4)
                {
                    run.Events.Add(new GhostEvent
                    {
                        MissionTime = Parse(parts[1]),
                        Kind = Unescape(parts[2]),
                        Label = Unescape(parts[3])
                    });
                }
            }

            if (string.IsNullOrEmpty(run.SlotName))
            {
                run.SlotName = normalizedSlot;
            }

            return run.HasSamples ? run : null;
        }

        public string[] ListSlots(string vesselName)
        {
            string safeVessel = SanitizeFileName(vesselName);
            string[] files = Directory.GetFiles(runDirectory, safeVessel + "__*.ghost");
            string[] slots = new string[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileNameWithoutExtension(files[i]);
                int separator = name.IndexOf("__", StringComparison.Ordinal);
                slots[i] = separator >= 0 ? name.Substring(separator + 2) : "default";
            }

            Array.Sort(slots, StringComparer.OrdinalIgnoreCase);
            return slots;
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "unnamed-vessel";
            }

            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value.Trim();
        }

        public static string NormalizeSlotName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "default";
            }

            value = value.Trim();
            return value.Length == 0 ? "default" : value;
        }

        private static string Format(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static double Parse(string value)
        {
            double parsed;
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : 0d;
        }

        private static string Escape(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string Unescape(string value)
        {
            return (value ?? "").Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t").Replace("\\\\", "\\");
        }
    }
}
