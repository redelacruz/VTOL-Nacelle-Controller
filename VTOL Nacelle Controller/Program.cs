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
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // -------------------------
        // Rotor Custom Data strings
        // -------------------------
        private const string reference = "~NC_Reference";
        private const string mirror = "~NC_Mirror";
        private const string copy = "~NC_Copy";
        private const string offset = "~NC_Offset";
        private const string upperLimit = "~NC_UpperLimit";
        private const string lowerLimit = "~NC_LowerLimit";
        private const string velocity = "~NC_Velocity";

        // -- DO NOT EDIT BELOW THIS LINE --

        private const int DEBUG_SCROLLING_LINE_COUNT = 25;
        private const int TIME_TILL_UPDATE = 1; // in seconds

        // Managed stators
        private static NacelleStator referenceStator;
        private static List<NacelleStator> slaveStators = new List<NacelleStator>(); // all the slave stators

        // State machines
        private static List<IEnumerator<bool>> stateMachines = new List<IEnumerator<bool>>();
        private static List<Func<IEnumerator<bool>>> stateMachineProviders = new List<Func<IEnumerator<bool>>>();
        private static int maxStateMachines = 0;

        // Stuff used for updating the info panel
        private static List<string> debugAdditions = new List<string>();
        private static List<string> debugPersistent = new List<string>();
        private static int updateCounter = 0;
        private static string referenceStatorName;
        private static int mirroredStators = 0;
        private static int copiedStators = 0;

        public Program()
        {
            GetBlocks();
        }

        public void Save()
        {
            // TODO Save variables like managedReference to the Storage field
            // ^^ Basically, the variables that would be important to recovery
            //    in case of a crash.
        }

        public void Main(string argument)
        {
            ProcessArguments(argument);

            //UpdateStators(); // TODO New method for updating the stators

            RunStateMachines();

            UpdateInfo();
        }

        /// <summary>
        /// Process arguments passed to the script.
        /// </summary>
        private void ProcessArguments(string argument)
        {
            switch (argument)
            {
                case "update":
                    GetBlocks();
                    return;

                case "clearLog":
                    debugAdditions.Clear();
                    return;
            }

            // Handle angles used to turn the reference stator by script
            float angle;
            if (float.TryParse(argument, out angle))
            {
                referenceStator.Update().TargetAngle(angle).Commit();
                stateMachineProviders.Add(referenceStator.RunManagedMovement());

                foreach(NacelleStator slave in slaveStators)
                {
                    slave.Update().TargetAngle(angle).Commit();
                    stateMachineProviders.Add(slave.RunManagedMovement());
                }
            }
        }

        /// <summary>
        /// Runs a list of state machines.
        /// </summary>
        private void RunStateMachines()
        {
            // Modification of Malware's state machine code
            // For more about yielding enumerator state machines in Space Engineers, see:
            // https://github.com/malware-dev/MDK-SE/wiki/Advanced:-Easy-and-Powerful-State-Machine-Using-yield-return

            if (stateMachines.Count == 0) return;

            // Iterate through the list of state machines
            for (int i = stateMachines.Count - 1; i >= 0; i--)
            {
                IEnumerator<bool> sm = stateMachines[i];

                // If there are no more instructions, we stop and release the state machine.
                if (!sm.MoveNext())
                {
                    sm.Dispose();
                    stateMachines.RemoveAt(i);
                }
            }

            // If there are new state machine providers, iterate through them so we can start their
            // state machines in the next cycle
            if (stateMachineProviders.Count == 0) return;

            // Check if adding the new state machines will violate the max and remove old state machines, if necessary
            while (stateMachines.Count + stateMachineProviders.Count >= maxStateMachines)
            {
                stateMachines[0].Dispose();
                stateMachines.RemoveAt(0);
            }

            // Iterate through the list of state machine providers
            foreach (Func<IEnumerator<bool>> provider in stateMachineProviders) stateMachines.Add(provider());
        }

        /// <summary>
        /// Update the programmable block's information panel.
        /// </summary>
        private void UpdateInfo()
        {
            if (updateCounter == 4) updateCounter = 0;

            // Create running symbol
            string runningSymbol = "";

            switch (updateCounter)
            {
                case 0:
                    runningSymbol = "--";
                    break;
                case 1:
                    runningSymbol = "\\";
                    break;
                case 2:
                    runningSymbol = "|";
                    break;
                case 3:
                    runningSymbol = "/";
                    break;
            }

            // Build Echo string
            StringBuilder updateString = new StringBuilder();

            updateString.AppendLine("VTOL Nacelle Controller");
            updateString.Append("Running: ").AppendLine(runningSymbol).AppendLine();

            if (referenceStatorName != null)
            {
                updateString.Append("Reference Rotor: ").AppendLine(referenceStatorName);
                updateString.Append("Mirrored Rotors: ").AppendLine(mirroredStators.ToString());
                updateString.Append("Copied Rotors:   ").AppendLine(copiedStators.ToString());
                updateString.AppendLine();
            }

            // Append debug lines
            foreach (string line in Log("")) updateString.AppendLine(line);

            // Output string the info panel
            Echo(updateString.ToString());

            if (Runtime.TimeSinceLastRun.Seconds >= 1) updateCounter++;
        }

        /// <summary>
        /// Just an ordinary logging method. Pass "" as message for output. Use isPersistent
        /// if the message is updated each cycle. Otherwise, messages are displayed as scrolling
        /// text on the info panel.
        /// </summary>
        private Stack<string> Log(string message = "", bool isPersistent = false)
        {
            if (message == "")
            {
                List<string> list = new List<string>(debugAdditions);
                list.AddRange(debugPersistent);
                debugPersistent.Clear();
                return new Stack<string>(list);
            }

            if (isPersistent)
            {
                debugPersistent.Add(message);
                return null;
            }

            if (debugAdditions.Count > DEBUG_SCROLLING_LINE_COUNT) debugAdditions.RemoveAt(0);
            debugAdditions.Add(message);
            return null;
        }

        /// <summary>
        /// Gets all managed stator blocks.
        /// </summary>
        private void GetBlocks()
        {
            // Reset static variables with stator data
            referenceStator = null;
            slaveStators.Clear();
            maxStateMachines = 0;
            mirroredStators = 0;
            copiedStators = 0;
            referenceStatorName = "";

            // Prepare a list of stators
            List<IMyMotorStator> statorList = new List<IMyMotorStator>();

            // Get all stator blocks
            GridTerminalSystem.GetBlocksOfType(statorList);

            // Filter stator blocks
            foreach (IMyMotorStator stator in statorList)
            {
                NacelleStator managedStator;

                // Read Custom Data
                string val = stator.CustomData.ToLower();

                // Check if stator is configured
                if (val.Contains(reference.ToLower()))
                {
                    managedStator = new NacelleStator(stator);
                    referenceStator = managedStator;
                }
                else if (val.Contains(mirror.ToLower()))
                {
                    managedStator = new NacelleStator(stator, true);
                    slaveStators.Add(managedStator);
                    mirroredStators++;
                }
                else if (val.Contains(copy.ToLower()))
                {
                    managedStator = new NacelleStator(stator);
                    slaveStators.Add(managedStator);
                    copiedStators++;
                }
                else continue;

                maxStateMachines++;

                StatorProperties properties = managedStator.Update();

                // Check if stator has an offset
                try { properties.Offset(float.Parse(IsolateSetValue(val, offset))); }
                catch { }

                // Check if stator has a lower limit
                try { properties.LowerLimit(float.Parse(IsolateSetValue(val, lowerLimit))); }
                catch { }

                // Check if stator has an upper limit
                try { properties.UpperLimit(float.Parse(IsolateSetValue(val, upperLimit))); }
                catch { }

                // Check if stator has a velocity
                try { properties.Velocity(float.Parse(IsolateSetValue(val, velocity))); }
                catch { }
            }

            if (referenceStator == null)
            {
                Log("ERROR: No reference stator.");
                return;
            }

            // Get the reference stator's name (for info purposes)
            referenceStatorName = referenceStator.stator.CustomName;
        }

        /// <summary>
        /// Returns the value of a single-line key/value pair from a multiline string.
        /// </summary>
        private string IsolateSetValue(string text, string key)
        {
            int startLine = text.IndexOf(key.ToLower());
            int endLine = text.IndexOf(key.ToLower(), startLine);
            return text.Substring(startLine, endLine - startLine).Trim();
        }

        /// <summary>
        /// A single stator used to turn a nacelle.
        /// </summary>
        private class NacelleStator
        {
            public IMyMotorStator stator { get; }
            public IMyMotorStator reference { get; private set; }
            private bool propertyLock = true;
            private StatorProperties properties;

            // TODO [Maybe] Implement isRunning

            public NacelleStator(IMyMotorStator stator, bool isMirrored = false)
            {
                this.stator = stator;
                properties = new StatorProperties(PropertyLock, isMirrored);
            }

            public void SetReference(IMyMotorStator reference)
            {
                this.reference = reference;
            }

            /// <summary>
            /// Used to lock the properties or enforce the lock.
            /// </summary>
            private bool PropertyLock(bool isLocked)
            {
                if (isLocked)
                {
                    propertyLock = isLocked;
                    Commit();
                }

                if (isLocked == propertyLock) return true;
                else return false;
            }

            /// <summary>
            /// Get the stator properties.
            /// </summary>
            public StatorProperties getProperties()
            {
                return properties;
            }

            /// <summary>
            /// Begin updating the stator properties.
            /// </summary>
            public StatorProperties Update()
            {
                if (PropertyLock(false)) throw new Exception("Update on pending transaction.");

                propertyLock = false;
                return properties;
            }

            /// <summary>
            /// Commit changes to the stator properties.
            /// </summary>
            private void Commit()
            {
                // TODO [Pending API change] Use stator.UpperLimit and stator.LowerLimit instead of SetValueFloat
                // TODO [With above] Check if we can set both upper and lower limits without the extra third call
                stator.SetValueFloat("UpperLimit", properties.upperLimitDeg);
                stator.SetValueFloat("LowerLimit", properties.lowerLimitDeg);
                stator.SetValueFloat("UpperLimit", properties.upperLimitDeg);
                stator.TargetVelocity = properties.velocityRads;

                ClampAndOffset();
            }

            /// <summary>
            /// Clamp the stator to the maximums in its properties and apply offsets, if necessary.
            /// </summary>
            private void ClampAndOffset()
            {
                // TODO Implement offsets

                // Clamp
                if (stator.LowerLimit <= properties.lowerLimitRad)
                {
                    stator.SetValueFloat("LowerLimit", properties.lowerLimitDeg);
                }

                if (stator.UpperLimit >= properties.upperLimitRad)
                {
                    stator.SetValueFloat("UpperLimit", properties.upperLimitDeg);
                }

                // TODO Possibly make velocity proportional by some value to the target stator
                if (Math.Abs(stator.TargetVelocity) >= Math.Abs(properties.velocityRPM))
                {
                    stator.TargetVelocity = Math.Abs(properties.velocityRads) * Math.Sign(stator.TargetVelocity);
                }
            }

            /// <summary>
            /// Runs the stator to match the target angle in its properties.
            /// </summary>
            public Func<IEnumerator<bool>> RunManagedMovement()
            {
                int rotationAngle = 1;

                float currentAngle = StatorProperties.RadToDeg(stator.Angle);

                // Get the shortest direction of travel
                if ((properties.targetAngleDeg - currentAngle + 360) % 360 > 180) rotationAngle = -1;

                // Check if the shortest direction intersects the limits
                // Normalize the angles
                float normCurrentAngle = ((StatorProperties.RadToDeg(stator.Angle) % 360) + 360) % 360;
                float normTargetAngle = ((properties.targetAngleDeg % 360) + 360) % 360;
                float normUpperLimit = ((properties.upperLimitDeg % 360) + 360) % 360;
                float normLowerLimit = ((properties.lowerLimitDeg % 360) + 360) % 360;

                // Check for segment intersections
                if (Intersect(normCurrentAngle, normUpperLimit, normLowerLimit) ||
                    Intersect(normTargetAngle, normUpperLimit, normLowerLimit) ||
                    Intersect(normUpperLimit, normCurrentAngle, normTargetAngle) ||
                    Intersect(normLowerLimit, normCurrentAngle, normTargetAngle))
                {
                    rotationAngle = rotationAngle * -1;
                }

                // Apply the rotation angle
                stator.TargetVelocity = properties.velocityRads * rotationAngle;

                // TODO Set a temporary limit (side closest to target) to avoid overshoots
                // Old limits are reset after the runner completes using Commit(), so above is safe
                
                // Turn the stator
                stator.SafetyLock = false;
                stator.Enabled = true;

                return Runner;
            }

            /// <summary>
            /// Moves the stator dynamically. Used when following a reference stator that isn't turned by the
            /// script.
            /// </summary>
            public void RunDynamicMovement()
            {
                // TODO Write code for dynamically following a reference stator
            }

            /// <summary>
            /// State-machine runner used to poll a running stator and perform closing actions when completed.
            /// </summary>
            private IEnumerator<bool> Runner()
            {
                while (!CompareTargetAngle()) yield return true;

                // Turn off the stator and re-engage the safety lock after the run
                stator.Enabled = false;
                stator.SafetyLock = true;
                
                // Recommit properties in case they were changed during the run
                Commit();
                
                yield return false;
            }

            /// <summary>
            /// Checks if a point lies within the endpoints of a segment.
            /// </summary>
            private bool Intersect(float point, float segmentEnd1, float segmentEnd2)
            {
                if (segmentEnd1 > segmentEnd2)
                {
                    if (point > segmentEnd1 || point < segmentEnd2) return true;
                }
                else
                {
                    if (point > segmentEnd1 && point < segmentEnd2) return true;
                }
                return false;
            }

            /// <summary>
            /// Compares the current angle of the stator to the target angle. Returns true if there is
            /// no target angle set. (Hopefully temporary if Keen fixes the mismatched units between
            /// getters and setters.)
            /// </summary>
            public bool CompareTargetAngle()
            {
                if (properties.targetAngleRad == StatorProperties.INFINITE_ANGLE_RADIANS) return true;
                if (stator.Angle == properties.targetAngleRad ||
                    StatorProperties.RadToDeg(stator.Angle) == properties.targetAngleDeg) return true;
                return false;
            }
        }

        /// <summary>
        /// Stores properties of a stator.
        /// </summary>
        private class StatorProperties
        {
            public const float INFINITE_ANGLE_RADIANS = 6.30064f;
            public const float INFINITE_ANGLE_DEGREES = 361f;

            // Stored in radians
            private static float upperLimit = INFINITE_ANGLE_RADIANS;
            private static float lowerLimit = -INFINITE_ANGLE_RADIANS;
            private static float targetAngle = INFINITE_ANGLE_RADIANS; // infinite target angle (no target angle set)
            private static float offset = 0;

            // Stored in radians per second
            private static float velocity = 0; // 0 means no maximum velocity is set

            // Non-SI units
            public float upperLimitDeg { get { return RadToDeg(upperLimit); } }
            public float lowerLimitDeg { get { return RadToDeg(lowerLimit); } }
            public float targetAngleDeg { get { return RadToDeg(targetAngle); } }
            public float offsetDeg { get { return RadToDeg(offset); } }
            public float velocityRPM { get { return RadsToRpm(velocity); } }

            // SI units
            public float upperLimitRad { get { return upperLimit; } }
            public float lowerLimitRad { get { return lowerLimit; } }
            public float targetAngleRad { get { return targetAngle; } }
            public float offsetRad { get { return offset; } }
            public float velocityRads { get { return velocity; } }

            // Used during transactions
            private static float uLimitTemp = upperLimit;
            private static float lLimitTemp = lowerLimit;
            private static float angleTemp = targetAngle;
            private static float offsetTemp = offset;
            private static float velocityTemp = velocity;
            private Func<bool, bool> propertyLock;
            public bool isMirrored { get; private set; }

            public StatorProperties(Func<bool, bool> propertyLock, bool isMirrored = false)
            {
                this.propertyLock = propertyLock;
                this.isMirrored = isMirrored;
            }

            /// <summary>
            /// Set the upper limit.
            /// </summary>
            public StatorProperties UpperLimit(float value, bool isRadians = false)
            {
                if (propertyLock(false)) throw new Exception("Attempt to update upper limit without transaction.");

                uLimitTemp = isRadians ? value : DegToRad(value);
                if (isMirrored) uLimitTemp = -uLimitTemp;
                return this;
            }

            /// <summary>
            /// Set the lower limit.
            /// </summary>
            public StatorProperties LowerLimit(float value, bool isRadians = false)
            {
                if (propertyLock(false)) throw new Exception("Attempt to update lower limit without transaction.");

                lLimitTemp = isRadians ? value : DegToRad(value);
                if (isMirrored) lLimitTemp = -lLimitTemp;
                return this;
            }

            /// <summary>
            /// Set the target angle.
            /// </summary>
            public StatorProperties TargetAngle(float value, bool isRadians = false)
            {
                if (propertyLock(false)) throw new Exception("Attempt to update target angle without transaction.");

                angleTemp = isRadians ? value : DegToRad(value);
                if (isMirrored) angleTemp = -angleTemp;
                return this;
            }

            /// <summary>
            /// Set the offset angle.
            /// </summary>
            public StatorProperties Offset(float value, bool isRadians = false)
            {
                if (propertyLock(false)) throw new Exception("Attempt to update offset without transaction.");

                offsetTemp = isRadians ? value : DegToRad(value);
                if (isMirrored) offsetTemp = -offsetTemp;
                return this;
            }

            /// <summary>
            /// Set the stator velocity.
            /// </summary>
            public StatorProperties Velocity(float value, bool isRads = false)
            {
                if (propertyLock(false)) throw new Exception("Attempt to update velocity without transaction.");

                velocityTemp = Math.Abs(isRads ? value : RpmToRads(value));
                return this;
            }

            /// <summary>
            /// Reset the target angle to the default.
            /// </summary>
            public StatorProperties ResetTargetAngle()
            {
                if (propertyLock(false)) throw new Exception("Attempt to reset target angle without transaction.");

                angleTemp = INFINITE_ANGLE_RADIANS;
                return this;
            }

            /// <summary>
            /// Commit the transaction.
            /// </summary>
            public void Commit()
            {
                if (propertyLock(false)) throw new Exception("No pending transaction to commit.");

                ConstrainTargetAngle();

                upperLimit = uLimitTemp;
                lowerLimit = lLimitTemp;
                targetAngle = angleTemp;
                offset = offsetTemp;
                velocity = velocityTemp;

                propertyLock(true);
            }

            /// <summary>
            ///  Cancel a pending transaction.
            /// </summary>
            public void CancelTransaction()
            {
                if (propertyLock(false)) throw new Exception("No pending transaction to cancel.");

                // Match temps with stored values
                uLimitTemp = upperLimit;
                lLimitTemp = lowerLimit;
                angleTemp = targetAngle;
                offsetTemp = offset;
                velocityTemp = velocity;

                propertyLock(true);
            }

            /// <summary>
            /// Constrains the target angle, if set, to the upper and lower limits.
            /// </summary>
            private void ConstrainTargetAngle()
            {
                if (upperLimit == INFINITE_ANGLE_RADIANS && lowerLimit == INFINITE_ANGLE_RADIANS) return;
                if (angleTemp <= lLimitTemp) angleTemp = lLimitTemp;
                if (angleTemp >= uLimitTemp) angleTemp = uLimitTemp;
            }

            public static float RpmToRads(float RPM)
            {
                return ((2 * (float)Math.PI) / 60) * RPM;
            }

            public static float RadsToRpm(float rads)
            {
                return (60 / (2 * (float)Math.PI)) * rads;
            }

            public static float RadToDeg(float radians)
            {
                return radians * (180 / (float)Math.PI);
            }

            public static float DegToRad(float degrees)
            {
                return degrees * ((float)Math.PI / 180);
            }
        }
    }
}