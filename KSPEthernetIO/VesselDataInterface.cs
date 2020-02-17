using KSP.UI.Screens;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static KSPEthernetIO.DataPackets;
using static KSPEthernetIO.Utilities;

namespace KSPEthernetIO
{
    class VesselDataInterface : IVesselDataInterface
    {
        private double _lastUpdate = 0.0f;
        private double _deltaT = 1.0f;
        private double _missionTime = 0;
        private double _missionTimeOld = 0;
        private double _theTime = 0;
        private double _refreshrate = 1.0f;
        private Vessel _activeVessel = null;

        public enum EnumAG : int
        {
            SAS,
            RCS,
            Light,
            Gear,
            Brakes,
            Abort,
            Custom01,
            Custom02,
            Custom03,
            Custom04,
            Custom05,
            Custom06,
            Custom07,
            Custom08,
            Custom09,
            Custom10,
        };

        /// <summary>
        /// Ressource wrapper structure
        /// </summary>
        public struct IOResource
        {
            public float Max;
            public float Current;
        }

        private VesselData _vData;
        private static byte _vesselSync = 0; //Save sync value static to share between different flights

        /// <summary>
        /// Initialize VesselDataInterface
        /// </summary>
        public VesselDataInterface()
        {
            _vData = new VesselData();
            _vData.id = VDid;
            _vData.vesselSync = _vesselSync;
            _refreshrate = Settings.Refresh;
        }

        public byte getVesselSync()
        {
            return _vData.vesselSync;
        }

        /// <summary>
        /// Runs a timer. If the refresh time is exceed updates all vessel data and 
        /// triggers a VesselDataInvalidated event.
        /// </summary>
        public void UpdateVesselData()
        {
            if (FlightGlobals.ActiveVessel != null)
            {
                bool vesselChanged = false;
                if (_activeVessel == null || _activeVessel.id != FlightGlobals.ActiveVessel.id) vesselChanged = true;
                if (vesselChanged)
                {
                    _activeVessel = FlightGlobals.ActiveVessel;
                    _vData.vesselSync++;
                    if (_vData.vesselSync == 0) _vData.vesselSync++;
                    _vesselSync = _vData.vesselSync;
                }

                _theTime = Time.unscaledTime;
                if ((_theTime - _lastUpdate)*1000 > _refreshrate)
                {
                    IOResource TempR = new IOResource();

                    Vessel ActiveVessel = FlightGlobals.ActiveVessel;

                    _lastUpdate = _theTime;

                    List<Part> ActiveEngines = new List<Part>();
                    ActiveEngines = GetListOfActivatedEngines(ActiveVessel);

                    _vData.AP = (float)ActiveVessel.orbit.ApA;
                    _vData.PE = (float)ActiveVessel.orbit.PeA;
                    _vData.SemiMajorAxis = (float)ActiveVessel.orbit.semiMajorAxis;
                    _vData.SemiMinorAxis = (float)ActiveVessel.orbit.semiMinorAxis;
                    _vData.e = (float)ActiveVessel.orbit.eccentricity;
                    _vData.inc = (float)ActiveVessel.orbit.inclination;
                    _vData.VVI = (float)ActiveVessel.verticalSpeed;
                    _vData.G = (float)ActiveVessel.geeForce;
                    _vData.TAp = (int)Math.Round(ActiveVessel.orbit.timeToAp);
                    _vData.TPe = (int)Math.Round(ActiveVessel.orbit.timeToPe);
                    _vData.Density = (float)ActiveVessel.atmDensity;
                    _vData.TrueAnomaly = (float)ActiveVessel.orbit.trueAnomaly;
                    _vData.period = (int)Math.Round(ActiveVessel.orbit.period);

                    double ASL = ActiveVessel.mainBody.GetAltitude(ActiveVessel.CoM);
                    double AGL = (ASL - ActiveVessel.terrainAltitude);

                    if (AGL < ASL)
                        _vData.RAlt = (float)AGL;
                    else
                        _vData.RAlt = (float)ASL;

                    _vData.Alt = (float)ASL;
                    _vData.Vsurf = (float)ActiveVessel.srfSpeed;
                    _vData.Lat = (float)ActiveVessel.latitude;
                    _vData.Lon = (float)ActiveVessel.longitude;

                    TempR = GetResourceTotal(ActiveVessel, "LiquidFuel");
                    _vData.LiquidFuelTot = TempR.Max;
                    _vData.LiquidFuel = TempR.Current;

                    _vData.LiquidFuelTotS = (float)ProspectForResourceMax("LiquidFuel", ActiveEngines);
                    _vData.LiquidFuelS = (float)ProspectForResource("LiquidFuel", ActiveEngines);

                    TempR = GetResourceTotal(ActiveVessel, "Oxidizer");
                    _vData.OxidizerTot = TempR.Max;
                    _vData.Oxidizer = TempR.Current;

                    _vData.OxidizerTotS = (float)ProspectForResourceMax("Oxidizer", ActiveEngines);
                    _vData.OxidizerS = (float)ProspectForResource("Oxidizer", ActiveEngines);

                    TempR = GetResourceTotal(ActiveVessel, "ElectricCharge");
                    _vData.EChargeTot = TempR.Max;
                    _vData.ECharge = TempR.Current;
                    TempR = GetResourceTotal(ActiveVessel, "MonoPropellant");
                    _vData.MonoPropTot = TempR.Max;
                    _vData.MonoProp = TempR.Current;
                    TempR = GetResourceTotal(ActiveVessel, "IntakeAir");
                    _vData.IntakeAirTot = TempR.Max;
                    _vData.IntakeAir = TempR.Current;
                    TempR = GetResourceTotal(ActiveVessel, "SolidFuel");
                    _vData.SolidFuelTot = TempR.Max;
                    _vData.SolidFuel = TempR.Current;
                    TempR = GetResourceTotal(ActiveVessel, "XenonGas");
                    _vData.XenonGasTot = TempR.Max;
                    _vData.XenonGas = TempR.Current;

                    _missionTime = ActiveVessel.missionTime;
                    _deltaT = _missionTime - _missionTimeOld;
                    _missionTimeOld = _missionTime;

                    _vData.MissionTime = (UInt32)Math.Round(_missionTime);
                    _vData.deltaTime = (float)_deltaT;

                    _vData.VOrbit = (float)ActiveVessel.orbit.GetVel().magnitude;

                    Vector3d CoM, north, up, east;
                    Quaternion rotationSurface;
                    CoM = ActiveVessel.CoM;
                    up = (CoM - ActiveVessel.mainBody.position).normalized;
                    north = Vector3d.Exclude(up, (ActiveVessel.mainBody.position + ActiveVessel.mainBody.transform.up * (float)ActiveVessel.mainBody.Radius) - CoM).normalized;
                    east = Vector3d.Cross(up, north);
                    rotationSurface = Quaternion.LookRotation(north, up);
                    Vector3d attitude = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(ActiveVessel.GetTransform().rotation) * rotationSurface).eulerAngles;

                    _vData.Roll = ToScaledUInt((attitude.z > 180) ? (attitude.z - 360.0) : attitude.z);
                    _vData.Pitch = ToScaledUInt((attitude.x > 180) ? (attitude.x - 360.0) : attitude.x);
                    _vData.Heading = ToScaledUInt((attitude.y > 180) ? (attitude.y - 360.0) : attitude.y);

                    Vector3d prograde = new Vector3d(0, 0, 0);
                    switch (FlightGlobals.speedDisplayMode)
                    {
                        case FlightGlobals.SpeedDisplayModes.Surface:
                            prograde = ActiveVessel.srf_velocity.normalized;
                            break;
                        case FlightGlobals.SpeedDisplayModes.Orbit:
                            prograde = ActiveVessel.obt_velocity.normalized;
                            break;
                        case FlightGlobals.SpeedDisplayModes.Target:
                            prograde = FlightGlobals.ship_tgtVelocity;
                            break;
                    }

                    NavHeading zeroHeading; zeroHeading.Pitch = zeroHeading.Heading = 0;
                    NavHeading Prograde = WorldVecToNavHeading(up, north, east, prograde), Target = zeroHeading, Maneuver = zeroHeading;

                    _vData.ProgradeHeading = ToScaledUInt(Prograde.Heading);
                    _vData.ProgradePitch = ToScaledUInt(Prograde.Pitch);

                    if (TargetExists())
                    {
                        _vData.TargetDist = (float)Vector3.Distance(FlightGlobals.fetch.VesselTarget.GetVessel().transform.position, ActiveVessel.transform.position);
                        _vData.TargetV = (float)FlightGlobals.ship_tgtVelocity.magnitude;
                        Target = WorldVecToNavHeading(up, north, east, ActiveVessel.targetObject.GetTransform().position - ActiveVessel.transform.position);
                    }
                    _vData.TargetHeading = ToScaledUInt(Target.Heading);
                    _vData.TargetPitch = ToScaledUInt(Target.Pitch);

                    _vData.NormalHeading = ToScaledUInt(WorldVecToNavHeading(up, north, east, Vector3d.Cross(ActiveVessel.obt_velocity.normalized, up)).Heading);


                    _vData.MNTime = 0;
                    _vData.MNDeltaV = 0;
                    if (ActiveVessel.patchedConicSolver != null)
                    {
                        if (ActiveVessel.patchedConicSolver.maneuverNodes != null)
                        {
                            if (ActiveVessel.patchedConicSolver.maneuverNodes.Count > 0)
                            {
                                _vData.MNTime = (UInt32)Math.Round(ActiveVessel.patchedConicSolver.maneuverNodes[0].UT - Planetarium.GetUniversalTime());
                                _vData.MNDeltaV = (float)ActiveVessel.patchedConicSolver.maneuverNodes[0].GetBurnVector(ActiveVessel.patchedConicSolver.maneuverNodes[0].patch).magnitude; //Added JS

                                Maneuver = WorldVecToNavHeading(up, north, east, ActiveVessel.patchedConicSolver.maneuverNodes[0].GetBurnVector(ActiveVessel.patchedConicSolver.maneuverNodes[0].patch));
                            }
                        }
                    }
                    _vData.ManeuverHeading = ToScaledUInt(Maneuver.Heading);
                    _vData.ManeuverPitch = ToScaledUInt(Maneuver.Pitch);





                    ControlStatus((int)EnumAG.SAS, ActiveVessel.ActionGroups[KSPActionGroup.SAS]);
                    ControlStatus((int)EnumAG.RCS, ActiveVessel.ActionGroups[KSPActionGroup.RCS]);
                    ControlStatus((int)EnumAG.Light, ActiveVessel.ActionGroups[KSPActionGroup.Light]);
                    ControlStatus((int)EnumAG.Gear, ActiveVessel.ActionGroups[KSPActionGroup.Gear]);
                    ControlStatus((int)EnumAG.Brakes, ActiveVessel.ActionGroups[KSPActionGroup.Brakes]);
                    ControlStatus((int)EnumAG.Abort, ActiveVessel.ActionGroups[KSPActionGroup.Abort]);
                    ControlStatus((int)EnumAG.Custom01, ActiveVessel.ActionGroups[KSPActionGroup.Custom01]);
                    ControlStatus((int)EnumAG.Custom02, ActiveVessel.ActionGroups[KSPActionGroup.Custom02]);
                    ControlStatus((int)EnumAG.Custom03, ActiveVessel.ActionGroups[KSPActionGroup.Custom03]);
                    ControlStatus((int)EnumAG.Custom04, ActiveVessel.ActionGroups[KSPActionGroup.Custom04]);
                    ControlStatus((int)EnumAG.Custom05, ActiveVessel.ActionGroups[KSPActionGroup.Custom05]);
                    ControlStatus((int)EnumAG.Custom06, ActiveVessel.ActionGroups[KSPActionGroup.Custom06]);
                    ControlStatus((int)EnumAG.Custom07, ActiveVessel.ActionGroups[KSPActionGroup.Custom07]);
                    ControlStatus((int)EnumAG.Custom08, ActiveVessel.ActionGroups[KSPActionGroup.Custom08]);
                    ControlStatus((int)EnumAG.Custom09, ActiveVessel.ActionGroups[KSPActionGroup.Custom09]);
                    ControlStatus((int)EnumAG.Custom10, ActiveVessel.ActionGroups[KSPActionGroup.Custom10]);

                    if (ActiveVessel.orbit.referenceBody != null)
                    {
                        _vData.SOINumber = GetSOINumber(ActiveVessel.orbit.referenceBody.name);
                    }

                    _vData.MaxOverHeat = GetMaxOverHeat(ActiveVessel);
                    _vData.MachNumber = (float)ActiveVessel.mach;
                    _vData.IAS = (float)ActiveVessel.indicatedAirSpeed;
                    _vData.CurrentStage = (byte)StageManager.CurrentStage;
                    _vData.TotalStage = (byte)StageManager.StageCount;

                    _vData.NavballSASMode = (byte)(((int)FlightGlobals.speedDisplayMode + 1) << 4); //get navball speed display mode
                    if (ActiveVessel.ActionGroups[KSPActionGroup.SAS])
                    {
                        _vData.NavballSASMode = (byte)(((int)FlightGlobals.ActiveVessel.Autopilot.Mode + 1) | _vData.NavballSASMode);
                    }


                    //target distance and velocity stuff                    

                    _vData.TargetDist = 0;
                    _vData.TargetV = 0;

                    if (TargetExists())
                    {
                        _vData.TargetDist = (float)Vector3.Distance(FlightGlobals.fetch.VesselTarget.GetVessel().transform.position, ActiveVessel.transform.position);
                        _vData.TargetV = (float)FlightGlobals.ship_tgtVelocity.magnitude;
                    }


                    _vData.NavballSASMode = (byte)(((int)FlightGlobals.speedDisplayMode + 1) << 4); //get navball speed display mode
                    if (ActiveVessel.ActionGroups[KSPActionGroup.SAS])
                    {
                        _vData.NavballSASMode = (byte)(((int)FlightGlobals.ActiveVessel.Autopilot.Mode + 1) | _vData.NavballSASMode);
                    }


                    //Notify listeners
                    NotifyInvalidate(_vData);
                }



            }
        }

        /// <summary>
        /// Uses the EnumAG to set a specific control bit.
        /// </summary>
        /// <param name="n">EnumAG element</param>
        /// <param name="s">True to activate</param>
        private void ControlStatus(int n, bool s)
        {
            if (s)
                _vData.ActionGroups |= (UInt16)(1 << n);       // forces nth bit of x to be 1.  all other bits left alone.
            else
                _vData.ActionGroups &= (UInt16)~(1 << n);      // forces nth bit of x to be 0.  all other bits left alone.
        }
        
        /// <summary>
        /// Get the max vessel overheat.
        /// </summary>
        /// <param name="V">Vessel</param>
        /// <returns>Overheat percentage</returns>
        private byte GetMaxOverHeat(Vessel V)
        {
            byte percent = 0;
            double sPercent = 0, iPercent = 0;
            double percentD = 0, percentP = 0;

            foreach (Part p in V.parts)
            {
                //internal temperature
                iPercent = p.temperature / p.maxTemp;
                //skin temperature
                sPercent = p.skinTemperature / p.skinMaxTemp;

                if (iPercent > sPercent)
                    percentP = iPercent;
                else
                    percentP = sPercent;

                if (percentD < percentP)
                    percentD = percentP;
            }

            percent = (byte)Math.Round(percentD * 100);
            return percent;
        }

        /// <summary>
        /// Get ressource information by ressource name.
        /// Name can be
        ///  - XenonGas
        ///  - SolidFuel
        ///  - IntakeAir
        ///  - MonoPropellant
        ///  - ElectricCharge
        ///  - Oxidizer
        ///  - LiquidFuel
        /// 
        /// </summary>
        /// <param name="V">Vessel</param>
        /// <param name="resourceName">Name of the ressource</param>
        /// <returns>Max and actual amount</returns>
        private IOResource GetResourceTotal(Vessel V, string resourceName)
        {
            IOResource R = new IOResource();

            foreach (Part p in V.parts)
            {
                foreach (PartResource pr in p.Resources)
                {
                    if (pr.resourceName.Equals(resourceName))
                    {
                        R.Current += (float)pr.amount;
                        R.Max += (float)pr.maxAmount;

                        break;
                    }
                }
            }

            if (R.Max == 0)
                R.Current = 0;

            return R;
        }
        
        /// <summary>
        /// Resolve planet name to a specific number.
        /// </summary>
        /// <param name="name">Planet name</param>
        /// <returns>Identifier</returns>
        private byte GetSOINumber(string name)
        {
            byte SOI;

            switch (name.ToLower())
            {
                case "sun":
                    SOI = 100;
                    break;
                case "moho":
                    SOI = 110;
                    break;
                case "eve":
                    SOI = 120;
                    break;
                case "gilly":
                    SOI = 121;
                    break;
                case "kerbin":
                    SOI = 130;
                    break;
                case "mun":
                    SOI = 131;
                    break;
                case "minmus":
                    SOI = 132;
                    break;
                case "duna":
                    SOI = 140;
                    break;
                case "ike":
                    SOI = 141;
                    break;
                case "dres":
                    SOI = 150;
                    break;
                case "jool":
                    SOI = 160;
                    break;
                case "laythe":
                    SOI = 161;
                    break;
                case "vall":
                    SOI = 162;
                    break;
                case "tylo":
                    SOI = 163;
                    break;
                case "bop":
                    SOI = 164;
                    break;
                case "pol":
                    SOI = 165;
                    break;
                case "eeloo":
                    SOI = 170;
                    break;
                default:
                    SOI = 0;
                    break;
            }
            return SOI;
        }

        /// <summary>
        /// this recursive stage look up stuff stolen and modified from KOS and others
        /// </summary>
        /// <param name="vessel"></param>
        /// <returns></returns>
        private List<Part> GetListOfActivatedEngines(Vessel vessel)
        {
            var retList = new List<Part>();

            foreach (var part in vessel.Parts)
            {
                foreach (PartModule module in part.Modules)
                {
                    var engineModule = module as ModuleEngines;
                    if (engineModule != null)
                    {
                        if (engineModule.getIgnitionState)
                        {
                            retList.Add(part);
                        }
                    }

                    var engineModuleFx = module as ModuleEnginesFX;
                    if (engineModuleFx != null)
                    {
                        if (engineModuleFx.getIgnitionState)
                        {
                            retList.Add(part);
                        }
                    }
                }
            }

            return retList;
        }

        /// <summary>
        /// Not quite sure what those do. Have to figure out...
        /// </summary>
        /// <param name="resourceName"></param>
        /// <param name="engines"></param>
        /// <returns></returns>
        private double ProspectForResource(String resourceName, List<Part> engines)
        {
            List<Part> visited = new List<Part>();
            double total = 0;

            foreach (var part in engines)
            {
                total += ProspectForResource(resourceName, part, ref visited);
            }

            return total;
        }
        private double ProspectForResource(String resourceName, Part engine)
        {
            List<Part> visited = new List<Part>();

            return ProspectForResource(resourceName, engine, ref visited);
        }
        private double ProspectForResource(String resourceName, Part part, ref List<Part> visited)
        {
            double ret = 0;

            if (visited.Contains(part))
            {
                return 0;
            }

            visited.Add(part);

            foreach (PartResource resource in part.Resources)
            {
                if (resource.resourceName.ToLower() == resourceName.ToLower())
                {
                    ret += resource.amount;
                }
            }

            foreach (AttachNode attachNode in part.attachNodes)
            {
                if (attachNode.attachedPart != null //if there is a part attached here
                    && attachNode.nodeType == AttachNode.NodeType.Stack //and the attached part is stacked (rather than surface mounted)
                    && (attachNode.attachedPart.fuelCrossFeed //and the attached part allows fuel flow
                        )
                    && !(part.NoCrossFeedNodeKey.Length > 0 //and this part does not forbid fuel flow
                         && attachNode.id.Contains(part.NoCrossFeedNodeKey))) // through this particular node
                {


                    ret += ProspectForResource(resourceName, attachNode.attachedPart, ref visited);
                }
            }

            return ret;
        }
        private double ProspectForResourceMax(String resourceName, List<Part> engines)
        {
            List<Part> visited = new List<Part>();
            double total = 0;

            foreach (var part in engines)
            {
                total += ProspectForResourceMax(resourceName, part, ref visited);
            }

            return total;
        }
        private double ProspectForResourceMax(String resourceName, Part engine)
        {
            List<Part> visited = new List<Part>();

            return ProspectForResourceMax(resourceName, engine, ref visited);
        }
        private double ProspectForResourceMax(String resourceName, Part part, ref List<Part> visited)
        {
            double ret = 0;

            if (visited.Contains(part))
            {
                return 0;
            }

            visited.Add(part);

            foreach (PartResource resource in part.Resources)
            {
                if (resource.resourceName.ToLower() == resourceName.ToLower())
                {
                    ret += resource.maxAmount;
                }
            }

            foreach (AttachNode attachNode in part.attachNodes)
            {
                if (attachNode.attachedPart != null //if there is a part attached here
                    && attachNode.nodeType == AttachNode.NodeType.Stack //and the attached part is stacked (rather than surface mounted)
                    && (attachNode.attachedPart.fuelCrossFeed //and the attached part allows fuel flow
                        )
                    && !(part.NoCrossFeedNodeKey.Length > 0 //and this part does not forbid fuel flow
                         && attachNode.id.Contains(part.NoCrossFeedNodeKey))) // through this particular node
                {


                    ret += ProspectForResourceMax(resourceName, attachNode.attachedPart, ref visited);
                }
            }

            return ret;
        }
    }

    /// <summary>
    /// Listener interface for VesselDataInterface
    /// </summary>
    public interface IVesselDataInterfaceListener
    {
        void VesselDataInvalidated(VesselData VData);
    }

    /// <summary>
    /// VesselDataInterface event provider
    /// </summary>
    public abstract class IVesselDataInterface
    {
        private ArrayList listeners = new ArrayList();

        public void AddListener(IVesselDataInterfaceListener l)
        {
            listeners.Add(l);
        }

        public void RemoveListener(IVesselDataInterfaceListener l)
        {
            listeners.Remove(l);
        }

        protected void NotifyInvalidate(VesselData VData)
        {
            foreach (IVesselDataInterfaceListener l in listeners) l.VesselDataInvalidated(VData);
        }
    }

}
