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
                Data = new TabInfo { TabPosition = tabPosition };
                Data.items = new List<FormatBase.Model>();
                Data.LongName = heading.StartsWith (".") ? heading.Substring (1) : null;
            }

            public void Add (FormatBase.Model fmtModel)
            {
                if (Data.MaxSeverity < fmtModel.Data.Issues.MaxSeverity)
                    Data.MaxSeverity = fmtModel.Data.Issues.MaxSeverity;
                if (fmtModel.Data.Issues.MaxSeverity >= Severity.Error)
                {
                    if (Data.ErrorCount == 0)
                        Data.firstError = Data.items.Count;
                    Data.lastError = Data.items.Count;
                    ++Data.ErrorCount;
                }
                if (fmtModel.Data.IsRepairable)
                {
                    if (Data.RepairableCount == 0)
                        Data.firstRepairable = Data.items.Count;
                    Data.lastRepairable = Data.items.Count;
                    ++Data.RepairableCount;
                }
                Data.items.Add (fmtModel);
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
                        {
                            --Data.RepairableCount;
                            if (Data.RepairableCount == 0)
                            { Data.firstRepairable = 0; Data.lastRepairable = -1; }
                            else
                                if (issueIndex == Data.firstRepairable)
                                {
                                    for (int ix = issueIndex+1; ; ++ix)
                                        if (Data.items[ix].Data.IsRepairable)
                                        { Data.firstRepairable = ix; break; }
                                }
                                else if (issueIndex == Data.lastRepairable)
                                    for (int ix = issueIndex-1; ; --ix)
                                        if (Data.items[ix].Data.IsRepairable)
                                        { Data.lastRepairable = ix; break; }
                        }
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
        private int firstError = 0;
        private int lastError = -1;
        private int firstRepairable = 0;
        private int lastRepairable = -1;

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

        public bool IsFirstSeekable => Count > 0 && Index != 0;
        public bool IsLastSeekable => Count > 0 && Index != Count-1;
        public bool IsPrevSeekable => Count > 0 && Index > 0;
        public bool IsNextSeekable => Count > 0 && Index < Count-1;

        public bool IsFirstErrorSeekable => ErrorCount > 0 && Index != firstError;
        public bool IsLastErrorSeekable => ErrorCount > 0 && Index != lastError;
        public bool IsPrevErrorSeekable => ErrorCount > 0 && Index > firstError;
        public bool IsNextErrorSeekable => ErrorCount > 0 && Index < lastError;

        public bool IsFirstRepairableSeekable => RepairableCount > 0 && Index != firstRepairable;
        public bool IsLastRepairableSeekable => RepairableCount > 0 && Index != lastRepairable;
        public bool IsPrevRepairableSeekable => RepairableCount > 0 && Index > firstRepairable;
        public bool IsNextRepairableSeekable => RepairableCount > 0 && Index < lastRepairable;

        private TabInfo() { }

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
