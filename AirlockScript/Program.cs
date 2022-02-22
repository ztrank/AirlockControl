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

    partial class Program : MyGridProgram
    {
        private enum AirlockState
        {
            INIT,
            DOORS_CLOSING,
            DOORS_CLOSED,
            DOORS_LOCKED,
            CYCLING,
            CYCLED,
            DOORS_UNLOCKED,
            FINISHED
        }

        // This file contains your actual script.
        //
        // You can either keep all your code here, or you can create separate
        // code files to make your program easier to navigate while coding.
        //
        // In order to add a new utility class, right-click on your project, 
        // select 'New' then 'Add Item...'. Now find the 'Space Engineers'
        // category under 'Visual C# Items' on the left hand side, and select
        // 'Utility Class' in the main area. Name it in the box below, and
        // press OK. This utility class will be merged in with your code when
        // deploying your final script.
        //
        // You can also simply create a new utility class manually, you don't
        // have to use the template if you don't want to. Just do so the first
        // time to see what a utility class looks like.
        // 
        // Go to:
        // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts
        //
        // to learn more about ingame scripts.

        private readonly List<IMyDoor> InteriorDoors = new List<IMyDoor>();
        private readonly List<IMyDoor> ExteriorDoors = new List<IMyDoor>();
        private readonly List<IMyDoor> Doors = new List<IMyDoor>();
        private readonly List<IMyAirVent> AirVents = new List<IMyAirVent>();
        private readonly List<IMyGasTank> OxygentTanks = new List<IMyGasTank>();
        private readonly List<IMyTextPanel> StatusPanels = new List<IMyTextPanel>();
        private readonly List<LogPanel> LogPanels = new List<LogPanel>();
        private readonly List<IMyLightingBlock> CycleIndicators = new List<IMyLightingBlock>();
        private readonly List<IMyLightingBlock> InteriorIndicator = new List<IMyLightingBlock>();
        private readonly List<IMyLightingBlock> ExteriorIndicator = new List<IMyLightingBlock>();
        private readonly List<IMyTimerBlock> TimerBlocks = new List<IMyTimerBlock>();

        private readonly Dictionary<string, Action> Commands = new Dictionary<string, Action>();
        private readonly Dictionary<string, string> SearchTerms = new Dictionary<string, string>();
        private readonly MyCommandLine commandLine = new MyCommandLine();
        private readonly MyIni ini = new MyIni();
        private AirlockStateMachine stateMachine;
        private string Tag;
        private Color SuccessColor = new Color(51, 165, 50, 1);
        private Color DangerColor = new Color(187, 30, 16, 1);
        private Color WarningColor = new Color(255, 23, 146, 1);
        private short Timing = 1;

        public Program()
        {
            // The constructor, called only once every session and
            // always before any other method is called. Use it to
            // initialize your script. 
            //     
            // The constructor is optional and can be removed if not
            // needed.
            // 
            // It's recommended to set Runtime.UpdateFrequency 
            // here, which will allow your script to run itself without a 
            // timer block.
            this.Commands["cycle"] = this.Cycle;
            this.Commands["egress"] = this.Pressurize;
            this.Commands["ingress"] = this.Depressurize;
            this.Commands["connect"] = this.Connect;

            if (this.ini.TryParse(this.Me.CustomData))
            {
                this.SearchTerms.Add("cycle-lights", this.ini.Get("lighting", "cycle").ToString());
                this.SearchTerms.Add("interior-lights", this.ini.Get("lighting", "interior").ToString());
                this.SearchTerms.Add("exterior-lights", this.ini.Get("lighting", "exterior").ToString());

                this.SuccessColor = this.ToColor(this.ini.Get("lighting", "success-color").ToString(), this.SuccessColor);
                this.DangerColor = this.ToColor(this.ini.Get("lighting", "danger-color").ToString(), this.DangerColor);
                this.WarningColor = this.ToColor(this.ini.Get("lighting", "warning-color").ToString(), this.WarningColor);

                this.Timing = this.ini.Get("general", "timer").ToInt16(this.Timing);
            }
        }

        private Color ToColor(string rgba, Color fallback)
        {
            List<float> irgba = new List<float>();
            List<string> parts = rgba.Split(',').ToList();
            if (parts.Count < 4)
            {
                parts.Add("1");
            }

            if (parts.Count != 4)
            {
                return fallback;
            }

            foreach(string part in parts)
            {
                float value;
                if(float.TryParse(part, out value))
                {
                    irgba.Add(value);
                }
                else
                {
                    return fallback;
                }
            }

            return new Color(irgba[0], irgba[1], irgba[2], irgba[3]);
        }

        public void Save()
        {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means. 
            // 
            // This method is optional and can be removed if not
            // needed.
        }

        public void Main(string argument, UpdateType updateSource)
        {
            // The main entry point of the script, invoked every time
            // one of the programmable block's Run actions are invoked,
            // or the script updates itself. The updateSource argument
            // describes where the update came from. Be aware that the
            // updateSource is a  bitfield  and might contain more than 
            // one update type.
            // 
            // The method itself is required, but the arguments above
            // can be removed if not needed.
            try
            {
                if (this.commandLine.TryParse(argument))
                {
                    
                    Action commandAction;
                    string command = this.commandLine.Argument(0);

                    if (command == null)
                    {
                        this.Continue();
                        return;
                    }

                    if (this.Commands.TryGetValue(command, out commandAction))
                    {
                        commandAction.Invoke();
                    }
                    else
                    {
                        this.Log($"Unrecognized command: {command}");
                    }
                }
                else
                {
                    this.Continue();
                }
            }
            catch(Exception ex)
            {
                this.Echo("An error occurred during script execution.");
                this.Echo($"Exception: {ex}\n---");
            }
        }

        /// <summary>
        /// Checks if all the airlock blocks exist.
        /// </summary>
        /// <returns>True if the airlock is valid.</returns>
        private bool IsValid()
        {
            bool isValid = true;
            StringBuilder builder = new StringBuilder();
            if (!this.InteriorDoors.Any())
            {
                this.Log("Invalid Airlock: No Interior Doors");
                builder.AppendLine("No Interior Doors");
                isValid = false;
            }

            if (!this.ExteriorDoors.Any())
            {
                this.Log("Invalid Airlock: No Exterior Doors");
                builder.AppendLine("No Exterior Doors");
                isValid = false;
            }

            if (!this.AirVents.Any())
            {
                this.Log("Invalid Airlock: No Air Vents");
                builder.AppendLine("No Air Vents");
                isValid = false;
            }

            if (!this.OxygentTanks.Any())
            {
                this.Log("Invalid Airlock: No Oxygen Tanks");
                builder.AppendLine("No Oxygen Tanks");
                isValid = false;
            }

            if (!isValid)
            {
                this.WriteStatus(builder.ToString());
                return false;
            }

            return true;
        }

        /// <summary>
        /// Cycles the airlock.
        /// </summary>
        private void Cycle()
        {
            if (!this.IsValid())
            {
                return;
            }

            IMyAirVent airVent = this.AirVents[0];
            
            // If the room is airtight before cycling begins, assume the inner door is open.
            if (airVent.CanPressurize)
            {
                this.Depressurize();
            }
            else
            {
                this.Pressurize();
            }
        }

        /// <summary>
        /// Searches for the airlock blocks.
        /// </summary>
        private void Connect()
        {
            this.Tag = this.commandLine.Argument(1);
            
            if (this.commandLine.Argument(2) != null)
            {
                this.LogPanels.Clear();
                List<IMyTerminalBlock> logBlocks = new List<IMyTerminalBlock>();
                this.GridTerminalSystem.SearchBlocksOfName(this.commandLine.Argument(2), logBlocks, b => b is IMyTextPanel);
                foreach(IMyTerminalBlock block in logBlocks)
                {
                    this.LogPanels.Add(new LogPanel((IMyTextPanel)block));
                }
            }

            if (this.Tag == null)
            {
                this.Log("Connect called without a tag name. Enter search term for your airlock blocks, example: [Airlock 1]");
                return;
            }

            this.InteriorDoors.Clear();
            this.ExteriorDoors.Clear();
            this.Doors.Clear();
            this.AirVents.Clear();
            this.OxygentTanks.Clear();
            this.StatusPanels.Clear();
            this.CycleIndicators.Clear();
            this.InteriorIndicator.Clear();
            this.ExteriorIndicator.Clear();
            this.TimerBlocks.Clear();

            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();

            this.GridTerminalSystem.SearchBlocksOfName(this.Tag, blocks, block => block is IMyDoor || block is IMyAirVent || block is IMyGasTank || block is IMyTextPanel || block is IMyTimerBlock);

            foreach(IMyTerminalBlock block in blocks)
            {
                if (block is IMyDoor)
                {
                    this.Doors.Add((IMyDoor)block);
                    if (block.CustomName.ToLower().Contains("exterior"))
                    {
                        this.ExteriorDoors.Add((IMyDoor)block);
                    }
                    else
                    {
                        this.InteriorDoors.Add((IMyDoor)block);
                    }
                }
                else if (block is IMyAirVent)
                {
                    this.AirVents.Add((IMyAirVent)block);
                }
                else if (block is IMyGasTank)
                {
                    this.OxygentTanks.Add((IMyGasTank)block);
                }
                else if (block is IMyTextPanel)
                {
                    this.StatusPanels.Add((IMyTextPanel)block);
                }

                if (block is IMyTimerBlock)
                {
                    this.TimerBlocks.Add((IMyTimerBlock)block);
                }
            }


            this.SetLights("cycle-lights", this.CycleIndicators);
            this.SetLights("interior-lights", this.InteriorIndicator);
            this.SetLights("exterior-lights", this.ExteriorIndicator);
        }

        /// <summary>
        /// Sets the lights.
        /// </summary>
        /// <param name="searchTerm">Term to check the search term dictionary for.</param>
        /// <param name="lights">List of lights to add the lights to.</param>
        private void SetLights(string searchTerm, List<IMyLightingBlock> lights)
        {
            string searchTag;
            
            if (this.SearchTerms.TryGetValue(searchTerm, out searchTag))
            {
                List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
                this.GridTerminalSystem.SearchBlocksOfName(searchTag, blocks, block => block is IMyLightingBlock);
                foreach(IMyTerminalBlock block in blocks)
                {
                    lights.Add((IMyLightingBlock)block);
                }
            }
        }

        /// <summary>
        /// Executes the state machine's run method and disposes of it when it is complete.
        /// </summary>
        private void Continue()
        {
            if (this.stateMachine != null)
            {
                AirlockStateMachine.Status status = this.stateMachine.Run();
                
                if(status == AirlockStateMachine.Status.COMPLETE)
                {
                    this.stateMachine.Dispose();
                    this.stateMachine = null;
                }
            }    
        }

        /// <summary>
        /// Begins the pressurize procedure.
        /// </summary>
        private void Pressurize()
        {
            if (this.stateMachine != null)
            {
                this.Log("Airlock already cycling!");
                return;
            }

            this.stateMachine = new AirlockStateMachine()
            {
                CloseDoors = this.CloseDoors,
                OpenDoors = () => this.OpenDoors(this.InteriorDoors),
                LockDoors = this.LockDoors,
                UnlockDoors = () => this.UnlockDoors(this.InteriorDoors),
                Cycle = this.Fill,
                AreDoorsClosed = this.AreDoorsClosed,
                IsRoomReady = this.IsPressurized,
                Log = this.Log,
                StartCycleLights = () => this.SetLightValues(this.WarningColor, this.CycleIndicators),
                StopCycleLights = () => this.SetLightValues(this.WarningColor, this.CycleIndicators, false),
                SetOpenLights = () => this.SetLightValues(this.SuccessColor, this.InteriorIndicator),
                SetLockedLights = () =>
                {
                    this.SetLightValues(this.DangerColor, this.ExteriorIndicator);
                    this.SetLightValues(this.DangerColor, this.InteriorIndicator);
                },
                StartCountdown = this.StartCountdown
            };
            
            this.stateMachine.Run();
        }

        /// <summary>
        /// Begins the depressurize procedure.
        /// </summary>
        private void Depressurize()
        {
            if (this.stateMachine != null)
            {
                this.Log("Airlock already cycling!");
                return;
            }

            this.stateMachine = new AirlockStateMachine()
            {
                CloseDoors = this.CloseDoors,
                OpenDoors = () => this.OpenDoors(this.ExteriorDoors),
                LockDoors = this.LockDoors,
                UnlockDoors = () => this.UnlockDoors(this.ExteriorDoors),
                Cycle = this.Empty,
                AreDoorsClosed = this.AreDoorsClosed,
                IsRoomReady = this.IsDepressurized,
                Log = this.Log,
                StartCycleLights = () => this.SetLightValues(this.WarningColor, this.CycleIndicators),
                StopCycleLights = () => this.SetLightValues(this.WarningColor, this.CycleIndicators, false),
                SetOpenLights = () => this.SetLightValues(this.SuccessColor, this.ExteriorIndicator),
                SetLockedLights = () =>
                {
                    this.SetLightValues(this.DangerColor, this.ExteriorIndicator);
                    this.SetLightValues(this.DangerColor, this.InteriorIndicator);
                },
                StartCountdown = this.StartCountdown
            };

            this.stateMachine.Run();
        }

        private void StartCountdown()
        {
            foreach(IMyTimerBlock timer in this.TimerBlocks)
            {
                timer.StartCountdown();
            }
        }

        /// <summary>
        /// Opens the list of doors.
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
        /// Closes all the airlock doors.
        /// </summary>
        private void CloseDoors()
        {
            foreach(IMyDoor door in this.Doors)
            {
                door.CloseDoor();
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
        /// Locks the airlock doors.
        /// </summary>
        private void LockDoors()
        {
            foreach(IMyDoor door in this.Doors)
            {
                door.Enabled = false;
            }
        }

        /// <summary>
        /// Pressurizes the room.
        /// </summary>
        private void Fill()
        {
            foreach(IMyAirVent vent in this.AirVents)
            {
                vent.Depressurize = false;
            }
        }

        /// <summary>
        /// Depressurizes the room.
        /// </summary>
        private void Empty()
        {
            foreach(IMyAirVent vent in this.AirVents)
            {
                vent.Depressurize = true;
            }
        }

        /// <summary>
        /// Checks for pressurization.
        /// </summary>
        /// <returns>True if pressurized.</returns>
        private bool IsPressurized()
        {
            bool isPressurized = true;
            foreach(IMyAirVent vent in this.AirVents)
            {
                if (vent.Status != VentStatus.Pressurized)
                {
                    isPressurized = false;
                }
            }

            return isPressurized;
        }

        /// <summary>
        /// Checks for depressurization.
        /// </summary>
        /// <returns>True if depressurized.</returns>
        private bool IsDepressurized()
        {
            bool isDepressurized = true;
            foreach(IMyAirVent vent in this.AirVents)
            {
                
                if (vent.Status != VentStatus.Depressurized)
                {
                    // Vents sometimes get stuck in Depressurizing even after they have depressurized the room.
                    // Oxygen level is a float, so we just check for under 1% oxygen
                    if (vent.Status == VentStatus.Depressurizing && vent.GetOxygenLevel() > 0.01)
                    {
                        this.Log("Vent not ready: Oxygen too High");
                        isDepressurized = false;
                    }
                    else if (vent.Status != VentStatus.Depressurizing)
                    {
                        this.Log("Vent not ready: " + vent.Status.ToString());
                        isDepressurized = false;
                    }
                }
            }

            return isDepressurized;
        }

        /// <summary>
        /// Checks if the oxygen tanks are full. 
        /// </summary>
        /// <returns>True if the tanks are full.</returns>
        private bool AreOxygenTanksFull()
        {
            bool isFull = true;
            foreach(IMyGasTank tank in this.OxygentTanks)
            {
                isFull = tank.FilledRatio == 1;
            }

            return isFull;
        }

        /// <summary>
        /// Checks if the doors are closed. Used by state machine to check for animation completion.
        /// </summary>
        /// <returns>True if the doors are closed.</returns>
        private bool AreDoorsClosed()
        {
            bool isClosed = true;

            foreach (IMyDoor door in this.Doors)
            {
                if (door.Status != DoorStatus.Closed)
                {
                    isClosed = false;
                }
            }

            return isClosed;
        }

        /// <summary>
        /// Logs the text.
        /// </summary>
        /// <param name="text">Text to log.</param>
        private void Log(string text)
        {
            foreach(LogPanel logPanel in this.LogPanels)
            {
                logPanel.WriteText($"Airlock: {this.Tag} - {text}");
            }

            this.Echo(text);
        }

        /// <summary>
        /// Writes status.
        /// </summary>
        /// <param name="text">Status to write.</param>
        private void WriteStatus(string text)
        {
            foreach(IMyTextPanel panel in this.StatusPanels)
            {
                panel.WriteText(text);
            }
        }

        private void SetLightValues(Color color, List<IMyLightingBlock> lights, bool enable = true)
        {
            foreach(IMyLightingBlock light in lights)
            {
                light.Color = color;
                light.Enabled = enable;
            }
        }
    }
}
