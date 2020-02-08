using KSP.UI.Screens;
using System;
using UnityEngine;
using static KSPEthernetIO.DataPackets;
using static KSPEthernetIO.Utilities;

namespace KSPEthernetIO
{
    /// <summary>
    /// VesselControl from original KSPSerialIO customized for use in separate class.
    /// </summary>
    class VesselControlInterface
    {
        private struct VesselControls
        {
            public Boolean SAS;
            public Boolean RCS;
            public Boolean Lights;
            public Boolean Gear;
            public Boolean Brakes;
            public Boolean Precision;
            public Boolean Abort;
            public Boolean Stage;
            public Boolean OpenMenu;
            public Boolean OpenMap;
            public int UiMode;
            public int CameraMode;
            public int SASMode;
            public int SpeedMode;
            public Boolean[] ControlGroup;
            public float Pitch;
            public float Roll;
            public float Yaw;
            public float TX;
            public float TY;
            public float TZ;
            public float WheelSteer;
            public float Throttle;
            public float WheelThrottle;
        };

        private VesselControls _vControls = new VesselControls();
        private VesselControls _vControlsOld = new VesselControls();
        private Vessel _activeVessel = null;
        private bool _wasSASOn = false;

        /// <summary>
        /// Initialize VesselControlInterface
        /// </summary>
        public VesselControlInterface()
        {
            _vControls.ControlGroup = new Boolean[11];
            _vControlsOld.ControlGroup = new Boolean[11];
        }

        /// <summary>
        /// Called during flight in Update() routine. Continuously updates the active vessel.
        /// </summary>
        public void UpdateActiveVessel()
        {
            //If the current active vessel is not what we were using, we need to remove controls from the old 
            //vessel and attache it to the current one
            if (_activeVessel != null && _activeVessel.id != FlightGlobals.ActiveVessel.id)
            {
                _activeVessel.OnPostAutopilotUpdate -= AxisInput;
            }

            //Change the Active vessel if necessary
            if (_activeVessel == null || _activeVessel.id != FlightGlobals.ActiveVessel.id)
            {
                _activeVessel = FlightGlobals.ActiveVessel;
                if (_activeVessel != null)
                {
                    _activeVessel.OnPostAutopilotUpdate += AxisInput;

                    //sync some inputs on vessel switch
                    _activeVessel.ActionGroups.SetGroup(KSPActionGroup.RCS, _vControls.RCS);
                    _activeVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, _vControls.SAS);
                    _activeVessel.ActionGroups.SetGroup(KSPActionGroup.Light, _vControls.Lights);
                    _activeVessel.ActionGroups.SetGroup(KSPActionGroup.Gear, _vControls.Gear);
                    _activeVessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, _vControls.Brakes);

                }
            }
        }

        /// <summary>
        /// Send new control data to the active vessel.
        /// </summary>
        /// <param name="CPacket">Control data</param>
        public void ControlsReceived(ControlPacket CPacket)
        {

            _vControls.SAS = BitMathByte(CPacket.MainControls, 7);
            _vControls.RCS = BitMathByte(CPacket.MainControls, 6);
            _vControls.Lights = BitMathByte(CPacket.MainControls, 5);
            _vControls.Gear = BitMathByte(CPacket.MainControls, 4);
            _vControls.Brakes = BitMathByte(CPacket.MainControls, 3);
            _vControls.Precision = BitMathByte(CPacket.MainControls, 2);
            _vControls.Abort = BitMathByte(CPacket.MainControls, 1);
            _vControls.Stage = BitMathByte(CPacket.MainControls, 0);
            _vControls.Pitch = (float)CPacket.Pitch / 1000.0F;
            _vControls.Roll = (float)CPacket.Roll / 1000.0F;
            _vControls.Yaw = (float)CPacket.Yaw / 1000.0F;
            _vControls.TX = (float)CPacket.TX / 1000.0F;
            _vControls.TY = (float)CPacket.TY / 1000.0F;
            _vControls.TZ = (float)CPacket.TZ / 1000.0F;
            _vControls.WheelSteer = (float)CPacket.WheelSteer / 1000.0F;
            _vControls.Throttle = (float)CPacket.Throttle / 1000.0F;
            _vControls.WheelThrottle = (float)CPacket.WheelThrottle / 1000.0F;
            _vControls.SASMode = (int)CPacket.NavballSASMode & 0x0F;
            _vControls.SpeedMode = (int)(CPacket.NavballSASMode >> 4);
            _vControls.UiMode = (int)CPacket.Mode & 0x0F;
            _vControls.CameraMode = (int)(CPacket.Mode >> 4);
            _vControls.OpenMenu = BitMathByte(CPacket.AdditionalControlByte1, 0);
            _vControls.OpenMap = BitMathByte(CPacket.AdditionalControlByte1, 1);

            for (int j = 1; j <= 10; j++)
            {
                _vControls.ControlGroup[j] = BitMathUshort(CPacket.ControlGroup, j);
            }


            //if (FlightInputHandler.RCSLock != VControls.RCS)
            if (_vControls.RCS != _vControlsOld.RCS)
            {
                _activeVessel.ActionGroups.SetGroup(KSPActionGroup.RCS, _vControls.RCS);
                _vControlsOld.RCS = _vControls.RCS;
                //ScreenMessages.PostScreenMessage("RCS: " + VControls.RCS.ToString(), 10f, KSPIOScreenStyle);
            }

            //if (ActiveVessel.ctrlState.killRot != VControls.SAS)
            if (_vControls.SAS != _vControlsOld.SAS)
            {
                _activeVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, _vControls.SAS);
                _vControlsOld.SAS = _vControls.SAS;
                //ScreenMessages.PostScreenMessage("SAS: " + VControls.SAS.ToString(), 10f, KSPIOScreenStyle);
            }

            if (_vControls.Lights != _vControlsOld.Lights)
            {
                _activeVessel.ActionGroups.SetGroup(KSPActionGroup.Light, _vControls.Lights);
                _vControlsOld.Lights = _vControls.Lights;
            }

            if (_vControls.Gear != _vControlsOld.Gear)
            {
                _activeVessel.ActionGroups.SetGroup(KSPActionGroup.Gear, _vControls.Gear);
                _vControlsOld.Gear = _vControls.Gear;
            }

            if (_vControls.Brakes != _vControlsOld.Brakes)
            {
                _activeVessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, _vControls.Brakes);
                _vControlsOld.Brakes = _vControls.Brakes;
            }

            if (_vControls.Abort != _vControlsOld.Abort)
            {
                _activeVessel.ActionGroups.SetGroup(KSPActionGroup.Abort, _vControls.Abort);
                _vControlsOld.Abort = _vControls.Abort;
            }

            if (_vControls.Stage != _vControlsOld.Stage)
            {
                if (_vControls.Stage) StageManager.ActivateNextStage();

                _activeVessel.ActionGroups.SetGroup(KSPActionGroup.Stage, _vControls.Stage);
                _vControlsOld.Stage = _vControls.Stage;
            }

            //================ control groups

            if (_vControls.ControlGroup[1] != _vControlsOld.ControlGroup[1])
            {
                _activeVessel.ActionGroups.SetGroup(KSPActionGroup.Custom01, _vControls.ControlGroup[1]);
                _vControlsOld.ControlGroup[1] = _vControls.ControlGroup[1];
            }

            if (_vControls.ControlGroup[2] != _vControlsOld.ControlGroup[2])
            {
                _activeVessel.ActionGroups.SetGroup(KSPActionGroup.Custom02, _vControls.ControlGroup[2]);
                _vControlsOld.ControlGroup[2] = _vControls.ControlGroup[2];
            }

            if (_vControls.ControlGroup[3] != _vControlsOld.ControlGroup[3])
            {
                _activeVessel.ActionGroups.SetGroup(KSPActionGroup.Custom03, _vControls.ControlGroup[3]);
                _vControlsOld.ControlGroup[3] = _vControls.ControlGroup[3];
            }

            if (_vControls.ControlGroup[4] != _vControlsOld.ControlGroup[4])
            {
                _activeVessel.ActionGroups.SetGroup(KSPActionGroup.Custom04, _vControls.ControlGroup[4]);
                _vControlsOld.ControlGroup[4] = _vControls.ControlGroup[4];
            }

            if (_vControls.ControlGroup[5] != _vControlsOld.ControlGroup[5])
            {
                _activeVessel.ActionGroups.SetGroup(KSPActionGroup.Custom05, _vControls.ControlGroup[5]);
                _vControlsOld.ControlGroup[5] = _vControls.ControlGroup[5];
            }

            if (_vControls.ControlGroup[6] != _vControlsOld.ControlGroup[6])
            {
                _activeVessel.ActionGroups.SetGroup(KSPActionGroup.Custom06, _vControls.ControlGroup[6]);
                _vControlsOld.ControlGroup[6] = _vControls.ControlGroup[6];
            }

            if (_vControls.ControlGroup[7] != _vControlsOld.ControlGroup[7])
            {
                _activeVessel.ActionGroups.SetGroup(KSPActionGroup.Custom07, _vControls.ControlGroup[7]);
                _vControlsOld.ControlGroup[7] = _vControls.ControlGroup[7];
            }

            if (_vControls.ControlGroup[8] != _vControlsOld.ControlGroup[8])
            {
                _activeVessel.ActionGroups.SetGroup(KSPActionGroup.Custom08, _vControls.ControlGroup[8]);
                _vControlsOld.ControlGroup[8] = _vControls.ControlGroup[8];
            }

            if (_vControls.ControlGroup[9] != _vControlsOld.ControlGroup[9])
            {
                _activeVessel.ActionGroups.SetGroup(KSPActionGroup.Custom09, _vControls.ControlGroup[9]);
                _vControlsOld.ControlGroup[9] = _vControls.ControlGroup[9];
            }

            if (_vControls.ControlGroup[10] != _vControlsOld.ControlGroup[10])
            {
                _activeVessel.ActionGroups.SetGroup(KSPActionGroup.Custom10, _vControls.ControlGroup[10]);
                _vControlsOld.ControlGroup[10] = _vControls.ControlGroup[10];
            }

            //Set sas mode
            if (_vControls.SASMode != _vControlsOld.SASMode)
            {
                if (_vControls.SASMode != 0 && _vControls.SASMode < 11)
                {
                    if (!_activeVessel.Autopilot.CanSetMode((VesselAutopilot.AutopilotMode)(_vControls.SASMode - 1)))
                    {
                        ScreenMessages.PostScreenMessage("[KSPEthernetIO]: SAS mode " + _vControls.SASMode.ToString() + " not avalible");
                    }
                    else
                    {
                        _activeVessel.Autopilot.SetMode((VesselAutopilot.AutopilotMode)_vControls.SASMode - 1);
                    }
                }
                _vControlsOld.SASMode = _vControls.SASMode;
            }

            //set navball mode
            if (_vControls.SpeedMode != _vControlsOld.SpeedMode)
            {
                if (!((_vControls.SpeedMode == 0) || ((_vControls.SpeedMode == 3) && !TargetExists())))
                {
                    FlightGlobals.SetSpeedMode((FlightGlobals.SpeedDisplayModes)(_vControls.SpeedMode - 1));
                }
                _vControlsOld.SpeedMode = _vControls.SpeedMode;
            }


            if (Math.Abs(_vControls.Pitch) > Settings.SASTol ||
                Math.Abs(_vControls.Roll) > Settings.SASTol ||
                Math.Abs(_vControls.Yaw) > Settings.SASTol)
            {
                if ((_activeVessel.ActionGroups[KSPActionGroup.SAS]) && (_wasSASOn == false))
                {
                    _wasSASOn = true;
                    _activeVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                }
            }
            else
            {
                if (_wasSASOn == true)
                {
                    _wasSASOn = false;
                    _activeVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
                }
            }

            if (_vControlsOld.UiMode != _vControls.UiMode)
            {
                if (FlightUIModeController.Instance != null)
                {
                    switch (_vControls.UiMode)
                    {
                        case 0:
                            FlightUIModeController.Instance.SetMode(FlightUIMode.STAGING);
                            break;
                        case 1:
                            FlightUIModeController.Instance.SetMode(FlightUIMode.DOCKING);
                            break;
                        case 2:
                            FlightUIModeController.Instance.SetMode(FlightUIMode.MAPMODE);
                            break;
                        default:
                            break;
                    }
                    _vControlsOld.UiMode = _vControls.UiMode;
                }
            }

            if (_vControlsOld.CameraMode != _vControls.CameraMode)
            {
                if (FlightCamera.fetch != null)
                {
                    switch (_vControls.CameraMode)
                    {
                        case 0:
                            FlightCamera.fetch.setMode(FlightCamera.Modes.AUTO);
                            break;
                        case 1:
                            FlightCamera.fetch.setMode(FlightCamera.Modes.FREE);
                            break;
                        case 2:
                            FlightCamera.fetch.setMode(FlightCamera.Modes.ORBITAL);
                            break;
                        case 3:
                            FlightCamera.fetch.setMode(FlightCamera.Modes.CHASE);
                            break;
                        case 4:
                            FlightCamera.fetch.setMode(FlightCamera.Modes.LOCKED);
                            break;
                        default:
                            break;
                    }
                    _vControlsOld.CameraMode = _vControls.CameraMode;
                }
            }

            if (_vControlsOld.OpenMenu != _vControls.OpenMenu)
            {
                if (_vControls.OpenMenu) PauseMenu.Display();
                else PauseMenu.Close();
                _vControlsOld.OpenMenu = _vControls.OpenMenu;
            }

            if (_vControlsOld.OpenMap != _vControls.OpenMap)
            {
                if (_vControls.OpenMap) MapView.EnterMapView();
                else MapView.ExitMapView();
                _vControlsOld.OpenMap = _vControls.OpenMap;
            }
        }

        /// <summary>
        /// Ingame callback for the active vessel to update the axis input.
        /// </summary>
        /// <param name="s"></param>
        private void AxisInput(FlightCtrlState s)
        {
            switch (Settings.ThrottleEnable)
            {
                case 1:
                    s.mainThrottle = _vControls.Throttle;
                    break;
                case 2:
                    if (s.mainThrottle == 0)
                    {
                        s.mainThrottle = _vControls.Throttle;
                    }
                    break;
                case 3:
                    if (_vControls.Throttle != 0)
                    {
                        s.mainThrottle = _vControls.Throttle;
                    }
                    break;
                default:
                    break;
            }

            switch (Settings.PitchEnable)
            {
                case 1:
                    s.pitch = _vControls.Pitch;
                    break;
                case 2:
                    if (s.pitch == 0)
                        s.pitch = _vControls.Pitch;
                    break;
                case 3:
                    if (_vControls.Pitch != 0)
                        s.pitch = _vControls.Pitch;
                    break;
                default:
                    break;
            }

            switch (Settings.RollEnable)
            {
                case 1:
                    s.roll = _vControls.Roll;
                    break;
                case 2:
                    if (s.roll == 0)
                        s.roll = _vControls.Roll;
                    break;
                case 3:
                    if (_vControls.Roll != 0)
                        s.roll = _vControls.Roll;
                    break;
                default:
                    break;
            }

            switch (Settings.YawEnable)
            {
                case 1:
                    s.yaw = _vControls.Yaw;
                    break;
                case 2:
                    if (s.yaw == 0)
                        s.yaw = _vControls.Yaw;
                    break;
                case 3:
                    if (_vControls.Yaw != 0)
                        s.yaw = _vControls.Yaw;
                    break;
                default:
                    break;
            }
            /*
            if (ActiveVessel.Autopilot.SAS.lockedMode == true)
            {
            }
            */
            switch (Settings.TXEnable)
            {
                case 1:
                    s.X = _vControls.TX;
                    break;
                case 2:
                    if (s.X == 0)
                        s.X = _vControls.TX;
                    break;
                case 3:
                    if (_vControls.TX != 0)
                        s.X = _vControls.TX;
                    break;
                default:
                    break;
            }

            switch (Settings.TYEnable)
            {
                case 1:
                    s.Y = _vControls.TY;
                    break;
                case 2:
                    if (s.Y == 0)
                        s.Y = _vControls.TY;
                    break;
                case 3:
                    if (_vControls.TY != 0)
                        s.Y = _vControls.TY;
                    break;
                default:
                    break;
            }

            switch (Settings.TZEnable)
            {
                case 1:
                    s.Z = _vControls.TZ;
                    break;
                case 2:
                    if (s.Z == 0)
                        s.Z = _vControls.TZ;
                    break;
                case 3:
                    if (_vControls.TZ != 0)
                        s.Z = _vControls.TZ;
                    break;
                default:
                    break;
            }

            switch (Settings.WheelSteerEnable)
            {
                case 1:
                    s.wheelSteer = _vControls.WheelSteer;
                    break;
                case 2:
                    if (s.wheelSteer == 0)
                    {
                        s.wheelSteer = _vControls.WheelSteer;
                    }
                    break;
                case 3:
                    if (_vControls.WheelSteer != 0)
                    {
                        s.wheelSteer = _vControls.WheelSteer;
                    }
                    break;
                default:
                    break;
            }

            switch (Settings.WheelThrottleEnable)
            {
                case 1:
                    s.wheelThrottle = _vControls.WheelThrottle;
                    break;
                case 2:
                    if (s.wheelThrottle == 0)
                    {
                        s.wheelThrottle = _vControls.WheelThrottle;
                    }
                    break;
                case 3:
                    if (_vControls.WheelThrottle != 0)
                    {
                        s.wheelThrottle = _vControls.WheelThrottle;
                    }
                    break;
                default:
                    break;
            }
        }

    }
}
