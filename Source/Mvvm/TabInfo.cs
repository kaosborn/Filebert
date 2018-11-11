using System.Collections.Generic;
using KaosIssue;
using KaosFormat;

namespace AppViewModel
{
    public class TabInfo
    {
        public class Model
        {
            public TabInfo Data { get; private set; }

            public Model (string heading, int tabPosition)
            {
                Data = new TabInfo (tabPosition);

                if (heading.StartsWith ("."))
                {
                    Data.LongName = heading.Substring (1);
                    Data.items = new List<FormatBase.Model>();
                }
            }

            public void Add (FormatBase.Model fmtModel)
            {
                Data.items.Add (fmtModel);
                if (Data.MaxSeverity < fmtModel.Data.Issues.MaxSeverity)
                    Data.MaxSeverity = fmtModel.Data.Issues.MaxSeverity;
                if (fmtModel.Data.Issues.MaxSeverity >= Severity.Error)
                    ++Data.ErrorCount;
                if (fmtModel.Data.IsRepairable)
                    ++Data.RepairableCount;
            }
        }

        private List<FormatBase.Model> items;
        public string LongName { get; private set; }
        public int TabPosition { get; private set; }
        public Severity MaxSeverity { get; private set; }
        public int ErrorCount { get; private set; }
        public int RepairableCount { get; private set; }
        public int Count => items.Count;
        public FormatBase Current => index < 0 ? null : items[index].Data;
        public bool HasError => MaxSeverity >= Severity.Error;
        public bool HasRepairables => RepairableCount != 0;

        private int index = -1;
        public int Index
        {
            get => index;
            set
            {
                if (items.Count > 0 && value >= 0 && value < items.Count)
                    index = value;
            }
        }

        private TabInfo (int tabPosition)
         => TabPosition = tabPosition;

        public int IndexOf (FormatBase fmt)
        {
            for (int ix = 0; ix < items.Count; ++ix)
                if (items[ix].Data == fmt)
                    return ix;
            return -1;
        }

        public bool GetIsRepairable (int index)
         => items[index].Data.IsRepairable;

        public Severity GetMaxSeverity (int index)
         => items[index].Data.Issues.MaxSeverity;
    }
}
