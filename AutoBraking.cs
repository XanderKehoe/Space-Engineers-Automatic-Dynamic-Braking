using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {

        //TODO:
        //Change from angle based error to distance from vector based error.

        //INSTRUCTIONS:
        //--- Setup ------------------------------------------
        //Step #1: Place forward facing camera on ship nose
        //and match its name to 'nameOfCamera' or vise versa.
        //Step #2: Place forward facing remote control anywhere
        //and match its name to 'nameOfRC' or vise versa.
        //Step #3 (Optional): Place programmable block on hotbar
        //and set to run with argument "RUN" for quick access.
        //---------------------------------------------------
        //--- Use -------------------------------------------
        //Step #1: Point at desired location.
        //Step #2: Run script from terminal or from hotbar with argument "RUN".
        //Step #3 (Optional) To abort, run terminal with argument "ABORT"
        //or turn off the autopilot from the remote control.

        //--- CHANGABLE VARIABLES --------------------------

        String nameOfRC = "rc";
        String nameOfCamera = "cam";

        float rayCastDistance = 30000; //Maximum Scanning Distance
        const int finalDistFromTarget = 200; //How far to put adjust target coordinates

        const float maxSpeed = 100; //Set maximum desired speed of ship, do NOT set higher than server allows

        //---DO NOT CHANGE BELOW THIS LINE------------------

        IMyRemoteControl rc;
        IMyCameraBlock cam;

        MyDetectedEntityInfo detectedInfo;
        Vector3D coordinate;

        double backwardsAcceleration = 0;
        List<IMyThrust> thrusters = new List<IMyThrust>();
        List<IMyThrust> backwardThrusters = new List<IMyThrust>();

        Vector3D prevPosition = Vector3D.Zero;

        Boolean travelling = false;
        Boolean blindMode = false;
        int blindCounter = 0;

        public Program()
        {
            rc = GridTerminalSystem.GetBlockWithName(nameOfRC) as IMyRemoteControl; 
            rc.SpeedLimit = maxSpeed;
            cam = GridTerminalSystem.GetBlockWithName(nameOfCamera) as IMyCameraBlock;
            cam.EnableRaycast = true;

            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            Echo("Setup Complete");

            SetupThrusters();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            string[] argInfo = argument.Split(new string[] { "," }, StringSplitOptions.None);
            if (argument == "RUN")
            {
                //Check if can scan, and scan if can.
                if (cam.CanScan(rayCastDistance))
                    detectedInfo = cam.Raycast(rayCastDistance);
                else
                    Echo("Can't scan yet!");

                Echo("INITIATING");

                coordinate = Vector3D.Zero; //initating to zero value.

                Boolean found = false;
                if (detectedInfo.HitPosition != null)
                {
                    coordinate = detectedInfo.HitPosition.Value;
                    found = true;
                }

                if (found)
                {
                    Vector3D currentCoords = rc.GetPosition();

                    //creating unit vector
                    double denominator = Math.Sqrt(Math.Pow(coordinate.X - currentCoords.X, 2)
                        + Math.Pow(coordinate.Y - currentCoords.Y, 2)
                        + Math.Pow(coordinate.Z - currentCoords.Z, 2));
                    double xMultiplier = (coordinate.X - currentCoords.X) / denominator;
                    double yMultiplier = (coordinate.Y - currentCoords.Y) / denominator;
                    double zMultiplier = (coordinate.Z - currentCoords.Z) / denominator;

                    //manipulating target coordinate with unit vector
                    coordinate.X -= finalDistFromTarget * xMultiplier;
                    coordinate.Y -= finalDistFromTarget * yMultiplier;
                    coordinate.Z -= finalDistFromTarget * zMultiplier;

                    //Setting up backward thrusters list
                    backwardThrusters = new List<IMyThrust>();

                    //Obtaining each thruster pointing backward
                    foreach (IMyThrust thruster in thrusters)
                        if (Base6Directions.GetFlippedDirection(rc.Orientation.Forward) == Base6Directions.GetFlippedDirection(thruster.Orientation.Forward))
                            backwardThrusters.Add(thruster);
                    
                    //Obtaining max backward acceleration
                    MyShipMass myShipMass = rc.CalculateShipMass();
                    backwardsAcceleration = CalculateAcceleration(myShipMass.TotalMass, backwardThrusters);

                    //autopilot settings
                    rc.ClearWaypoints();
                    rc.AddWaypoint(coordinate, "CAPTXAN'S SCRIPT COORDINATE");
                    rc.SetAutoPilotEnabled(true);
                    rc.SetCollisionAvoidance(false);
                    rc.SetDockingMode(false); //CHANGE??? or dont?
                    rc.FlightMode = FlightMode.OneWay;
                    rc.Direction = Base6Directions.Direction.Forward;
                    blindMode = false;
                }
            }
            else if (argInfo[0] == "blind".ToLower()) 
            {

                int dist = 0;
                Boolean passed = Int32.TryParse(argInfo[1], out dist);

                if (passed)
                {
                    Vector3D dir = rc.WorldMatrix.Forward;
                    coordinate = rc.GetPosition();
                    coordinate.X += dir.X * dist;
                    coordinate.Y += dir.Y * dist;
                    coordinate.Z += dir.Z * dist;

                    Vector3D currentCoords = rc.GetPosition();

                    //Setting up backward thrusters list
                    backwardThrusters = new List<IMyThrust>();

                    //Obtaining each thruster pointing backward
                    foreach (IMyThrust thruster in thrusters)
                        if (Base6Directions.GetFlippedDirection(rc.Orientation.Forward) == Base6Directions.GetFlippedDirection(thruster.Orientation.Forward))
                            backwardThrusters.Add(thruster);

                    //Obtaining max backward acceleration
                    MyShipMass myShipMass = rc.CalculateShipMass();
                    backwardsAcceleration = CalculateAcceleration(myShipMass.TotalMass, backwardThrusters);

                    //autopilot settings
                    rc.ClearWaypoints();
                    rc.AddWaypoint(coordinate, "CAPTXAN'S SCRIPT COORDINATE");
                    rc.SetAutoPilotEnabled(true);
                    rc.SetCollisionAvoidance(false);
                    rc.SetDockingMode(false); //CHANGE??? or dont?
                    rc.FlightMode = FlightMode.OneWay;
                    rc.Direction = Base6Directions.Direction.Forward;
                    blindMode = true;
                    blindCounter = 0;
                }
                else
                    Echo("2nd parameter is not a number!");
            }
            else
            {
                //User Feedback
                if (!cam.CanScan(rayCastDistance))
                {
                    float percentage = ((cam.TimeUntilScan(rayCastDistance) / 1000) / (rayCastDistance / 2000));
                    percentage = (1 - percentage)*100;
                    Echo("Raycast is recharging " + percentage + "%");
                    if (!cam.EnableRaycast)
                        cam.EnableRaycast = true;
                }
                else
                {
                    Echo("Ready to Scan");
                    cam.EnableRaycast = false;
                }

                //Travelling CHANGE HERE FOR ENABLE / DISABLE AUTOPILOT
                if (rc.IsAutoPilotEnabled)
                {
                    travelling = true;
                    double currentDistanceFromTarget = Vector3D.Distance(coordinate, rc.GetPosition());
                    Echo("Travelling, ETA: " + (int)(currentDistanceFromTarget / rc.GetShipSpeed())+"s");

                    //Calculating stopping distance to determine if thrusters need to be enabled.
                    Echo("Current Speed: " + (int)rc.GetShipSpeed() + "m/s");
                    Echo("Ship Speed Limit: " + rc.SpeedLimit + "m/s");
                    if (rc.GetShipSpeed() > rc.SpeedLimit - 1) //If ship at max speed
                    {
                        Vector3D currentTrajectory = Vector3D.Normalize(rc.GetPosition() - prevPosition);
                        prevPosition = rc.GetPosition();
                        Vector3D calculatedTrajectory = Vector3D.Normalize(rc.GetPosition() - coordinate);

                        double accuracyAmount;
                        if (currentDistanceFromTarget > 15000)
                            accuracyAmount = .99999;
                        else if (currentDistanceFromTarget > 5000)
                            accuracyAmount = .9999;
                        else
                            accuracyAmount = .999;

                        if (currentDistanceFromTarget * .90 > (Math.Pow(rc.GetShipSpeed(), 2) / (2 * backwardsAcceleration)) 
                        && Math.Abs(currentTrajectory.Dot(calculatedTrajectory)) > accuracyAmount)
                            foreach (IMyThrust thruster in thrusters)
                                thruster.ApplyAction("OnOff_Off");
                        else //Curr < stopp
                            foreach (IMyThrust thruster in thrusters)
                                thruster.ApplyAction("OnOff_On");
                    }

                    Echo("Blind Mode: " + blindMode);

                    if (blindMode) {
                        Echo("Blind Counter: " + blindCounter);
                        Echo("Coll Avoid: " + rc.);
                        if (cam.CanScan(((Math.Pow(rc.GetShipSpeed(), 2) / (2 * backwardsAcceleration)) * 2))){
                            detectedInfo = cam.Raycast((Math.Pow(maxSpeed, 2) / (2 * backwardsAcceleration)) * 2);
                            if (detectedInfo.HitPosition != null)
                            {
                                rc.SpeedLimit = 3;
                                rc.SetCollisionAvoidance(true);
                                blindCounter = 0;
                            }
                            else
                            {
                                if (blindCounter > 500)
                                {
                                    rc.SpeedLimit = maxSpeed;
                                    rc.SetCollisionAvoidance(false);
                                    blindCounter = 0;
                                }
                                else
                                    blindCounter++;
                            }
                            
                        }
                    }
                }
                else if (travelling)
                {
                    foreach (IMyThrust thruster in thrusters)
                        thruster.ApplyAction("OnOff_On");
                    travelling = false;
                    blindMode = false;
                }
            }
            //Additional Arugment Commands
            if (argument == "ABORT") {
                rc.SetAutoPilotEnabled(false);
                rc.DampenersOverride = true;
            }
        }

        void SetupThrusters()
        {
            //add all backward facing thrusters to backwardThruster list.
            thrusters = new List<IMyThrust>();
            GridTerminalSystem.GetBlocksOfType(thrusters);
            foreach (IMyThrust thruster in thrusters)
                if (Base6Directions.GetFlippedDirection(rc.Orientation.Forward) == Base6Directions.GetFlippedDirection(thruster.Orientation.Forward))
                    backwardThrusters.Add(thruster);
        }

        double CalculateBrakingDistance(double acceleration, double velocity) {
            double brakingDistance = Math.Pow(velocity, 2) / (2 * acceleration);
            return brakingDistance;
        }

        double CalculateAcceleration(float mass, List<IMyThrust> thrusters) {
            double totalForce = 0;
            foreach (IMyThrust thruster in thrusters)
                totalForce += thruster.MaxEffectiveThrust;

            Echo("Total Force: " + totalForce);
            Echo("Mass: " + mass);
            double acceleration = totalForce / mass;
            return acceleration;
        }

        /*Boolean boolCheckAngleTolerance(Vector3D currentVec, Vector3D desiredVec, float toleranceAngle)
        {
            //Checks if current trajectory matches desired trajectory +- the tolerance angle
            double toleranceCosine = Math.Cos(toleranceAngle);
            var dot = Vector3D.Dot(currentVec, desiredVec);
            return (dot * dot > toleranceCosine * toleranceCosine * currentVec.LengthSquared() * desiredVec.LengthSquared());
            //Thanks Whiplash141!!!
        }*/
    }
}
