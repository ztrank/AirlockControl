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
    /// Program partial class containing the Airlock State Machine.
    /// </summary>
    partial class Program
    {
        /// <summary>
        /// Airlock state machine.
        /// </summary>
        public class AirlockStateMachine : IDisposable
        {
            /// <summary>
            /// Status of the state machine.
            /// </summary>
            public enum Status
            {
                /// <summary>
                /// Machine is still runnings.
                /// </summary>
                RUNNING,

                /// <summary>
                /// Machine has completed.
                /// </summary>
                COMPLETE
            }

            /// <summary>
            /// State of the airlock process.
            /// </summary>
            private enum CycleState
            {
                /// <summary>
                /// Cycle is starting.
                /// </summary>
                INIT,

                /// <summary>
                /// Doors are closing.
                /// </summary>
                DOORS_CLOSING,

                /// <summary>
                /// Doors are closed.
                /// </summary>
                DOORS_CLOSED,

                /// <summary>
                /// Doors are locked.
                /// </summary>
                DOORS_LOCKED,

                /// <summary>
                /// Cycling air.
                /// </summary>
                CYCLING,

                /// <summary>
                /// Air cycled.
                /// </summary>
                CYCLED,

                /// <summary>
                /// Doors are unlocked.
                /// </summary>
                DOORS_UNLOCKED,

                /// <summary>
                /// Cycle process completed.
                /// </summary>
                FINISHED
            }

            /// <summary>
            /// Gets or sets the delegate to close doors.
            /// </summary>
            public Action CloseDoors { get; set; }

            /// <summary>
            /// Gets or sets the delegate to open doors.
            /// </summary>
            public Action OpenDoors { get; set; }

            /// <summary>
            /// Gets or sets the delegate to lock doors.
            /// </summary>
            public Action LockDoors { get; set; }

            /// <summary>
            /// Gets or sets the delegate to unlock doors.
            /// </summary>
            public Action UnlockDoors { get; set; }

            /// <summary>
            /// Gets or sets the delegate to cycle the air.
            /// </summary>
            public Action Cycle { get; set; }

            /// <summary>
            /// Gets or sets the delegate to check if doors are closed.
            /// </summary>
            public Func<bool> AreDoorsClosed { get; set; }

            /// <summary>
            /// Gets or sets the delegate to check if the room is ready.
            /// </summary>
            public Func<bool> IsRoomReady { get; set; }

            /// <summary>
            /// Gets or sets the delegate to start cycle lights.
            /// </summary>
            public Action StartCycleLights { get; set; }

            /// <summary>
            /// Gets or sets the delegate to set open lights.
            /// </summary>
            public Action SetOpenLights { get; set; }

            /// <summary>
            /// Gets or sets the delegate to set locked lights.
            /// </summary>
            public Action SetLockedLights { get; set; }

            /// <summary>
            /// Gets or sets the delegate to stop cycle lights.
            /// </summary>
            public Action StopCycleLights { get; set; }

            /// <summary>
            /// Gets or sets the delegate to start the countdown.
            /// </summary>
            public Action StartCountdown { get; set; }
            
            /// <summary>
            /// Airlock current state.
            /// </summary>
            private CycleState currentAirlockState = CycleState.INIT;

            /// <summary>
            /// Disposed value.
            /// </summary>
            private bool disposedValue;

            /// <summary>
            /// Runs the state machine.
            /// </summary>
            /// <returns>Status of the airlock.</returns>
            public Status Run()
            {
                this.StartCountdown?.Invoke();
                switch(this.currentAirlockState)
                {
                    case CycleState.INIT:
                        this.CloseDoors();
                        this.StartCycleLights?.Invoke();
                        this.currentAirlockState = CycleState.DOORS_CLOSING;
                        return Status.RUNNING;
                    case CycleState.DOORS_CLOSING:
                        if (this.AreDoorsClosed())
                        {
                            this.currentAirlockState = CycleState.DOORS_CLOSED;
                        }
                        return Status.RUNNING;
                    case CycleState.DOORS_CLOSED:
                        this.LockDoors();
                        this.SetLockedLights?.Invoke();
                        this.currentAirlockState = CycleState.DOORS_LOCKED;
                        return Status.RUNNING;
                    case CycleState.DOORS_LOCKED:
                        this.Cycle();
                        this.currentAirlockState = CycleState.CYCLING;
                        return Status.RUNNING;
                    case CycleState.CYCLING:
                        if (this.IsRoomReady())
                        {
                            this.currentAirlockState = CycleState.CYCLED;
                        }
                        return Status.RUNNING;
                    case CycleState.CYCLED:
                        this.UnlockDoors();
                        this.currentAirlockState = CycleState.DOORS_UNLOCKED;
                        this.SetOpenLights?.Invoke();
                        this.StopCycleLights?.Invoke();
                        return Status.RUNNING;
                    case CycleState.DOORS_UNLOCKED:
                        this.OpenDoors();
                        this.currentAirlockState = CycleState.FINISHED;
                        return Status.COMPLETE;
                    default:
                        return Status.RUNNING;
                }
            }

            /// <summary>
            /// Implementation of Disposable pattern.
            /// </summary>
            /// <param name="disposing">Is Disposing.</param>
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

            /// <summary>
            /// Implementation of disposable pattern.
            /// </summary>
            public void Dispose()
            {
                // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
                this.Dispose(disposing: true);
            }
        }
    }
}
