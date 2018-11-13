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
                Data.items = new List<FormatBase.Model>();
                Data.LongName = heading.StartsWith (".") ? heading.Substring (1) : null;
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

            public bool Repair (int issueIndex)
            {
                if (Data.Index >= 0)
                {
                    FormatBase.Model fmtModel = Data.items[Data.Index];
                    string err = fmtModel.IssueModel.Repair (issueIndex);
                    if (err == null)
                    {
                        if (fmtModel.IssueModel.Data.RepairableCount == 0)
                            --Data.RepairableCount;
                        return true;
                    }
                }
                return false;
            }

            public bool SetIndex (int index)
            {
                if (index >= 0 && index < Data.Count)
                {
                    Data.Index = index;
                    return true;
                }
                return false;
            }
        }


        private List<FormatBase.Model> items;
        public int Index { get; private set; } = -1;
        public int TabPosition { get; private set; }
        public string LongName { get; private set; }
        public Severity MaxSeverity { get; private set; }
        public int ErrorCount { get; private set; }
        public int RepairableCount { get; private set; }
        public int Count => items.Count;
        public FormatBase Current => Index < 0 ? null : items[Index].Data;
        public bool HasError => MaxSeverity >= Severity.Error;
        public bool HasRepairables => RepairableCount != 0;

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
