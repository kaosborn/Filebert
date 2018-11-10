using System.Collections.Generic;
using KaosIssue;
using KaosFormat;

namespace AppViewModel
{
    public class TabInfo
    {
        private List<FormatBase> items;
        public string LongName { get; private set; }
        public int TabPosition { get; private set; }
        public Severity MaxSeverity { get; private set; }
        public int ErrorCount { get; private set; }
        public int RepairableCount { get; private set; }
        public int Count => items.Count;
        public FormatBase Current => index < 0 ? null : items[index];
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

        public TabInfo (string heading, int tabPosition)
        {
            TabPosition = tabPosition;
            if (heading.StartsWith ("."))
            {
                LongName = heading.Substring (1);
                items = new List<FormatBase>();
            }
        }

        public void Add (FormatBase fmt)
        {
            items.Add (fmt);
            if (MaxSeverity < fmt.Issues.MaxSeverity)
                MaxSeverity = fmt.Issues.MaxSeverity;
            if (fmt.Issues.MaxSeverity >= Severity.Error)
                ++ErrorCount;
            if (fmt.IsRepairable)
                ++RepairableCount;
        }

        public int IndexOf (FormatBase fmt)
         => items.IndexOf (fmt);

        public bool GetIsRepairable (int index)
         => items[index].IsRepairable;

        public Severity GetMaxSeverity (int index)
         => items[index].Issues.MaxSeverity;
    }
}
