using System;
using System.Collections.Generic;
using UnityEngine;

namespace GhostRehearsal
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class GhostRehearsalController : MonoBehaviour
    {
        private const double SampleInterval = 1.0;
        private static readonly Color TrailColor = new Color(1f, 0.84f, 0.1f, 0.82f);
        private static readonly Color EventStageColor = new Color(1f, 0.55f, 0.1f, 0.95f);
        private static readonly Color EventLandingColor = new Color(0.2f, 1f, 0.35f, 0.95f);
        private static readonly Color EventCrashColor = new Color(1f, 0.15f, 0.1f, 0.95f);

        private readonly List<LineRenderer> trailSegments = new List<LineRenderer>();
        private readonly List<GameObject> eventMarkers = new List<GameObject>();
        private GhostRunStore store;
        private GhostRun currentRun;
        private GhostRun loadedGhost;
        private GhostRun bestGhost;
        private GameObject ghostMarker;
        private Rect windowRect = new Rect(220f, 80f, 300f, 238f);
        private double nextSampleTime;
        private int lastStage = -1;
        private Vessel.Situations lastSituation = Vessel.Situations.PRELAUNCH;
        private string currentSlot = "default";
        private string slotDraft = "default";
        private bool isRecording = true;
        private bool showGhost = true;
        private bool showWindow = true;
        private string status = "Ready";
        private GUIStyle labelStyle;
        private GUIStyle overlayStyle;

        public void Awake()
        {
            store = new GhostRunStore();
            CreateGhostMarker();
            GameEvents.onStageActivate.Add(OnStageActivate);
            GameEvents.onVesselDestroy.Add(OnVesselDestroy);
            GameEvents.onCrash.Add(OnCrash);
        }

        public void Start()
        {
            StartNewRun();
            LoadGhostForActiveVessel();
        }

        public void OnDestroy()
        {
            SaveCurrentRun();
            ClearTrailSegments();
            ClearEventMarkers();
            if (ghostMarker != null)
            {
                Destroy(ghostMarker);
            }
            GameEvents.onStageActivate.Remove(OnStageActivate);
            GameEvents.onVesselDestroy.Remove(OnVesselDestroy);
            GameEvents.onCrash.Remove(OnCrash);
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsFlight && Input.GetKeyDown(KeyCode.F8))
            {
                showWindow = !showWindow;
            }

            UpdateGhostTrailVisibility();
            UpdateGhostMarker();

            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null || !isRecording || currentRun == null)
            {
                return;
            }

            double now = Planetarium.GetUniversalTime();
            if (now >= nextSampleTime)
            {
                CaptureSample(vessel);
                nextSampleTime = now + SampleInterval;
            }
        }

        public void OnGUI()
        {
            if (!HighLogic.LoadedSceneIsFlight)
            {
                return;
            }

            EnsureStyles();
            DrawGhostOverlayLabel();
            DrawEventOverlayLabels();

            if (!showWindow)
            {
                return;
            }

            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "Ghost Rehearsal");
        }

        private void DrawWindow(int id)
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            GUILayout.BeginVertical();

            if (vessel == null)
            {
                GUILayout.Label("No active vessel.", labelStyle);
            }
            else
            {
                GUILayout.Label("Vessel: " + vessel.vesselName, labelStyle);
                GUILayout.Label("Slot: " + currentSlot, labelStyle);
                GUILayout.Label("Recording samples: " + (currentRun == null ? "0" : currentRun.Samples.Count.ToString()), labelStyle);
                GUILayout.Label("Loaded ghost: " + (loadedGhost == null ? "none" : loadedGhost.SlotName + " / " + loadedGhost.Samples.Count + " samples"), labelStyle);
                GUILayout.Label("Best ghost: " + (bestGhost == null ? "none" : bestGhost.Samples.Count + " samples"), labelStyle);

                GhostSample current = CreateSample(vessel);
                GhostSample ghost = FindGhostSample(current.MissionTime);
                if (ghost != null)
                {
                    GUILayout.Space(6f);
                    GUILayout.Label("Time delta: " + FormatSigned(current.MissionTime - ghost.MissionTime, "s"), labelStyle);
                    GUILayout.Label("Altitude delta: " + FormatSigned(current.Altitude - ghost.Altitude, "m"), labelStyle);
                    GUILayout.Label("Speed delta: " + FormatSigned(current.SurfaceSpeed - ghost.SurfaceSpeed, "m/s"), labelStyle);
                    GUILayout.Label("LF delta: " + FormatSigned(current.LiquidFuel - ghost.LiquidFuel, "u"), labelStyle);
                }

                GhostSample best = FindBestSample(current.MissionTime);
                if (best != null)
                {
                    GUILayout.Label("Best fuel delta: " + FormatSigned(CurrentFuel(current) - CurrentFuel(best), "u"), labelStyle);
                    GUILayout.Label("Best altitude delta: " + FormatSigned(current.Altitude - best.Altitude, "m"), labelStyle);
                }
            }

            GUILayout.Space(8f);
            isRecording = GUILayout.Toggle(isRecording, "Record");
            showGhost = GUILayout.Toggle(showGhost, "Show ghost trail");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Slot", GUILayout.Width(34f));
            slotDraft = GUILayout.TextField(slotDraft, GUILayout.Width(116f));
            if (GUILayout.Button("Use", GUILayout.Width(48f)))
            {
                SetCurrentSlot(slotDraft);
            }
            if (GUILayout.Button("Best", GUILayout.Width(58f)))
            {
                SetCurrentSlot("best");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save"))
            {
                SaveCurrentRun();
            }
            if (GUILayout.Button("Load Ghost"))
            {
                LoadGhostForActiveVessel();
            }
            if (GUILayout.Button("Save Best"))
            {
                SaveBestRun();
            }
            if (GUILayout.Button("New Run"))
            {
                SaveCurrentRun();
                StartNewRun();
            }
            GUILayout.EndHorizontal();

            GUILayout.Label(status, labelStyle);
            GUILayout.Label("F8 toggles this window.", labelStyle);
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void DrawGhostOverlayLabel()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (!showGhost || loadedGhost == null || vessel == null || vessel.mainBody == null)
            {
                return;
            }

            GhostSample ghost = FindGhostSample(vessel.missionTime);
            if (ghost == null || ghost.BodyName != vessel.mainBody.bodyName)
            {
                return;
            }

            Vector3d ghostWorld = ghost.ToWorldPosition(vessel.mainBody);
            Vector3 screen = WorldToGuiPoint(ghostWorld);
            if (screen.z <= 0f)
            {
                return;
            }

            GhostSample current = CreateSample(vessel);
            double distance = Vector3d.Distance(vessel.GetWorldPos3D(), ghostWorld);
            string text = "GHOST  " + FormatDistance(distance) + "  " + FormatSigned(current.SurfaceSpeed - ghost.SurfaceSpeed, "m/s");
            GUI.Label(new Rect(screen.x + 12f, screen.y - 16f, 260f, 24f), text, overlayStyle);
        }

        private void DrawEventOverlayLabels()
        {
            if (!showGhost || loadedGhost == null || MapView.MapIsEnabled)
            {
                return;
            }

            for (int i = 0; i < eventMarkers.Count; i++)
            {
                GameObject marker = eventMarkers[i];
                if (marker == null || !marker.activeSelf)
                {
                    continue;
                }

                Vector3 screen = WorldToGuiPoint(marker.transform.position);
                if (screen.z <= 0f)
                {
                    continue;
                }

                string label = marker.name.Replace("GhostRehearsalEvent: ", "");
                GUI.Label(new Rect(screen.x + 8f, screen.y - 10f, 160f, 22f), label, overlayStyle);
            }
        }

        private static Vector3 WorldToGuiPoint(Vector3d worldPosition)
        {
            Camera camera = null;
            if (FlightCamera.fetch != null)
            {
                camera = FlightCamera.fetch.mainCamera;
            }
            if (camera == null)
            {
                camera = Camera.main;
            }
            if (camera == null)
            {
                return new Vector3(0f, 0f, -1f);
            }

            Vector3 screen = camera.WorldToScreenPoint(worldPosition);
            screen.y = Screen.height - screen.y;
            return screen;
        }

        private void StartNewRun()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            currentRun = new GhostRun();
            currentRun.StartedUtc = DateTime.UtcNow.ToString("o");
            currentRun.StartUniversalTime = Planetarium.GetUniversalTime();
            currentRun.SlotName = currentSlot;

            if (vessel != null)
            {
                currentRun.VesselName = vessel.vesselName;
                currentRun.BodyName = vessel.mainBody == null ? "" : vessel.mainBody.bodyName;
                lastStage = vessel.currentStage;
                lastSituation = vessel.situation;
                CaptureSample(vessel);
            }

            nextSampleTime = Planetarium.GetUniversalTime() + SampleInterval;
            status = "Started recording.";
        }

        private void SaveCurrentRun()
        {
            if (currentRun == null || !currentRun.HasSamples)
            {
                status = "Nothing to save yet.";
                return;
            }

            store.Save(currentRun, currentSlot);
            status = "Saved slot " + currentSlot + " for " + currentRun.VesselName + ".";
            Debug.Log("[GhostRehearsal] Saved " + currentRun.Samples.Count + " samples for " + currentRun.VesselName + " slot " + currentSlot);
        }

        private void SaveBestRun()
        {
            if (currentRun == null || !currentRun.HasSamples)
            {
                status = "Nothing to save as best yet.";
                return;
            }

            string oldSlot = currentRun.SlotName;
            store.Save(currentRun, "best");
            currentRun.SlotName = oldSlot;
            status = "Saved best ghost for " + currentRun.VesselName + ".";
            bestGhost = store.Load(currentRun.VesselName, "best");
            Debug.Log("[GhostRehearsal] Saved best ghost with " + currentRun.Samples.Count + " samples for " + currentRun.VesselName);
        }

        private void SetCurrentSlot(string slotName)
        {
            currentSlot = GhostRunStore.NormalizeSlotName(slotName);
            slotDraft = currentSlot;
            if (currentRun != null)
            {
                currentRun.SlotName = currentSlot;
            }
            status = "Selected slot " + currentSlot + ".";
            LoadGhostForActiveVessel();
        }

        private void LoadGhostForActiveVessel()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null)
            {
                status = "No active vessel to load for.";
                return;
            }

            loadedGhost = store.Load(vessel.vesselName, currentSlot);
            bestGhost = store.Load(vessel.vesselName, "best");
            status = loadedGhost == null ? "No saved ghost for slot " + currentSlot + "." : "Loaded " + currentSlot + " for " + vessel.vesselName + ".";
            Debug.Log("[GhostRehearsal] " + status);
            RebuildTrailSegments();
            RebuildEventMarkers();
        }

        private void CaptureSample(Vessel vessel)
        {
            GhostSample sample = CreateSample(vessel);
            currentRun.Samples.Add(sample);
            if (currentRun.Samples.Count % 10 == 0)
            {
                Debug.Log("[GhostRehearsal] Recorded " + currentRun.Samples.Count + " samples for " + currentRun.VesselName);
            }

            if (sample.Stage != lastStage)
            {
                AddEvent(sample.MissionTime, "stage", "Stage " + sample.Stage);
                lastStage = sample.Stage;
            }

            if (vessel.situation != lastSituation)
            {
                if (vessel.situation == Vessel.Situations.LANDED)
                {
                    AddEvent(sample.MissionTime, "touchdown", "Touchdown");
                }
                else if (vessel.situation == Vessel.Situations.SPLASHED)
                {
                    AddEvent(sample.MissionTime, "splashdown", "Splashdown");
                }

                lastSituation = vessel.situation;
            }
        }

        private GhostSample CreateSample(Vessel vessel)
        {
            double liquidFuel = 0d;
            double liquidFuelCapacity = 0d;
            double oxidizer = 0d;
            double oxidizerCapacity = 0d;
            PartResourceDefinition liquidFuelDefinition = PartResourceLibrary.Instance.GetDefinition("LiquidFuel");
            PartResourceDefinition oxidizerDefinition = PartResourceLibrary.Instance.GetDefinition("Oxidizer");

            if (liquidFuelDefinition != null)
            {
                vessel.GetConnectedResourceTotals(liquidFuelDefinition.id, out liquidFuel, out liquidFuelCapacity);
            }

            if (oxidizerDefinition != null)
            {
                vessel.GetConnectedResourceTotals(oxidizerDefinition.id, out oxidizer, out oxidizerCapacity);
            }

            Orbit orbit = vessel.orbit;
            return new GhostSample
            {
                MissionTime = vessel.missionTime,
                UniversalTime = Planetarium.GetUniversalTime(),
                BodyName = vessel.mainBody == null ? "" : vessel.mainBody.bodyName,
                Latitude = vessel.latitude,
                Longitude = vessel.longitude,
                Altitude = vessel.altitude,
                RadarAltitude = vessel.radarAltitude,
                SurfaceSpeed = vessel.srfSpeed,
                VerticalSpeed = vessel.verticalSpeed,
                OrbitalSpeed = vessel.obt_speed,
                Apoapsis = orbit == null ? 0d : orbit.ApA,
                Periapsis = orbit == null ? 0d : orbit.PeA,
                LiquidFuel = liquidFuel,
                Oxidizer = oxidizer,
                Stage = vessel.currentStage
            };
        }

        private GhostSample FindGhostSample(double missionTime)
        {
            return FindSampleInRun(loadedGhost, missionTime);
        }

        private GhostSample FindBestSample(double missionTime)
        {
            return FindSampleInRun(bestGhost, missionTime);
        }

        private static GhostSample FindSampleInRun(GhostRun run, double missionTime)
        {
            if (run == null || run.Samples.Count == 0)
            {
                return null;
            }

            if (run.Samples.Count == 1)
            {
                return Math.Abs(run.Samples[0].MissionTime - missionTime) <= 2.5d ? run.Samples[0] : null;
            }

            GhostSample first = run.Samples[0];
            GhostSample last = run.Samples[run.Samples.Count - 1];
            if (missionTime < first.MissionTime || missionTime > last.MissionTime)
            {
                return null;
            }

            for (int i = 0; i < run.Samples.Count - 1; i++)
            {
                GhostSample before = run.Samples[i];
                GhostSample after = run.Samples[i + 1];
                if (missionTime >= before.MissionTime && missionTime <= after.MissionTime)
                {
                    return InterpolateGhostSample(before, after, missionTime);
                }
            }

            return null;
        }

        private static GhostSample InterpolateGhostSample(GhostSample before, GhostSample after, double missionTime)
        {
            double span = after.MissionTime - before.MissionTime;
            double t = span <= 0d ? 0d : (missionTime - before.MissionTime) / span;

            return new GhostSample
            {
                MissionTime = missionTime,
                UniversalTime = Lerp(before.UniversalTime, after.UniversalTime, t),
                BodyName = before.BodyName,
                Latitude = Lerp(before.Latitude, after.Latitude, t),
                Longitude = LerpAngle(before.Longitude, after.Longitude, t),
                Altitude = Lerp(before.Altitude, after.Altitude, t),
                RadarAltitude = Lerp(before.RadarAltitude, after.RadarAltitude, t),
                SurfaceSpeed = Lerp(before.SurfaceSpeed, after.SurfaceSpeed, t),
                VerticalSpeed = Lerp(before.VerticalSpeed, after.VerticalSpeed, t),
                OrbitalSpeed = Lerp(before.OrbitalSpeed, after.OrbitalSpeed, t),
                Apoapsis = Lerp(before.Apoapsis, after.Apoapsis, t),
                Periapsis = Lerp(before.Periapsis, after.Periapsis, t),
                LiquidFuel = Lerp(before.LiquidFuel, after.LiquidFuel, t),
                Oxidizer = Lerp(before.Oxidizer, after.Oxidizer, t),
                Stage = t < 0.5d ? before.Stage : after.Stage
            };
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + ((b - a) * t);
        }

        private static double LerpAngle(double a, double b, double t)
        {
            double delta = b - a;
            if (delta > 180d)
            {
                delta -= 360d;
            }
            else if (delta < -180d)
            {
                delta += 360d;
            }

            double result = a + (delta * t);
            if (result > 180d)
            {
                result -= 360d;
            }
            else if (result < -180d)
            {
                result += 360d;
            }

            return result;
        }

        private void RebuildTrailSegments()
        {
            ClearTrailSegments();
            if (loadedGhost == null || loadedGhost.Samples.Count < 2)
            {
                return;
            }

            Vessel vessel = FlightGlobals.ActiveVessel;
            CelestialBody body = vessel == null ? null : vessel.mainBody;
            if (body == null)
            {
                return;
            }

            LineRenderer segment = null;
            string bodyName = "";
            int segmentPosition = 0;

            for (int i = 0; i < loadedGhost.Samples.Count; i++)
            {
                GhostSample sample = loadedGhost.Samples[i];
                if (sample.BodyName != body.bodyName)
                {
                    segment = null;
                    continue;
                }

                if (segment == null || sample.BodyName != bodyName)
                {
                    segment = CreateTrailSegment();
                    bodyName = sample.BodyName;
                    segmentPosition = 0;
                }

                segment.positionCount = segmentPosition + 1;
                segment.SetPosition(segmentPosition, ScaledSpace.LocalToScaledSpace(sample.ToWorldPosition(body)));
                segmentPosition++;
            }
        }

        private void RebuildEventMarkers()
        {
            ClearEventMarkers();
            if (loadedGhost == null || loadedGhost.Events.Count == 0)
            {
                return;
            }

            Vessel vessel = FlightGlobals.ActiveVessel;
            CelestialBody body = vessel == null ? null : vessel.mainBody;
            if (body == null)
            {
                return;
            }

            for (int i = 0; i < loadedGhost.Events.Count; i++)
            {
                GhostEvent ghostEvent = loadedGhost.Events[i];
                GhostSample sample = FindNearestRecordedSample(loadedGhost, ghostEvent.MissionTime);
                if (sample == null || sample.BodyName != body.bodyName)
                {
                    continue;
                }

                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "GhostRehearsalEvent: " + ghostEvent.Label;
                marker.transform.position = sample.ToWorldPosition(body);
                marker.transform.localScale = new Vector3(3f, 3f, 3f);
                RemoveCollider(marker);
                ApplyMaterial(marker, GetEventColor(ghostEvent.Kind));
                marker.SetActive(false);
                eventMarkers.Add(marker);
            }
        }

        private static GhostSample FindNearestRecordedSample(GhostRun run, double missionTime)
        {
            if (run == null || run.Samples.Count == 0)
            {
                return null;
            }

            GhostSample best = null;
            double bestDelta = double.MaxValue;
            for (int i = 0; i < run.Samples.Count; i++)
            {
                double delta = Math.Abs(run.Samples[i].MissionTime - missionTime);
                if (delta < bestDelta)
                {
                    best = run.Samples[i];
                    bestDelta = delta;
                }
            }

            return bestDelta <= 3d ? best : null;
        }

        private static Vector3d FindRunDirection(GhostRun run, CelestialBody body, double missionTime)
        {
            if (run == null || body == null || run.Samples.Count < 2)
            {
                return Vector3d.zero;
            }

            GhostSample before = null;
            GhostSample after = null;
            for (int i = 0; i < run.Samples.Count - 1; i++)
            {
                if (missionTime >= run.Samples[i].MissionTime && missionTime <= run.Samples[i + 1].MissionTime)
                {
                    before = run.Samples[i];
                    after = run.Samples[i + 1];
                    break;
                }
            }

            if (before == null || after == null)
            {
                return Vector3d.zero;
            }

            Vector3d direction = after.ToWorldPosition(body) - before.ToWorldPosition(body);
            return direction.sqrMagnitude > 0.01d ? direction.normalized : Vector3d.zero;
        }

        private static Color GetEventColor(string kind)
        {
            if (kind == "stage")
            {
                return EventStageColor;
            }
            if (kind == "touchdown" || kind == "splashdown")
            {
                return EventLandingColor;
            }

            return EventCrashColor;
        }

        private LineRenderer CreateTrailSegment()
        {
            GameObject obj = new GameObject("GhostRehearsalTrail");
            obj.layer = 10;
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            Material material = CreateGhostMaterial();
            if (material != null)
            {
                line.material = material;
            }
            line.startColor = TrailColor;
            line.endColor = TrailColor;
            line.startWidth = 1.25f;
            line.endWidth = 1.25f;
            trailSegments.Add(line);
            return line;
        }

        private void CreateGhostMarker()
        {
            ghostMarker = new GameObject("GhostRehearsalMarker");
            ghostMarker.name = "GhostRehearsalMarker";
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "GhostRehearsalMarkerBody";
            body.transform.parent = ghostMarker.transform;
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = new Vector3(1.3f, 3.2f, 1.3f);
            RemoveCollider(body);
            ApplyMaterial(body, TrailColor);

            CreateMarkerFin(new Vector3(1.25f, -2.0f, 0f), new Vector3(0.28f, 1.2f, 0.7f));
            CreateMarkerFin(new Vector3(-1.25f, -2.0f, 0f), new Vector3(0.28f, 1.2f, 0.7f));
            CreateMarkerFin(new Vector3(0f, -2.0f, 1.25f), new Vector3(0.7f, 1.2f, 0.28f));

            ghostMarker.SetActive(false);
        }

        private void CreateMarkerFin(Vector3 localPosition, Vector3 localScale)
        {
            GameObject fin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fin.name = "GhostRehearsalMarkerFin";
            fin.transform.parent = ghostMarker.transform;
            fin.transform.localPosition = localPosition;
            fin.transform.localRotation = Quaternion.identity;
            fin.transform.localScale = localScale;
            RemoveCollider(fin);
            ApplyMaterial(fin, TrailColor);
        }

        private static Material CreateGhostMaterial()
        {
            Shader shader = Shader.Find("Particles/Additive");
            if (shader == null)
            {
                shader = Shader.Find("KSP/Alpha/Unlit Transparent");
            }
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Diffuse");
            }
            if (shader == null)
            {
                return null;
            }

            Material material = new Material(shader);
            material.color = TrailColor;
            return material;
        }

        private static void ApplyMaterial(GameObject obj, Color color)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            Material material = CreateGhostMaterial();
            if (material != null)
            {
                renderer.material = material;
            }

            renderer.material.color = color;
        }

        private static void RemoveCollider(GameObject obj)
        {
            Collider collider = obj.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
            }
        }

        private void UpdateGhostMarker()
        {
            if (ghostMarker == null)
            {
                return;
            }

            Vessel vessel = FlightGlobals.ActiveVessel;
            if (!showGhost || loadedGhost == null || vessel == null || vessel.mainBody == null)
            {
                ghostMarker.SetActive(false);
                return;
            }

            GhostSample ghost = FindGhostSample(vessel.missionTime);
            if (ghost == null || ghost.BodyName != vessel.mainBody.bodyName)
            {
                ghostMarker.SetActive(false);
                return;
            }

            Vector3d ghostWorld = ghost.ToWorldPosition(vessel.mainBody);
            ghostMarker.transform.position = ghostWorld;
            ghostMarker.transform.rotation = GetGhostMarkerRotation(loadedGhost, vessel.mainBody, ghostWorld, vessel.missionTime);
            ghostMarker.SetActive(!MapView.MapIsEnabled);
        }

        private static Quaternion GetGhostMarkerRotation(GhostRun run, CelestialBody body, Vector3d ghostWorld, double missionTime)
        {
            Vector3d direction = FindRunDirection(run, body, missionTime);
            if (direction == Vector3d.zero)
            {
                direction = (ghostWorld - body.position).normalized;
            }

            return Quaternion.FromToRotation(Vector3.up, direction);
        }

        private void UpdateGhostTrailVisibility()
        {
            bool active = showGhost && loadedGhost != null && MapView.MapIsEnabled;
            for (int i = 0; i < trailSegments.Count; i++)
            {
                if (trailSegments[i] != null)
                {
                    trailSegments[i].enabled = active;
                }
            }

            bool eventActive = showGhost && loadedGhost != null && !MapView.MapIsEnabled;
            for (int i = 0; i < eventMarkers.Count; i++)
            {
                if (eventMarkers[i] != null)
                {
                    eventMarkers[i].SetActive(eventActive);
                }
            }
        }

        private void ClearTrailSegments()
        {
            for (int i = 0; i < trailSegments.Count; i++)
            {
                if (trailSegments[i] != null)
                {
                    Destroy(trailSegments[i].gameObject);
                }
            }
            trailSegments.Clear();
        }

        private void ClearEventMarkers()
        {
            for (int i = 0; i < eventMarkers.Count; i++)
            {
                if (eventMarkers[i] != null)
                {
                    Destroy(eventMarkers[i]);
                }
            }
            eventMarkers.Clear();
        }

        private void OnStageActivate(int stage)
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel != null)
            {
                AddEvent(vessel.missionTime, "stage", "Stage " + stage);
            }
        }

        private void OnVesselDestroy(Vessel vessel)
        {
            if (vessel == FlightGlobals.ActiveVessel)
            {
                AddEvent(vessel.missionTime, "destroyed", "Vessel destroyed");
            }
        }

        private void OnCrash(EventReport report)
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel != null)
            {
                AddEvent(vessel.missionTime, "crash", "Crash");
            }
        }

        private void AddEvent(double missionTime, string kind, string label)
        {
            if (currentRun == null)
            {
                return;
            }

            currentRun.Events.Add(new GhostEvent
            {
                MissionTime = missionTime,
                Kind = kind,
                Label = label
            });
        }

        private void EnsureStyles()
        {
            if (labelStyle != null)
            {
                return;
            }

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.wordWrap = true;
            overlayStyle = new GUIStyle(GUI.skin.label);
            overlayStyle.normal.textColor = Color.white;
            overlayStyle.fontStyle = FontStyle.Bold;
            overlayStyle.alignment = TextAnchor.MiddleLeft;
        }

        private static string FormatSigned(double value, string unit)
        {
            return (value >= 0d ? "+" : "") + value.ToString("0.0") + " " + unit;
        }

        private static double CurrentFuel(GhostSample sample)
        {
            return sample == null ? 0d : sample.LiquidFuel + sample.Oxidizer;
        }

        private static string FormatDistance(double value)
        {
            if (Math.Abs(value) >= 1000d)
            {
                return (value / 1000d).ToString("0.00") + " km";
            }

            return value.ToString("0") + " m";
        }
    }
}
