namespace IngameScript
{
    using Sandbox.Game.EntityComponents;
    using Sandbox.ModAPI.Ingame;
    using Sandbox.ModAPI.Interfaces;
    using SpaceEngineers.Game.ModAPI.Ingame;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using VRage;
    using VRage.Collections;
    using VRage.Game;
    using VRage.Game.Components;
    using VRage.Game.GUI.TextPanel;
    using VRage.Game.ModAPI.Ingame;
    using VRage.Game.ModAPI.Ingame.Utilities;
    using VRage.Game.ObjectBuilders.Definitions;
    using VRageMath;

    /// <summary>
    /// Program class wrapping Airlock class.
    /// </summary>
    partial class Program
    {
        /// <summary>
        /// Airlock utility class for handling the functioning of an airlock.
        /// </summary>
        public class Airlock
        {
            /// <summary>
            /// Airlock Status.
            /// </summary>
            public enum Status
            {
                /// <summary>
                /// Airlock is idling.
                /// </summary>
                Idle,

                /// <summary>
                /// Airlock is cycling.
                /// </summary>
                Cycling,

                /// <summary>
                /// Airlock cycle has completed.
                /// </summary>
                Complete
            }

            /// <summary>
            /// Grid Search delegate.
            /// </summary>
            private readonly Search search;

            /// <summary>
            /// Programmable block running the script.
            /// </summary>
            private readonly IMyProgrammableBlock Me;

            /// <summary>
            /// Action to start the update timer.
            /// </summary>
            private readonly Action startTimer;

            /// <summary>
            /// List of terminal blocks.
            /// </summary>
            private readonly List<IMyTerminalBlock> TerminalBlocks = new List<IMyTerminalBlock>();

            /// <summary>
            /// List of all doors.
            /// </summary>
            private readonly List<IMyDoor> AllDoors = new List<IMyDoor>();

            /// <summary>
            /// List of interior doors.
            /// </summary>
            private readonly List<IMyDoor> InteriorDoors = new List<IMyDoor>();

            /// <summary>
            /// List of exterior doors.
            /// </summary>
            private readonly List<IMyDoor> ExteriorDoors = new List<IMyDoor>();

            /// <summary>
            /// List of air vents.
            /// </summary>
            private readonly List<IMyAirVent> AirVents = new List<IMyAirVent>();

            /// <summary>
            /// List of oxygen tanks.
            /// </summary>
            private readonly List<IMyGasTank> OxygenTanks = new List<IMyGasTank>();

            /// <summary>
            /// List of Cycling lights.
            /// </summary>
            private readonly List<IMyLightingBlock> CycleLights = new List<IMyLightingBlock>();

            /// <summary>
            /// List of interior lights.
            /// </summary>
            private readonly List<IMyLightingBlock> InteriorLights = new List<IMyLightingBlock>();

            /// <summary>
            /// List of exterior lights.
            /// </summary>
            private readonly List<IMyLightingBlock> ExteriorLights = new List<IMyLightingBlock>();

            /// <summary>
            /// Search term.
            /// </summary>
            private string tag;

            /// <summary>
            /// Airlock settings.
            /// </summary>
            private Settings settings;

            /// <summary>
            /// State machine for handling what action to run.
            /// </summary>
            private AirlockStateMachine stateMachine;

            /// <summary>
            /// Initializes a new instance of the airlock.
            /// </summary>
            /// <param name="tag">Search tag.</param>
            /// <param name="me">Programmable block.</param>
            /// <param name="settings">Settings class.</param>
            /// <param name="search">Search delegate</param>
            /// <param name="startTimer">Start timer action.</param>
            public Airlock(
                string tag,
                IMyProgrammableBlock me,
                Settings settings,
                Search search,
                Action startTimer)
            {
                this.Me = me;
                this.settings = settings;
                this.Name = tag;
                this.tag = $"{this.settings.TagWrapper[0]}{tag}{this.settings.TagWrapper[1]}";
                this.search = search;
                this.startTimer = startTimer;
                this.Initialize();
            }

            public string Name { get; private set; }

            /// <summary>
            /// Gets a value indicating whether the doors are closed.
            /// </summary>
            private bool DoorsAreClosed
            {
                get
                {
                    bool closed = true;
                    foreach(IMyDoor door in this.AllDoors)
                    {
                        if (!door.Closed)
                        {
                            closed = false;
                            break;
                        }
                    }

                    return closed;
                }
            }

            /// <summary>
            /// Gets a value indicating whether the room is depressurized.
            /// </summary>
            private bool IsDepressurized
            {
                get
                {
                    bool isDepressurized = true;
                    foreach (IMyAirVent vent in this.AirVents)
                    {

                        if (vent.Status != VentStatus.Depressurized)
                        {
                            // Vents sometimes get stuck in Depressurizing even after they have depressurized the room.
                            // Oxygen level is a float, so we just check for under 1% oxygen
                            if (vent.Status == VentStatus.Depressurizing && vent.GetOxygenLevel() > 0.01)
                            {
                                isDepressurized = false;
                                break;
                            }
                            else if (vent.Status != VentStatus.Depressurizing)
                            {
                                isDepressurized = false;
                                break;
                            }
                        }
                    }

                    return isDepressurized;
                }
            }

            /// <summary>
            /// Gets a value indicating whether the room is pressurized.
            /// </summary>
            private bool IsPressurized
            {
                get
                {
                    bool isPressurized = true;
                    foreach (IMyAirVent vent in this.AirVents)
                    {
                        if (vent.Status != VentStatus.Pressurized)
                        {
                            isPressurized = false;
                            break;
                        }
                    }

                    return isPressurized;
                }
            }

            /// <summary>
            /// Gets a value indicating whether the room is airtight.
            /// </summary>
            private bool CanPressurize
            {
                get
                {
                    bool canPressurize = true;
                    foreach (IMyAirVent vent in this.AirVents)
                    {
                        if (!vent.CanPressurize)
                        {
                            canPressurize = false;
                            break;
                        }
                    }

                    return canPressurize;
                }
            }

            /// <summary>
            /// Reinitializes the airlock with new tag and settings.
            /// </summary>
            /// <param name="tag">New search tag.</param>
            /// <param name="settings">New settings.</param>
            public void Reinitialize(string tag, Settings settings)
            {
                this.settings = settings;
                this.tag = $"{this.settings.TagWrapper}{tag}{this.settings.TagWrapper}";
                this.Initialize();
            }

            /// <summary>
            /// Cycles the airlock.
            /// </summary>
            public void Cycle()
            {
                if (this.stateMachine != null)
                {
                    return;
                }

                if (this.CanPressurize)
                {
                    this.stateMachine = new AirlockStateMachine()
                    {
                        CloseDoors = this.CloseDoors,
                        OpenDoors = () => this.OpenDoors(this.InteriorDoors),
                        LockDoors = this.LockDoors,
                        UnlockDoors = () => this.UnlockDoors(this.InteriorDoors),
                        Cycle = () => this.SetAirVent(true),
                        AreDoorsClosed = () => this.DoorsAreClosed,
                        IsRoomReady = () => this.IsPressurized,
                        StartCountdown = this.startTimer
                    };
                }
                else
                {
                    this.stateMachine = new AirlockStateMachine()
                    {
                        CloseDoors = this.CloseDoors,
                        OpenDoors = () => this.OpenDoors(this.ExteriorDoors),
                        LockDoors = this.LockDoors,
                        UnlockDoors = () => this.UnlockDoors(this.ExteriorDoors),
                        Cycle = () => this.SetAirVent(false),
                        AreDoorsClosed = () => this.DoorsAreClosed,
                        IsRoomReady = () => this.IsDepressurized,
                        StartCountdown = this.startTimer
                    };
                }

                this.stateMachine.Run();
            }

            /// <summary>
            /// Checks for updates.
            /// </summary>
            /// <returns>Airlock Status.</returns>
            public Status Update()
            {
                if (this.stateMachine != null)
                {
                    AirlockStateMachine.Status machineStatus = this.stateMachine.Run();
                    
                    if(machineStatus == AirlockStateMachine.Status.COMPLETE)
                    {
                        this.stateMachine.Dispose();
                        this.stateMachine = null;
                        return Status.Complete;
                    }

                    return Status.Cycling;
                }

                return Status.Idle;
            }

            /// <summary>
            /// Initializes the airlock.
            /// </summary>
            private void Initialize()
            {
                this.Clear();

                this.search(
                    this.tag,
                    this.TerminalBlocks,
                    block => block.IsSameConstructAs(this.Me) &&
                        (block is IMyDoor ||
                        block is IMyAirVent ||
                        (block is IMyGasTank && block.DefinitionDisplayNameText.Contains("Oxygen")) ||
                        block is IMyLightingBlock));

                foreach (IMyTerminalBlock block in this.TerminalBlocks)
                {
                    if (block is IMyDoor && block.CustomName.ToLower().Contains("exterior"))
                    {
                        this.ExteriorDoors.Add((IMyDoor)block);
                    } 
                    else if (block is IMyDoor)
                    {
                        this.InteriorDoors.Add((IMyDoor)block);
                    }
                    else if (block is IMyAirVent)
                    {
                        this.AirVents.Add((IMyAirVent)block);
                    }
                    else if (block is IMyGasTank)
                    {
                        this.OxygenTanks.Add((IMyGasTank)block);
                    }
                    else if (block is IMyLightingBlock)
                    {
                        string name = block.CustomName.ToLower();
                        IMyLightingBlock lightingBlock = (IMyLightingBlock)block;
                        if (name.Contains("interior"))
                        {
                            this.InteriorLights.Add(lightingBlock);
                        }
                        else if(name.Contains("exterior"))
                        {
                            this.ExteriorLights.Add(lightingBlock);
                        }
                        else if(name.Contains("cycle"))
                        {
                            this.CycleLights.Add(lightingBlock);
                        }
                    }
                }

                this.AllDoors.AddRange(this.InteriorDoors);
                this.AllDoors.AddRange(this.ExteriorDoors);

                if (!this.IsValid())
                {
                    throw new Exception("Invalid Airlock!");
                }
            }

            /// <summary>
            /// Clears the lists.
            /// </summary>
            private void Clear()
            {
                this.InteriorDoors.Clear();
                this.ExteriorDoors.Clear();
                this.AirVents.Clear();
                this.OxygenTanks.Clear();
                this.TerminalBlocks.Clear();
                this.CycleLights.Clear();
                this.InteriorLights.Clear();
                this.ExteriorLights.Clear();
                this.AllDoors.Clear();
            }

            /// <summary>
            /// Closes all doors.
            /// </summary>
            private void CloseDoors()
            {
                foreach(IMyDoor door in this.AllDoors)
                {
                    door.CloseDoor();
                }
            }

            /// <summary>
            /// Locks all doors.
            /// </summary>
            private void LockDoors()
            {
                foreach(IMyDoor door in this.AllDoors)
                {
                    door.Enabled = false;
                }
            }

            /// <summary>
            /// Opens the doors in the list.
            /// </summary>
            /// <param name="doors">Doors to open.</param>
            private void OpenDoors(List<IMyDoor> doors)
            {
                foreach(IMyDoor door in doors)
                {
                    door.OpenDoor();
                }
            }

            /// <summary>
            /// Unlocks the doors in the list.
            /// </summary>
            /// <param name="doors">Doors to unlock.</param>
            private void UnlockDoors(List<IMyDoor> doors)
            {
                foreach(IMyDoor door in doors)
                {
                    door.Enabled = true;
                }
            }

            /// <summary>
            /// Sets the air vent to pressurize or depressurize.
            /// </summary>
            /// <param name="pressurize">A value indicating whether the airvent should pressurize the room or not.</param>
            private void SetAirVent(bool pressurize)
            {
                foreach(IMyAirVent airVent in this.AirVents)
                {
                    airVent.Depressurize = !pressurize;
                }
            }

            /// <summary>
            /// Checks if the airlock is valid.
            /// </summary>
            /// <returns>True if the airlock is valid.</returns>
            private bool IsValid()
            {
                bool isValid = true;
                StringBuilder builder = new StringBuilder();
                if (!this.InteriorDoors.Any())
                {
                    isValid = false;
                }

                if (!this.ExteriorDoors.Any())
                {
                    isValid = false;
                }

                if (!this.AirVents.Any())
                {
                    isValid = false;
                }

                if (!this.OxygenTanks.Any())
                {
                    isValid = false;
                }

                if (!isValid)
                {
                    return false;
                }

                return true;
            }
        }
    }
}
