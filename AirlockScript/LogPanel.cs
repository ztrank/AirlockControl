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
        public class LogPanel
        {
            private readonly MyIni ini = new MyIni();
            private readonly IMyTextPanel logPanel;
            private readonly short MaxLines;
            private readonly List<string> Lines = new List<string>();

            public LogPanel(IMyTextPanel logPanel)
            {
                this.logPanel = logPanel;
                MyIniParseResult result;
                if (!this.ini.TryParse(this.logPanel.CustomData, out result))
                {
                    throw new Exception(result.ToString());
                }

                this.MaxLines = this.ini.Get("display", "maxLines").ToInt16();
            }

            public void WriteText(string txt)
            {
                this.Lines.Add(txt);
                

                if(this.MaxLines > 0 && this.Lines.Count > this.MaxLines)
                {
                    this.Lines.RemoveAt(0);
                }

                this.logPanel.WriteText(string.Join(Environment.NewLine, this.Lines));
            }
        }
    }
}
