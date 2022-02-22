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
    /// Airlock Program.
    /// </summary>
    partial class Program : MyGridProgram
    {
        /// <summary>
        /// Delegate for easier passing of Search method from the GridTerminalSystem.
        /// </summary>
        /// <param name="name">Search name.</param>
        /// <param name="blocks">Outputed list.</param>
        /// <param name="collect">Collect function.</param>
        public delegate void Search(string name, List<IMyTerminalBlock> blocks, Func<IMyTerminalBlock, bool> collect);

        /// <summary>
        /// List of airlock names.
        /// </summary>
        private readonly List<string> AirlockNames = new List<string>();

        /// <summary>
        /// Dictionary of commands.
        /// </summary>
        private readonly Dictionary<string, Action> Commands = new Dictionary<string, Action>();

        /// <summary>
        /// Command Line.
        /// </summary>
        private readonly MyCommandLine commandLine = new MyCommandLine();

        /// <summary>
        /// Ini class.
        /// </summary>
        private readonly MyIni ini = new MyIni();

        /// <summary>
        /// Dictionary of airlocks.
        /// </summary>
        private readonly Dictionary<string, Airlock> Airlocks = new Dictionary<string, Airlock>();

        /// <summary>
        /// Airlocks using the timer.
        /// </summary>
        private readonly List<string> UsingTimer = new List<string>();

        /// <summary>
        /// Timer delay.
        /// </summary>
        private short Timing = 1;

        /// <summary>
        /// Timer block.
        /// </summary>
        private IMyTimerBlock TimerBlock;

        /// <summary>
        /// Initializes the program.
        /// </summary>
        public Program()
        {
            this.Commands["cycle"] = this.Cycle;
            this.Commands["connect"] = this.Connect;

            this.Connect();
        }

        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="argument">Argument string.</param>
        /// <param name="updateSource">Update source.</param>
        public void Main(string argument, UpdateType updateSource)
        {
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
                        this.Echo($"Unrecognized command: {command}");
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
        /// Searches for the airlock blocks.
        /// </summary>
        private void Connect()
        {
            if (!this.ini.TryParse(this.Me.CustomData))
            {
                throw new Exception("Failed to parse custom data. See readme for details.");  
            }

            this.Timing = this.ini.Get("general", "delay").ToInt16(this.Timing);
            string delimiter = this.ini.Get("general", "delimiter").ToString(",");
            string timerBlockName = this.ini.Get("general", "timer").ToString();
            string[] tagWrapper = this.ini.Get("general", "tag-wrapper").ToString("").Split(delimiter.ToCharArray()[0]);

            if (tagWrapper.Length != 2)
            {
                tagWrapper = new string[] { string.Empty, string.Empty };
            }

            if (string.IsNullOrWhiteSpace(timerBlockName))
            {
                throw new Exception("Missing Timer Name.");
            }

            this.AirlockNames.AddRange(this.ini.Get("general", "airlocks").ToString().Split(delimiter.ToCharArray()[0]));
            IMyTerminalBlock timerBlock = this.GridTerminalSystem.GetBlockWithName(timerBlockName);
            
            if(timerBlock != null && timerBlock.IsSameConstructAs(this.Me) && timerBlock is IMyTimerBlock)
            {
                this.TimerBlock = (IMyTimerBlock)timerBlock;
            }
            else
            {
                throw new Exception("Missing Timer Block.");
            }

            Settings settings = new Settings()
            {
                TagWrapper = tagWrapper
            };

            this.Airlocks.Clear();
            foreach(string name in this.AirlockNames)
            {
                try
                {
                    this.Airlocks.Add(name, new Airlock(name, this.Me, settings, this.GridTerminalSystem.SearchBlocksOfName, () => this.StartTimer(name)));
                }
                catch (Exception)
                {
                    this.Echo($"Invalid Airlock: {name}");
                }
            }
        }

        /// <summary>
        /// Begins cycling the airlock.
        /// </summary>
        public void Cycle()
        {
            string name = this.commandLine.Argument(1);

            Airlock airlock;
            
            if (!this.Airlocks.TryGetValue(name, out airlock))
            {
                this.Echo($"Unknown airlock: {name}");
                return;
            }

            airlock.Cycle();
        }

        /// <summary>
        /// Checks running airlocks for status updates.
        /// </summary>
        public void Continue()
        {
            foreach(Airlock airlock in this.Airlocks.Values)
            {
                Airlock.Status status = airlock.Update();

                if (status == Airlock.Status.Complete)
                {
                    this.StopTimer(airlock.Name);
                }
            }
        }

        /// <summary>
        /// Starts the timer and adds the airlock name to the airlocks using the timer.
        /// </summary>
        /// <param name="name">Name of the airlock.</param>
        private void StartTimer(string name)
        {
            this.UsingTimer.Add(name);
            if (!this.TimerBlock.IsCountingDown)
            {
                this.TimerBlock.Silent = true;
                this.TimerBlock.TriggerDelay = this.Timing;
                this.TimerBlock.StartCountdown();
            }
        }

        /// <summary>
        /// Removes the name of the airlock from waiting airlocks and if none are left waiting, stops the timer.
        /// </summary>
        /// <param name="name">Airlock name.</param>
        private void StopTimer(string name)
        {
            this.UsingTimer.Remove(name);
            if (!this.UsingTimer.Any())
            {
                this.TimerBlock.StopCountdown();
            }
        }
    }
}
