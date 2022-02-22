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

    partial class Program
    {
        public class AirlockStateMachine : IDisposable
        {
            public enum Status
            {
                RUNNING,
                COMPLETE
            }

            public Action CloseDoors { get; set; }
            public Action OpenDoors { get; set; }
            public Action LockDoors { get; set; }
            public Action UnlockDoors { get; set; }
            public Action Cycle { get; set; }
            public Func<bool> AreDoorsClosed { get; set; }
            public Func<bool> IsRoomReady { get; set; }
            public Action<string> Log { get; set; }
            public Action StartCycleLights { get; set; }
            public Action SetOpenLights { get; set; }
            public Action SetLockedLights { get; set; }
            public Action StopCycleLights { get; set; }
            public Action StartCountdown { get; set; }
            

            private AirlockState currentAirlockState = AirlockState.INIT;
            private bool disposedValue;

            public Status Run()
            {
                this.StartCountdown?.Invoke();
                switch(this.currentAirlockState)
                {
                    case AirlockState.INIT:
                        this.Log("Beginning Airlock Cycle: Closing doors.");
                        this.CloseDoors();
                        this.StartCycleLights?.Invoke();
                        this.currentAirlockState = AirlockState.DOORS_CLOSING;
                        return Status.RUNNING;
                    case AirlockState.DOORS_CLOSING:
                        if (this.AreDoorsClosed())
                        {
                            this.currentAirlockState = AirlockState.DOORS_CLOSED;
                        }
                        return Status.RUNNING;
                    case AirlockState.DOORS_CLOSED:
                        this.Log("Doors Closed, locking doors.");
                        this.LockDoors();
                        this.SetLockedLights?.Invoke();
                        this.currentAirlockState = AirlockState.DOORS_LOCKED;
                        return Status.RUNNING;
                    case AirlockState.DOORS_LOCKED:
                        this.Log("Doors Locked, beginning atmosphere cycling.");
                        this.Cycle();
                        this.currentAirlockState = AirlockState.CYCLING;
                        return Status.RUNNING;
                    case AirlockState.CYCLING:
                        if (this.IsRoomReady())
                        {
                            this.currentAirlockState = AirlockState.CYCLED;
                        }
                        return Status.RUNNING;
                    case AirlockState.CYCLED:
                        this.Log("Atmosphere cycling complete. Unlocking doors.");
                        this.UnlockDoors();
                        this.currentAirlockState = AirlockState.DOORS_UNLOCKED;
                        this.SetOpenLights?.Invoke();
                        this.StopCycleLights?.Invoke();
                        return Status.RUNNING;
                    case AirlockState.DOORS_UNLOCKED:
                        this.Log("Doors unlocked, opening doors.");
                        this.OpenDoors();
                        this.Log("Airlock Cycle Complete.");
                        this.currentAirlockState = AirlockState.FINISHED;
                        return Status.COMPLETE;
                    default:
                        return Status.RUNNING;
                }
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        // TODO: dispose managed state (managed objects)
                    }

                    // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                    // TODO: set large fields to null
                    disposedValue = true;
                }
            }

            public void Dispose()
            {
                // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
                this.Dispose(disposing: true);
            }
        }
    }
}
